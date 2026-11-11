using System.Collections;
using System.Linq;
using NUnit.Framework;
using ServerAuthoritativeAnimation.Client.Networking;
using ServerAuthoritativeAnimation.Client.Presentation;
using ServerAuthoritativeAnimation.Client.Protocol;
using UnityEngine;
using UnityEngine.TestTools;

namespace ServerAuthoritativeAnimation.Client.PlayModeTests
{
    public sealed class RuntimeBootstrapTests
    {
        private static readonly string[] RequiredObjects =
        {
            "CharacterRoot",
            "RootMarker",
            "Forward(+Z)",
            "Head",
            "Torso",
            "LeftArm",
            "RightArm",
            "LeftLeg",
            "RightLeg",
            "Ground"
        };

        [UnityTest]
        public IEnumerator EmptySceneBuildsSnapshotOnlyPresentation()
        {
            yield return null;

            Assert.That(Object.FindFirstObjectByType<ServerWebSocketClient>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<SnapshotView>(), Is.Not.Null);
            Assert.That(Camera.main, Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<Light>(), Is.Not.Null);
            Assert.That(Object.FindObjectsByType<Behaviour>(FindObjectsSortMode.None)
                    .Any(component => component.GetType().Name == "Animator"),
                Is.False,
                "The client must not run an Animator.");

            for (var index = 0; index < RequiredObjects.Length; index++)
            {
                Assert.That(GameObject.Find(RequiredObjects[index]), Is.Not.Null,
                    "Missing runtime visualization object: " + RequiredObjects[index]);
            }
        }

        [UnityTest]
        public IEnumerator ServerSnapshotDrivesRootAndEveryHitboxTransform()
        {
            yield return null;

            var view = Object.FindFirstObjectByType<SnapshotView>();
            var rootRotation = Quaternion.Euler(0f, 90f, 0f);
            var snapshot = new ServerSnapshotMessage
            {
                serverTime = 12.5,
                isMoving = true,
                animationPhase = 0.25f,
                root = new RootSnapshot
                {
                    position = Vector(2f, 0f, -3f),
                    rotation = Rotation(rootRotation),
                    yawDegrees = 90f
                },
                hitboxes = RequiredObjects
                    .Where(name => name == "Head" || name == "Torso" ||
                                   name == "LeftArm" || name == "RightArm" ||
                                   name == "LeftLeg" || name == "RightLeg")
                    .Select((name, index) => new HitboxSnapshot
                    {
                        name = name,
                        center = Vector(3f + index, 1f + index, -2f - index),
                        rotation = Rotation(Quaternion.Euler(index * 5f, 90f, 0f)),
                        size = Vector(0.2f + index * 0.01f, 0.4f + index * 0.02f, 0.3f)
                    })
                    .ToArray()
            };

            var wireJson = JsonUtility.ToJson(snapshot);
            view.Apply(JsonUtility.FromJson<ServerSnapshotMessage>(wireJson));

            Assert.That(Vector3.Distance(view.RootPose.position, new Vector3(2f, 0f, -3f)),
                Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(view.RootPose.rotation, rootRotation), Is.LessThan(0.001f));
            Assert.That(view.IsMoving, Is.True);
            Assert.That(view.AnimationPhase, Is.EqualTo(0.25f).Within(0.0001f));

            for (var index = 0; index < snapshot.hitboxes.Length; index++)
            {
                var expected = snapshot.hitboxes[index];
                var actual = GameObject.Find(expected.name).transform;
                Assert.That(Vector3.Distance(actual.position, expected.center.ToUnityVector()),
                    Is.LessThan(0.0001f), expected.name + " position");
                Assert.That(Quaternion.Angle(actual.rotation, expected.rotation.ToUnityQuaternion()),
                    Is.LessThan(0.001f), expected.name + " rotation");
                Assert.That(Vector3.Distance(actual.localScale, expected.size.ToUnityVector()),
                    Is.LessThan(0.0001f), expected.name + " size");
            }
        }

        private static SnapshotVector3 Vector(float x, float y, float z)
        {
            return new SnapshotVector3 { x = x, y = y, z = z };
        }

        private static SnapshotQuaternion Rotation(Quaternion value)
        {
            return new SnapshotQuaternion { x = value.x, y = value.y, z = value.z, w = value.w };
        }
    }
}
