using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ServerAuthoritativeAnimation.Client.Input;
using ServerAuthoritativeAnimation.Client.Presentation;
using ServerAuthoritativeAnimation.Client.Protocol;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace ServerAuthoritativeAnimation.Client.Networking
{
    /// <summary>
    /// Sends only the normalized x/z input vector and displays server-authored snapshots.
    /// Unity objects and JsonUtility snapshot parsing are touched only from Update.
    /// </summary>
    public sealed class ServerWebSocketClient : MonoBehaviour
    {
        private const int HeartbeatMilliseconds = 50;
        private const int StaleInputMilliseconds = 250;
        private const int ReconnectMilliseconds = 1000;
        private const int ReceiveBufferBytes = 16 * 1024;
        private const int MaximumSnapshotBytes = 256 * 1024;
        private const string ZeroInputJson = "{\"x\":0.0,\"z\":0.0}";

        [SerializeField] private string serverUrl = "ws://127.0.0.1:5000/ws";
        [SerializeField] private SnapshotView snapshotView;

        private readonly ConcurrentQueue<string> networkEvents = new ConcurrentQueue<string>();
        private CancellationTokenSource lifetimeCancellation;
        private ClientWebSocket activeSocket;
        private Task connectionTask;
        private volatile string latestInputJson = ZeroInputJson;
        private readonly Queue<(string json, double receivedAt)> receivedSnapshots = new Queue<(string, double)>();
        private readonly SnapshotBuffer snapshotBuffer = new SnapshotBuffer();
        private bool resetSnapshots;
        private long latestInputTimestamp;
        private string connectionStatus = "Disconnected";

        public string ConnectionStatus
        {
            get { return connectionStatus; }
        }

        public void Configure(string url, SnapshotView view)
        {
            if (!string.IsNullOrWhiteSpace(url))
            {
                serverUrl = url;
            }

            snapshotView = view;
        }

        private void Start()
        {
            if (snapshotView == null)
            {
                snapshotView = FindFirstObjectByType<SnapshotView>();
            }

            Uri endpoint;
            if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out endpoint) ||
                (endpoint.Scheme != "ws" && endpoint.Scheme != "wss"))
            {
                connectionStatus = "Invalid WebSocket URL";
                Debug.LogError("Invalid WebSocket server URL: " + serverUrl, this);
                enabled = false;
                return;
            }

            lifetimeCancellation = new CancellationTokenSource();
            connectionTask = RunConnectionLoopAsync(endpoint, lifetimeCancellation.Token);
        }

        private void Update()
        {
            var input = WasdInput.ReadCurrent();
            SetLatestInput(input);

            string networkEvent;
            while (networkEvents.TryDequeue(out networkEvent))
            {
                connectionStatus = networkEvent;
                Debug.Log(networkEvent, this);
            }

            lock (receivedSnapshots)
            {
                if (resetSnapshots)
                {
                    snapshotBuffer.Clear();
                    resetSnapshots = false;
                }
                while (receivedSnapshots.Count > 0)
                {
                    var packet = receivedSnapshots.Dequeue();
                    try
                    {
                        snapshotBuffer.Add(JsonUtility.FromJson<ServerSnapshotMessage>(packet.json), packet.receivedAt);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning("Ignored malformed server snapshot: " + exception.Message, this);
                    }
                }
            }
            if (snapshotView != null && snapshotBuffer.Sample(MonotonicSeconds(), out var from, out var to, out var t))
                snapshotView.ApplyInterpolated(from, to, t);
        }

        private static double MonotonicSeconds() => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                SetLatestInput(Vector2.zero);
            }
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused)
            {
                SetLatestInput(Vector2.zero);
            }
        }

        private void OnDisable()
        {
            SetLatestInput(Vector2.zero);
        }

        private void SetLatestInput(Vector2 input)
        {
            latestInputJson = JsonUtility.ToJson(new InputVectorMessage(input.x, input.y));
            Interlocked.Exchange(ref latestInputTimestamp, Stopwatch.GetTimestamp());
        }

        private async Task RunConnectionLoopAsync(Uri endpoint, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                ClientWebSocket socket = null;
                CancellationTokenSource connectionCancellation = null;

                try
                {
                    socket = new ClientWebSocket();
                    activeSocket = socket;
                    networkEvents.Enqueue("Connecting to " + endpoint);
                    await socket.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
                    lock (receivedSnapshots)
                    {
                        receivedSnapshots.Clear();
                        resetSnapshots = true;
                    }
                    networkEvents.Enqueue("Connected to " + endpoint);

                    connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    var receiveTask = ReceiveSnapshotsAsync(socket, connectionCancellation.Token);
                    var heartbeatTask = SendHeartbeatAsync(socket, connectionCancellation.Token);

                    await Task.WhenAny(receiveTask, heartbeatTask).ConfigureAwait(false);
                    connectionCancellation.Cancel();

                    try
                    {
                        await Task.WhenAll(receiveTask, heartbeatTask).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (WebSocketException)
                    {
                    }

                    await CloseOutputSafelyAsync(socket).ConfigureAwait(false);
                    socket.Abort();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception exception)
                {
                    networkEvents.Enqueue("WebSocket unavailable: " + exception.Message);
                }
                finally
                {
                    if (connectionCancellation != null)
                    {
                        connectionCancellation.Dispose();
                    }

                    if (socket != null)
                    {
                        socket.Dispose();
                    }

                    if (ReferenceEquals(activeSocket, socket))
                    {
                        activeSocket = null;
                    }
                }

                if (!cancellationToken.IsCancellationRequested)
                {
                    networkEvents.Enqueue("Disconnected; retrying in 1 second");
                    try
                    {
                        await Task.Delay(ReconnectMilliseconds, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        private async Task SendHeartbeatAsync(ClientWebSocket socket, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                var payload = Encoding.UTF8.GetBytes(GetFreshInputJson());
                await socket.SendAsync(
                    new ArraySegment<byte>(payload),
                    WebSocketMessageType.Text,
                    true,
                    cancellationToken).ConfigureAwait(false);

                await Task.Delay(HeartbeatMilliseconds, cancellationToken).ConfigureAwait(false);
            }
        }

        private string GetFreshInputJson()
        {
            var lastUpdate = Interlocked.Read(ref latestInputTimestamp);
            var elapsedTicks = Stopwatch.GetTimestamp() - lastUpdate;
            var staleTicks = (Stopwatch.Frequency * StaleInputMilliseconds) / 1000L;
            return lastUpdate == 0L || elapsedTicks >= staleTicks ? ZeroInputJson : latestInputJson;
        }

        private async Task ReceiveSnapshotsAsync(ClientWebSocket socket, CancellationToken cancellationToken)
        {
            var buffer = new byte[ReceiveBufferBytes];

            while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                using (var message = new MemoryStream())
                {
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await socket.ReceiveAsync(
                            new ArraySegment<byte>(buffer),
                            cancellationToken).ConfigureAwait(false);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            return;
                        }

                        if (message.Length + result.Count > MaximumSnapshotBytes)
                        {
                            throw new InvalidDataException(
                                "Server snapshots are limited to " + MaximumSnapshotBytes + " bytes.");
                        }

                        message.Write(buffer, 0, result.Count);
                    }
                    while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        lock (receivedSnapshots)
                        {
                            if (receivedSnapshots.Count >= 32) receivedSnapshots.Dequeue();
                            receivedSnapshots.Enqueue((Encoding.UTF8.GetString(message.ToArray()), MonotonicSeconds()));
                        }
                    }
                }
            }
        }

        private static async Task CloseOutputSafelyAsync(ClientWebSocket socket)
        {
            if (socket.State != WebSocketState.Open && socket.State != WebSocketState.CloseReceived)
            {
                return;
            }

            using (var timeout = new CancellationTokenSource())
            {
                timeout.CancelAfter(1000);
                try
                {
                    await socket.CloseOutputAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Client session ended.",
                        timeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
                catch (WebSocketException)
                {
                }
            }
        }

        private void OnDestroy()
        {
            if (lifetimeCancellation == null)
            {
                return;
            }

            lifetimeCancellation.Cancel();
            if (activeSocket != null)
            {
                activeSocket.Abort();
            }

            lifetimeCancellation.Dispose();
            lifetimeCancellation = null;
            connectionTask = null;
        }
    }
}

