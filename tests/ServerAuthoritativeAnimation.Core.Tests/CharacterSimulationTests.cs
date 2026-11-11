using System.Numerics;
using ServerAuthoritativeAnimation.Core.Animation;
using ServerAuthoritativeAnimation.Core.Simulation;
using ServerAuthoritativeAnimation.Core.Snapshots;

namespace ServerAuthoritativeAnimation.Core.Tests;

[TestFixture]
public sealed class CharacterSimulationTests
{
    private const float Tolerance = 1e-4f;

    [TestCase(0f, 1f, 0f, TestName = "W_faces_forward")]
    [TestCase(1f, 1f, 45f, TestName = "WD_faces_forward_right")]
    [TestCase(1f, 0f, 90f, TestName = "D_faces_right")]
    [TestCase(1f, -1f, 135f, TestName = "SD_faces_back_right")]
    [TestCase(0f, -1f, 180f, TestName = "S_faces_back")]
    [TestCase(-1f, -1f, -135f, TestName = "SA_faces_back_left")]
    [TestCase(-1f, 0f, -90f, TestName = "A_faces_left")]
    [TestCase(-1f, 1f, -45f, TestName = "WA_faces_forward_left")]
    public void Tick_ComputesExpectedYawForAllWasdDirections(float x, float z, float expectedYaw)
    {
        var simulation = new CharacterSimulation();

        simulation.Tick(new MovementInput(x, z), 0f);

        Assert.Multiple(() =>
        {
            Assert.That(AngleDelta(simulation.YawDegrees, expectedYaw), Is.EqualTo(0f).Within(Tolerance));
            Assert.That(simulation.IsMoving, Is.True);

            var actualForward = Vector3.Transform(Vector3.UnitZ, simulation.RootRotation);
            var expectedForward = Vector3.Normalize(new Vector3(x, 0f, z));
            AssertVector(actualForward, expectedForward);
        });
    }

    [Test]
    public void Tick_MovesForwardAtConfiguredSpeed()
    {
        var simulation = new CharacterSimulation(moveSpeed: 2.5f);

        simulation.Tick(new MovementInput(1f, 0f), 0.4f);

        Assert.Multiple(() =>
        {
            AssertVector(simulation.Position, new Vector3(1f, 0f, 0f));
            Assert.That(simulation.YawDegrees, Is.EqualTo(90f).Within(Tolerance));
        });
    }

    [Test]
    public void Tick_NormalizedDiagonalHasSameSpeedAsCardinalInput()
    {
        var cardinal = new CharacterSimulation(moveSpeed: 4f);
        var diagonal = new CharacterSimulation(moveSpeed: 4f);

        cardinal.Tick(new MovementInput(0f, 1f), 0.5f);
        diagonal.Tick(new MovementInput(1f, 1f), 0.5f);

        Assert.Multiple(() =>
        {
            Assert.That(cardinal.Position.Length(), Is.EqualTo(2f).Within(Tolerance));
            Assert.That(diagonal.Position.Length(), Is.EqualTo(2f).Within(Tolerance));
            Assert.That(diagonal.Position.X, Is.EqualTo(MathF.Sqrt(2f)).Within(Tolerance));
            Assert.That(diagonal.Position.Z, Is.EqualTo(MathF.Sqrt(2f)).Within(Tolerance));
        });
    }

    [Test]
    public void Tick_ChangesFacingImmediatelyWithoutInterpolation()
    {
        var simulation = new CharacterSimulation();
        simulation.Tick(new MovementInput(0f, 1f), 0.2f);

        simulation.Tick(new MovementInput(-1f, 0f), 0f);

        Assert.Multiple(() =>
        {
            Assert.That(simulation.YawDegrees, Is.EqualTo(-90f).Within(Tolerance));
            AssertVector(Vector3.Transform(Vector3.UnitZ, simulation.RootRotation), -Vector3.UnitX);
            Assert.That(simulation.AnimationPhase, Is.EqualTo(0.2f).Within(Tolerance));
        });
    }

    [Test]
    public void Tick_AdvancesAndWrapsOneSecondAnimationPhaseOnlyWhileMoving()
    {
        Assert.That(WalkForwardAnimation.DurationSeconds, Is.EqualTo(1f));
        var simulation = new CharacterSimulation();

        simulation.Tick(new MovementInput(0f, 1f), 0.75f);
        Assert.That(simulation.AnimationPhase, Is.EqualTo(0.75f).Within(Tolerance));

        simulation.Tick(new MovementInput(0f, 1f), 0.5f);
        Assert.That(simulation.AnimationPhase, Is.EqualTo(0.25f).Within(Tolerance));

        simulation.Tick(new MovementInput(0f, 1f), 2.1f);
        Assert.That(simulation.AnimationPhase, Is.EqualTo(0.35f).Within(Tolerance));
    }

    [Test]
    public void Tick_WithNoInputFreezesPositionYawAndPhaseAndReturnsNeutralPose()
    {
        var simulation = new CharacterSimulation();
        simulation.Tick(new MovementInput(0f, 1f), 0.25f);
        var movingSnapshot = simulation.CreateSnapshot(1d);
        var positionBeforeRelease = simulation.Position;
        var yawBeforeRelease = simulation.YawDegrees;
        var phaseBeforeRelease = simulation.AnimationPhase;

        simulation.Tick(MovementInput.Zero, 10f);
        var idleSnapshot = simulation.CreateSnapshot(11d);
        var expectedNeutral = HitboxCalculator.Calculate(
            positionBeforeRelease,
            simulation.RootRotation,
            WalkPose.Standing);

        Assert.Multiple(() =>
        {
            Assert.That(simulation.IsMoving, Is.False);
            AssertVector(simulation.Position, positionBeforeRelease);
            Assert.That(simulation.YawDegrees, Is.EqualTo(yawBeforeRelease));
            Assert.That(simulation.AnimationPhase, Is.EqualTo(phaseBeforeRelease));
            Assert.That(idleSnapshot.IsMoving, Is.False);
            Assert.That(idleSnapshot.AnimationPhase, Is.EqualTo(phaseBeforeRelease));
        });

        AssertHitboxesEqual(idleSnapshot.Hitboxes, expectedNeutral);

        var movingLeftArm = movingSnapshot.Hitboxes.Single(hitbox => hitbox.Name == "LeftArm");
        var idleLeftArm = idleSnapshot.Hitboxes.Single(hitbox => hitbox.Name == "LeftArm");
        Assert.That(Distance(movingLeftArm.Center, idleLeftArm.Center), Is.GreaterThan(0.01f));
    }

    private static float AngleDelta(float actual, float expected) =>
        ((actual - expected + 540f) % 360f) - 180f;

    private static void AssertVector(Vector3 actual, Vector3 expected)
    {
        Assert.Multiple(() =>
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(Tolerance));
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(Tolerance));
            Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(Tolerance));
        });
    }

    private static void AssertHitboxesEqual(
        IReadOnlyList<HitboxSnapshot> actual,
        IReadOnlyList<HitboxSnapshot> expected)
    {
        Assert.That(actual, Has.Count.EqualTo(expected.Count));
        for (var index = 0; index < actual.Count; index++)
        {
            Assert.That(actual[index], Is.EqualTo(expected[index]));
        }
    }

    private static float Distance(Float3 left, Float3 right)
    {
        var x = left.X - right.X;
        var y = left.Y - right.Y;
        var z = left.Z - right.Z;
        return MathF.Sqrt((x * x) + (y * y) + (z * z));
    }
}
