using System.Numerics;
using ServerAuthoritativeAnimation.Core.Animation;
using ServerAuthoritativeAnimation.Core.Simulation;
using ServerAuthoritativeAnimation.Core.Snapshots;

namespace ServerAuthoritativeAnimation.Core.Tests;

[TestFixture]
public sealed class HitboxCalculatorTests
{
    private const float Tolerance = 1e-4f;
    private static readonly string[] ExpectedNames =
    [
        "Head",
        "Torso",
        "LeftArm",
        "RightArm",
        "LeftLeg",
        "RightLeg",
    ];

    [Test]
    public void Calculate_ReturnsExactlyTheSixRequiredBodyParts()
    {
        var hitboxes = HitboxCalculator.Calculate(Vector3.Zero, Quaternion.Identity, WalkPose.Standing);

        Assert.Multiple(() =>
        {
            Assert.That(hitboxes, Has.Length.EqualTo(6));
            Assert.That(hitboxes.Select(hitbox => hitbox.Name), Is.EquivalentTo(ExpectedNames));
            Assert.That(hitboxes.Select(hitbox => hitbox.Name).Distinct().Count(), Is.EqualTo(6));
        });
    }

    [Test]
    public void Calculate_RejectsAZeroLengthRootRotation()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HitboxCalculator.Calculate(Vector3.Zero, default, WalkPose.Standing));
    }

    [Test]
    public void Calculate_AppliesRootTranslationAndYawToFinalWalkingPose()
    {
        var pose = WalkForwardAnimation.Sample(0.25f);
        var localHitboxes = HitboxCalculator.Calculate(Vector3.Zero, Quaternion.Identity, pose);
        var rootPosition = new Vector3(10f, 0.5f, -3f);
        var rootRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI * 0.5f);

        var worldHitboxes = HitboxCalculator.Calculate(rootPosition, rootRotation, pose);

        foreach (var local in localHitboxes)
        {
            var world = worldHitboxes.Single(hitbox => hitbox.Name == local.Name);
            var expectedCenter = rootPosition + Vector3.Transform(ToVector3(local.Center), rootRotation);
            var expectedRotation = Quaternion.Normalize(
                Quaternion.Concatenate(ToQuaternion(local.Rotation), rootRotation));

            AssertVector(ToVector3(world.Center), expectedCenter, local.Name + " center");
            AssertQuaternionEquivalent(ToQuaternion(world.Rotation), expectedRotation, local.Name + " rotation");
            Assert.That(world.Size, Is.EqualTo(local.Size), local.Name + " size");
        }
    }

    [Test]
    public void Calculate_WalkPoseMovesLimbsButNotHeadOrTorso()
    {
        var standing = HitboxCalculator.Calculate(Vector3.Zero, Quaternion.Identity, WalkPose.Standing);
        var walking = HitboxCalculator.Calculate(
            Vector3.Zero,
            Quaternion.Identity,
            WalkForwardAnimation.Sample(0.25f));

        foreach (var staticPart in new[] { "Head", "Torso" })
        {
            Assert.That(
                walking.Single(hitbox => hitbox.Name == staticPart),
                Is.EqualTo(standing.Single(hitbox => hitbox.Name == staticPart)));
        }

        foreach (var limb in new[] { "LeftArm", "RightArm", "LeftLeg", "RightLeg" })
        {
            var standingLimb = standing.Single(hitbox => hitbox.Name == limb);
            var walkingLimb = walking.Single(hitbox => hitbox.Name == limb);
            Assert.Multiple(() =>
            {
                Assert.That(Distance(standingLimb.Center, walkingLimb.Center), Is.GreaterThan(0.01f), limb + " center");
                Assert.That(
                    MathF.Abs(Quaternion.Dot(ToQuaternion(standingLimb.Rotation), ToQuaternion(walkingLimb.Rotation))),
                    Is.LessThan(0.9999f),
                    limb + " rotation");
            });
        }
    }

    [Test]
    public void CreateSnapshot_ContainsRootAndServerCalculatedHitboxes()
    {
        var simulation = new CharacterSimulation(moveSpeed: 2f);
        simulation.Tick(new MovementInput(1f, 0f), 0.25f);

        var snapshot = simulation.CreateSnapshot(123.5d);
        var expectedHitboxes = HitboxCalculator.Calculate(
            simulation.Position,
            simulation.RootRotation,
            WalkForwardAnimation.Sample(simulation.AnimationPhase));

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.ServerTime, Is.EqualTo(123.5d));
            Assert.That(snapshot.Root.Position, Is.EqualTo(Float3.FromVector3(simulation.Position)));
            Assert.That(snapshot.Root.Rotation, Is.EqualTo(Rotation.FromQuaternion(simulation.RootRotation)));
            Assert.That(snapshot.Root.YawDegrees, Is.EqualTo(90f).Within(Tolerance));
            Assert.That(snapshot.Hitboxes, Is.EqualTo(expectedHitboxes));
        });
    }

    private static Vector3 ToVector3(Float3 value) => new(value.X, value.Y, value.Z);

    private static Quaternion ToQuaternion(Rotation value) => new(value.X, value.Y, value.Z, value.W);

    private static float Distance(Float3 left, Float3 right) =>
        Vector3.Distance(ToVector3(left), ToVector3(right));

    private static void AssertVector(Vector3 actual, Vector3 expected, string message)
    {
        Assert.Multiple(() =>
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(Tolerance), message + " X");
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(Tolerance), message + " Y");
            Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(Tolerance), message + " Z");
        });
    }

    private static void AssertQuaternionEquivalent(Quaternion actual, Quaternion expected, string message)
    {
        var normalizedActual = Quaternion.Normalize(actual);
        var normalizedExpected = Quaternion.Normalize(expected);
        Assert.That(
            MathF.Abs(Quaternion.Dot(normalizedActual, normalizedExpected)),
            Is.EqualTo(1f).Within(Tolerance),
            message);
    }
}
