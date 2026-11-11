using ServerAuthoritativeAnimation.Core.Animation;

namespace ServerAuthoritativeAnimation.Core.Tests;

[TestFixture]
public sealed class WalkForwardAnimationTests
{
    private const float Tolerance = 1e-5f;

    [TestCase(0f)]
    [TestCase(0.5f)]
    [TestCase(1f)]
    public void Sample_IsNeutralAtCenterCrossingsAndLoopBoundary(float phase)
    {
        AssertPose(WalkForwardAnimation.Sample(phase), WalkPose.Standing);
    }

    [Test]
    public void Sample_FirstStrideMovesLeftArmAndRightLegForward()
    {
        var pose = WalkForwardAnimation.Sample(0.25f);

        Assert.Multiple(() =>
        {
            Assert.That(pose.LeftArmPitchDegrees, Is.LessThan(0f));
            Assert.That(pose.RightLegPitchDegrees, Is.LessThan(0f));
            Assert.That(pose.RightArmPitchDegrees, Is.GreaterThan(0f));
            Assert.That(pose.LeftLegPitchDegrees, Is.GreaterThan(0f));
            Assert.That(pose.LeftArmPitchDegrees, Is.EqualTo(-pose.RightArmPitchDegrees).Within(Tolerance));
            Assert.That(pose.RightLegPitchDegrees, Is.EqualTo(-pose.LeftLegPitchDegrees).Within(Tolerance));
        });
    }

    [Test]
    public void Sample_SecondStrideMovesRightArmAndLeftLegForward()
    {
        var pose = WalkForwardAnimation.Sample(0.75f);

        Assert.Multiple(() =>
        {
            Assert.That(pose.RightArmPitchDegrees, Is.LessThan(0f));
            Assert.That(pose.LeftLegPitchDegrees, Is.LessThan(0f));
            Assert.That(pose.LeftArmPitchDegrees, Is.GreaterThan(0f));
            Assert.That(pose.RightLegPitchDegrees, Is.GreaterThan(0f));
            Assert.That(pose.LeftArmPitchDegrees, Is.EqualTo(-pose.RightArmPitchDegrees).Within(Tolerance));
            Assert.That(pose.RightLegPitchDegrees, Is.EqualTo(-pose.LeftLegPitchDegrees).Within(Tolerance));
        });
    }

    [TestCase(-0.75f, 0.25f)]
    [TestCase(0.25f, 1.25f)]
    [TestCase(0.75f, 2.75f)]
    public void Sample_RepeatsNaturallyEverySecond(float firstPhase, float secondPhase)
    {
        AssertPose(WalkForwardAnimation.Sample(firstPhase), WalkForwardAnimation.Sample(secondPhase));
    }

    private static void AssertPose(WalkPose actual, WalkPose expected)
    {
        Assert.Multiple(() =>
        {
            Assert.That(actual.LeftArmPitchDegrees, Is.EqualTo(expected.LeftArmPitchDegrees).Within(Tolerance));
            Assert.That(actual.RightArmPitchDegrees, Is.EqualTo(expected.RightArmPitchDegrees).Within(Tolerance));
            Assert.That(actual.LeftLegPitchDegrees, Is.EqualTo(expected.LeftLegPitchDegrees).Within(Tolerance));
            Assert.That(actual.RightLegPitchDegrees, Is.EqualTo(expected.RightLegPitchDegrees).Within(Tolerance));
        });
    }
}
