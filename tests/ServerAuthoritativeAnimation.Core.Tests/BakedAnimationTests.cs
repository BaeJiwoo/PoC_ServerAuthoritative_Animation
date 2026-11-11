using System.Numerics;
using System.Text.Json;
using ServerAuthoritativeAnimation.Core.Animation;
using ServerAuthoritativeAnimation.Core.Simulation;

namespace ServerAuthoritativeAnimation.Core.Tests;

public sealed class BakedAnimationTests
{
    private static string ClipPath => Path.Combine(AppContext.BaseDirectory, "Animations", "BobWalkForward.json");

    [Test]
    public void RealFbxContainsFullSkeletonAndArticulatedWalkingMotion()
    {
        var clip = BakedAnimationClip.Load(ClipPath);
        var a = clip.Sample(0.1f, true, Vector3.Zero, Quaternion.Identity);
        var b = clip.Sample(0.6f, true, Vector3.Zero, Quaternion.Identity);
        Assert.That(clip.BoneCount, Is.GreaterThan(30));
        Assert.That(clip.DurationSeconds, Is.EqualTo(1f).Within(1e-4));
        Assert.That(a.Hitboxes, Has.Length.EqualTo(10));
        Assert.That(a.Hitboxes.Select(box => box.Name), Does.Contain("LeftForearm").And.Contain("LeftShin"));
        var kneeA = a.Bones.Single(bone => bone.Path.EndsWith(":LeftLeg"));
        var kneeB = b.Bones.Single(bone => bone.Path.EndsWith(":LeftLeg"));
        Assert.That(kneeA.LocalRotation, Is.Not.EqualTo(kneeB.LocalRotation));
        Assert.That(a.Hitboxes.Single(box => box.Name == "LeftShin").Center,
            Is.Not.EqualTo(b.Hitboxes.Single(box => box.Name == "LeftShin").Center));
    }

    [Test]
    public void ServerHierarchyMatchesUnitySampledWorldPositionsAndRotations()
    {
        var clip = BakedAnimationClip.Load(ClipPath);
        using var reference = JsonDocument.Parse(File.ReadAllText(Path.ChangeExtension(ClipPath, ".reference.json")));
        var root = reference.RootElement;
        var sample = clip.Sample(root.GetProperty("phase").GetSingle(), true, Vector3.Zero, Quaternion.Identity);
        foreach (var expected in root.GetProperty("bones").EnumerateArray())
        {
            var path = expected.GetProperty("path").GetString();
            var actual = sample.Bones.Single(bone => bone.Path == path);
            var position = expected.GetProperty("position");
            var rotation = expected.GetProperty("rotation");
            Assert.Multiple(() =>
            {
                Assert.That(actual.WorldPosition.X, Is.EqualTo(position.GetProperty("x").GetSingle()).Within(0.0001), path);
                Assert.That(actual.WorldPosition.Y, Is.EqualTo(position.GetProperty("y").GetSingle()).Within(0.0001), path);
                Assert.That(actual.WorldPosition.Z, Is.EqualTo(position.GetProperty("z").GetSingle()).Within(0.0001), path);
                var expectedQ = new Quaternion(rotation.GetProperty("x").GetSingle(), rotation.GetProperty("y").GetSingle(),
                    rotation.GetProperty("z").GetSingle(), rotation.GetProperty("w").GetSingle());
                var actualQ = new Quaternion(actual.WorldRotation.X, actual.WorldRotation.Y, actual.WorldRotation.Z, actual.WorldRotation.W);
                Assert.That(Math.Abs(Quaternion.Dot(expectedQ, actualQ)), Is.EqualTo(1f).Within(0.0001), path);
            });
        }
    }

    [TestCase(0f, -1f)]
    [TestCase(-1f, 0f)]
    [TestCase(1f, 1f)]
    public void RootYawTransformsEveryImportedBoneAndStoppingUsesReferencePose(float x, float z)
    {
        var clip = BakedAnimationClip.Load(ClipPath);
        var simulation = new CharacterSimulation(animation: clip);
        simulation.Tick(new MovementInput(x, z), 0.25f);
        var local = clip.Sample(simulation.AnimationPhase, true, Vector3.Zero, Quaternion.Identity);
        var snapshot = simulation.CreateSnapshot(1);
        for (var i = 0; i < local.Bones.Length; i++)
        {
            var p = local.Bones[i].WorldPosition;
            var expected = simulation.Position + Vector3.Transform(new Vector3(p.X, p.Y, p.Z), simulation.RootRotation);
            var actual = snapshot.Bones![i].WorldPosition;
            Assert.That(Vector3.Distance(expected, new Vector3(actual.X, actual.Y, actual.Z)), Is.LessThan(0.0001));
        }
        for (var i = 0; i < local.Hitboxes.Length; i++)
        {
            var box = local.Hitboxes[i];
            var expectedPosition = simulation.Position + Vector3.Transform(
                new Vector3(box.Center.X, box.Center.Y, box.Center.Z), simulation.RootRotation);
            var expectedRotation = simulation.RootRotation * new Quaternion(
                box.Rotation.X, box.Rotation.Y, box.Rotation.Z, box.Rotation.W);
            var actual = snapshot.Hitboxes[i];
            var actualRotation = new Quaternion(actual.Rotation.X, actual.Rotation.Y, actual.Rotation.Z, actual.Rotation.W);
            Assert.That(Vector3.Distance(expectedPosition, new Vector3(actual.Center.X, actual.Center.Y, actual.Center.Z)),
                Is.LessThan(0.0001), box.Name);
            Assert.That(MathF.Abs(Quaternion.Dot(expectedRotation, actualRotation)), Is.EqualTo(1f).Within(0.0001), box.Name);
        }
        simulation.Tick(MovementInput.Zero, 2f);
        var stopped = simulation.CreateSnapshot(3);
        Assert.That(simulation.AnimationPhase, Is.EqualTo(0.25f).Within(0.0001));
        Assert.That(stopped.Root, Is.EqualTo(snapshot.Root));
        Assert.That(stopped.Bones, Is.EqualTo(clip.Sample(0, false, simulation.Position, simulation.RootRotation).Bones));
    }

    [Test]
    public void ImportedLoopWrapsWithoutChangingTheSkeleton()
    {
        var clip = BakedAnimationClip.Load(ClipPath);
        Assert.That(clip.Sample(0, true, Vector3.Zero, Quaternion.Identity).Bones,
            Is.EqualTo(clip.Sample(1, true, Vector3.Zero, Quaternion.Identity).Bones));
        var nearEnd = clip.Sample(0.99999f, true, Vector3.Zero, Quaternion.Identity);
        var start = clip.Sample(0, true, Vector3.Zero, Quaternion.Identity);
        for (var i = 0; i < start.Bones.Length; i++)
        {
            var a = start.Bones[i].WorldPosition;
            var b = nearEnd.Bones[i].WorldPosition;
            Assert.That(Vector3.Distance(new Vector3(a.X, a.Y, a.Z), new Vector3(b.X, b.Y, b.Z)), Is.LessThan(0.001));
        }
    }
}
