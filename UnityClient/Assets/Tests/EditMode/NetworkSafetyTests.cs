using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;
using ServerAuthoritativeAnimation.Client.Networking;
using UnityEngine;

namespace ServerAuthoritativeAnimation.Client.Tests
{
    public sealed class NetworkSafetyTests
    {
        [Test]
        public void HeartbeatFallsBackToZeroWhenMainThreadInputIsStale()
        {
            var gameObject = new GameObject("NetworkSafetyTest");
            try
            {
                var client = gameObject.AddComponent<ServerWebSocketClient>();
                var type = typeof(ServerWebSocketClient);
                var jsonField = type.GetField("latestInputJson", BindingFlags.Instance | BindingFlags.NonPublic);
                var timestampField = type.GetField(
                    "latestInputTimestamp",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var getFreshInput = type.GetMethod(
                    "GetFreshInputJson",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(jsonField, Is.Not.Null);
                Assert.That(timestampField, Is.Not.Null);
                Assert.That(getFreshInput, Is.Not.Null);

                jsonField.SetValue(client, "{\"x\":1.0,\"z\":0.0}");
                timestampField.SetValue(client, Stopwatch.GetTimestamp());
                Assert.That(getFreshInput.Invoke(client, null), Is.EqualTo("{\"x\":1.0,\"z\":0.0}"));

                timestampField.SetValue(client, Stopwatch.GetTimestamp() - Stopwatch.Frequency);
                Assert.That(getFreshInput.Invoke(client, null), Is.EqualTo("{\"x\":0.0,\"z\":0.0}"));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
