using System.Net.WebSockets;
using System.Text.Json;
using ServerAuthoritativeAnimation.Core.Animation;
using ServerAuthoritativeAnimation.Core.Simulation;

namespace ServerAuthoritativeAnimation.Server.Networking;

public sealed class WebSocketCharacterSession(
    ILogger<WebSocketCharacterSession> logger,
    BakedAnimationClip animation)
{
    public const int TickRate = 20;
    public static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1d / TickRate);
    public static readonly TimeSpan InputTimeout = TimeSpan.FromSeconds(0.5d);

    private const int ReceiveBufferSize = 1024;
    private const int MaximumMessageBytes = 4096;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task RunAsync(WebSocket socket, CancellationToken requestAborted)
    {
        ArgumentNullException.ThrowIfNull(socket);

        var inputState = new LatestInputState();
        var simulation = new CharacterSimulation(moveSpeed: 2.4f, animation: animation);
        using var sessionCancellation = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
        using var sendGate = new SemaphoreSlim(1, 1);

        var receiveTask = ReceiveInputsAsync(socket, inputState, sendGate, sessionCancellation.Token);
        var simulationTask = RunSimulationAsync(
            socket,
            inputState,
            simulation,
            sendGate,
            sessionCancellation.Token);

        await Task.WhenAny(receiveTask, simulationTask);
        await sessionCancellation.CancelAsync();

        try
        {
            await Task.WhenAll(receiveTask, simulationTask);
        }
        catch (OperationCanceledException) when (sessionCancellation.IsCancellationRequested)
        {
            // The companion loop is expected to be cancelled when either half of the session ends.
        }
        catch (WebSocketException exception)
        {
            logger.LogDebug(exception, "WebSocket character session disconnected.");
        }
        finally
        {
            await CloseOutputSafelyAsync(
                socket,
                sendGate,
                WebSocketCloseStatus.NormalClosure,
                "Session ended.");
        }
    }

    private static async Task ReceiveInputsAsync(
        WebSocket socket,
        LatestInputState inputState,
        SemaphoreSlim sendGate,
        CancellationToken cancellationToken)
    {
        var receiveBuffer = new byte[ReceiveBufferSize];
        using var messageBuffer = new MemoryStream();

        while (!cancellationToken.IsCancellationRequested && IsReadable(socket.State))
        {
            var result = await socket.ReceiveAsync(receiveBuffer, cancellationToken);
            if (result.MessageType is WebSocketMessageType.Close)
            {
                await CloseOutputSafelyAsync(
                    socket,
                    sendGate,
                    result.CloseStatus ?? WebSocketCloseStatus.NormalClosure,
                    result.CloseStatusDescription ?? "Client closed the session.");
                return;
            }

            if (result.MessageType is not WebSocketMessageType.Text)
            {
                await CloseOutputSafelyAsync(
                    socket,
                    sendGate,
                    WebSocketCloseStatus.InvalidMessageType,
                    "Only UTF-8 JSON text messages are accepted.");
                return;
            }

            if (messageBuffer.Length + result.Count > MaximumMessageBytes)
            {
                await CloseOutputSafelyAsync(
                    socket,
                    sendGate,
                    WebSocketCloseStatus.MessageTooBig,
                    $"Input messages are limited to {MaximumMessageBytes} bytes.");
                return;
            }

            await messageBuffer.WriteAsync(receiveBuffer.AsMemory(0, result.Count), cancellationToken);
            if (!result.EndOfMessage)
            {
                continue;
            }

            if (!messageBuffer.TryGetBuffer(out var messageBytes) ||
                !InputMessageParser.TryParse(messageBytes.AsMemory(0, checked((int)messageBuffer.Length)), out var input))
            {
                await CloseOutputSafelyAsync(
                    socket,
                    sendGate,
                    WebSocketCloseStatus.InvalidPayloadData,
                    "Expected exactly {\"x\": number, \"z\": number}.");
                return;
            }

            inputState.Update(input);
            messageBuffer.SetLength(0);
        }
    }

    private static async Task RunSimulationAsync(
        WebSocket socket,
        LatestInputState inputState,
        CharacterSimulation simulation,
        SemaphoreSlim sendGate,
        CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TickInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            if (socket.State is not WebSocketState.Open)
            {
                return;
            }

            simulation.Tick(inputState.ReadActive(InputTimeout), (float)TickInterval.TotalSeconds);
            var serverTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000d;
            var snapshot = simulation.CreateSnapshot(serverTime);
            var payload = JsonSerializer.SerializeToUtf8Bytes(snapshot, SerializerOptions);

            await sendGate.WaitAsync(cancellationToken);
            try
            {
                if (socket.State is not WebSocketState.Open)
                {
                    return;
                }

                await socket.SendAsync(
                    payload,
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    cancellationToken);
            }
            finally
            {
                sendGate.Release();
            }
        }
    }

    private static bool IsReadable(WebSocketState state) =>
        state is WebSocketState.Open or WebSocketState.CloseSent;

    private static async Task CloseOutputSafelyAsync(
        WebSocket socket,
        SemaphoreSlim sendGate,
        WebSocketCloseStatus status,
        string description)
    {
        await sendGate.WaitAsync(CancellationToken.None);
        try
        {
            if (socket.State is not (WebSocketState.Open or WebSocketState.CloseReceived))
            {
                return;
            }

            await socket.CloseOutputAsync(status, description, CancellationToken.None);
        }
        catch (WebSocketException)
        {
            // The peer may disappear before the close frame can be sent.
        }
        finally
        {
            sendGate.Release();
        }
    }
}
