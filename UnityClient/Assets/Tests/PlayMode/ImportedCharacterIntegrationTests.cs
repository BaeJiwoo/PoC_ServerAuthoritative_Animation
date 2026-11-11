using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ServerAuthoritativeAnimation.Client.Networking;
using ServerAuthoritativeAnimation.Client.Presentation;
using ServerAuthoritativeAnimation.Client.Protocol;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ServerAuthoritativeAnimation.Client.PlayModeTests
{
    public sealed class ImportedCharacterIntegrationTests
    {
        [UnityTest]
        public IEnumerator ImportedBobDisplaysTheExactServerBonesWithoutAnimator()
        {
            var url = Environment.GetEnvironmentVariable("SAA_INTEGRATION_WS");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("Set SAA_INTEGRATION_WS to run the real server integration test.");
            yield return null;

            var client = Object.FindFirstObjectByType<ServerWebSocketClient>();
            client.enabled = false;
            var view = Object.FindFirstObjectByType<SnapshotView>();
            using (var cancellation = new CancellationTokenSource(15000))
            using (var socket = new ClientWebSocket())
            {
                try
                {
                    var connect = socket.ConnectAsync(new Uri(url), cancellation.Token);
                    while (!connect.IsCompleted) yield return null;
                    connect.GetAwaiter().GetResult();

                    var directions = new[] { new Vector2(0,1), new Vector2(0,-1), new Vector2(-1,0),
                        new Vector2(1,0), new Vector2(-1,1).normalized, new Vector2(1,1).normalized };
                    ServerSnapshotMessage previous = null;
                    foreach (var direction in directions)
                    {
                        var send = Send(socket, direction, cancellation.Token);
                        while (!send.IsCompleted) yield return null;
                        send.GetAwaiter().GetResult();
                        ServerSnapshotMessage snapshot;
                        do
                        {
                            var receive = Receive(socket, cancellation.Token);
                            while (!receive.IsCompleted) yield return null;
                            snapshot = JsonUtility.FromJson<ServerSnapshotMessage>(receive.GetAwaiter().GetResult());
                        }
                        while (!snapshot.isMoving || Mathf.Abs(Mathf.DeltaAngle(snapshot.root.yawDegrees,
                            Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg)) > .01f);

                        view.Apply(snapshot);
                        Assert.That(snapshot.bones.Length, Is.GreaterThan(30));
                        Assert.That(snapshot.hitboxes.Length, Is.EqualTo(10));
                        Assert.That(Object.FindObjectsByType<Animator>(FindObjectsSortMode.None), Is.Empty);
                        var identity = view.RootPose.GetComponentInChildren<BakedCharacterIdentity>();
                        Assert.That(identity, Is.Not.Null);
                        Assert.That(identity.skeletonId, Is.EqualTo(snapshot.skeletonId));
                        foreach (var bone in snapshot.bones)
                        {
                            var transform = identity.transform.Find(bone.path);
                            Assert.That(transform, Is.Not.Null, bone.path);
                            Assert.That(Vector3.Distance(transform.position, bone.worldPosition.ToUnityVector()),
                                Is.LessThan(0.0002f), bone.path);
                            Assert.That(Quaternion.Angle(transform.rotation, bone.worldRotation.ToUnityQuaternion()),
                                Is.LessThan(0.08f), bone.path);
                        }
                        Assert.That(identity.GetComponentsInChildren<SkinnedMeshRenderer>(), Is.Not.Empty);
                        if (previous != null)
                        {
                            view.ApplyInterpolated(previous, snapshot, .5f);
                            Assert.That(Vector3.Distance(view.RootPose.position, Vector3.Lerp(
                                previous.root.position.ToUnityVector(), snapshot.root.position.ToUnityVector(), .5f)), Is.LessThan(.0001f));
                            Assert.That(Quaternion.Angle(view.RootPose.rotation, Quaternion.Slerp(
                                previous.root.rotation.ToUnityQuaternion(), snapshot.root.rotation.ToUnityQuaternion(), .5f)), Is.LessThan(.08f));
                            for (var i = 0; i < snapshot.bones.Length; i++)
                            {
                                var bone = snapshot.bones[i];
                                var actual = identity.transform.Find(bone.path);
                                Assert.That(Vector3.Distance(actual.localPosition, Vector3.Lerp(
                                    previous.bones[i].localPosition.ToUnityVector(), bone.localPosition.ToUnityVector(), .5f)), Is.LessThan(.0001f));
                                Assert.That(Quaternion.Angle(actual.localRotation, Quaternion.Slerp(
                                    previous.bones[i].localRotation.ToUnityQuaternion(), bone.localRotation.ToUnityQuaternion(), .5f)), Is.LessThan(.08f));
                            }
                            for (var i = 0; i < snapshot.hitboxes.Length; i++)
                            {
                                var box = snapshot.hitboxes[i];
                                var actual = view.transform.Find("ServerHitboxes/" + box.name);
                                Assert.That(Vector3.Distance(actual.position, Vector3.Lerp(
                                    previous.hitboxes[i].center.ToUnityVector(), box.center.ToUnityVector(), .5f)), Is.LessThan(.0001f));
                                Assert.That(Quaternion.Angle(actual.rotation, Quaternion.Slerp(
                                    previous.hitboxes[i].rotation.ToUnityQuaternion(), box.rotation.ToUnityQuaternion(), .5f)), Is.LessThan(.08f));
                            }
                            view.Apply(snapshot);
                        }
                        previous = snapshot;
                    }

                    var imagePath = Environment.GetEnvironmentVariable("SAA_PREVIEW_PATH");
                    if (!string.IsNullOrEmpty(imagePath))
                    {
                        yield return null;
                        CapturePreview(Path.Combine(Path.GetDirectoryName(imagePath), "bob-walking.png"));
                    }

                    var release = Send(socket, Vector2.zero, cancellation.Token);
                    while (!release.IsCompleted) yield return null;
                    release.GetAwaiter().GetResult();
                    ServerSnapshotMessage stopped;
                    do
                    {
                        var receive = Receive(socket, cancellation.Token);
                        while (!receive.IsCompleted) yield return null;
                        stopped = JsonUtility.FromJson<ServerSnapshotMessage>(receive.GetAwaiter().GetResult());
                    }
                    while (stopped.isMoving);
                    view.Apply(stopped);
                    var savedPhase = view.AnimationPhase;
                    var model = view.RootPose.GetComponentInChildren<BakedCharacterIdentity>();
                    var positions = model.GetComponentsInChildren<Transform>().Select(t => t.position).ToArray();
                    yield return null;
                    yield return null;
                    Assert.That(view.AnimationPhase, Is.EqualTo(savedPhase));
                    Assert.That(model.GetComponentsInChildren<Transform>().Select(t => t.position).ToArray(),
                        Is.EqualTo(positions));
                    if (!string.IsNullOrEmpty(imagePath)) CapturePreview(imagePath);
                }
                finally
                {
                    socket.Abort();
                    client.enabled = true;
                }
            }
        }

        private static Task Send(ClientWebSocket socket, Vector2 direction, CancellationToken cancellation)
        {
            var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new InputVectorMessage(direction.x, direction.y)));
            return socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellation);
        }

        private static async Task<string> Receive(ClientWebSocket socket, CancellationToken cancellation)
        {
            var buffer = new byte[65536];
            using (var stream = new MemoryStream())
            {
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellation).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close) throw new IOException("Server closed before a snapshot.");
                    stream.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private static void CapturePreview(string path)
        {
            var camera = Camera.main;
            var target = new RenderTexture(1280, 900, 24);
            var previous = RenderTexture.active;
            var oldTarget = camera.targetTexture;
            var texture = new Texture2D(1280, 900, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1280, 900), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget;
                RenderTexture.active = previous;
                Object.Destroy(texture);
                Object.Destroy(target);
            }
        }
    }
}
