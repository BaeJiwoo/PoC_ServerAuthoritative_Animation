using System.Linq;
using NUnit.Framework;
using ServerAuthoritativeAnimation.Client.Input;
using ServerAuthoritativeAnimation.Client.Protocol;
using UnityEngine;

namespace ServerAuthoritativeAnimation.Client.Tests
{
    public sealed class WasdInputTests
    {
        [TestCase(true, false, false, false, 0f, 1f)]
        [TestCase(false, true, false, false, 0f, -1f)]
        [TestCase(false, false, true, false, -1f, 0f)]
        [TestCase(false, false, false, true, 1f, 0f)]
        [TestCase(false, false, false, false, 0f, 0f)]
        public void CardinalInputsUseTheRequiredAxes(
            bool w,
            bool s,
            bool a,
            bool d,
            float expectedX,
            float expectedZ)
        {
            var result = WasdInput.FromButtons(w, s, a, d);

            Assert.That(result.x, Is.EqualTo(expectedX).Within(0.00001f));
            Assert.That(result.y, Is.EqualTo(expectedZ).Within(0.00001f));
        }

        [TestCase(true, false, true, false, -1f, 1f)]
        [TestCase(true, false, false, true, 1f, 1f)]
        [TestCase(false, true, true, false, -1f, -1f)]
        [TestCase(false, true, false, true, 1f, -1f)]
        public void DiagonalInputsAreNormalized(
            bool w,
            bool s,
            bool a,
            bool d,
            float signX,
            float signZ)
        {
            var result = WasdInput.FromButtons(w, s, a, d);

            Assert.That(result.magnitude, Is.EqualTo(1f).Within(0.00001f));
            Assert.That(Mathf.Sign(result.x), Is.EqualTo(signX));
            Assert.That(Mathf.Sign(result.y), Is.EqualTo(signZ));
        }

        [Test]
        public void OpposingKeysCancel()
        {
            Assert.That(WasdInput.FromButtons(true, true, false, false), Is.EqualTo(Vector2.zero));
            Assert.That(WasdInput.FromButtons(false, false, true, true), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void ClientPayloadHasExactlyXAndZPublicFields()
        {
            var fields = typeof(InputVectorMessage)
                .GetFields()
                .Select(field => field.Name)
                .OrderBy(name => name)
                .ToArray();

            CollectionAssert.AreEqual(new[] { "x", "z" }, fields);
            Assert.That(JsonUtility.ToJson(new InputVectorMessage(0.25f, -0.5f)),
                Is.EqualTo("{\"x\":0.25,\"z\":-0.5}"));
        }
    }
}

