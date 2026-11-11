namespace ServerAuthoritativeAnimation.Core.Animation;

/// <summary>
/// The single one-second animation used for every movement direction.
/// </summary>
public static class WalkForwardAnimation
{
    public const float DurationSeconds = 1f;

    private static readonly Keyframe[] Keyframes =
    [
        new(0.00f, WalkPose.Standing),
        // Left arm and right leg are forward (+Z).
        new(0.25f, new WalkPose(-35f, 35f, 30f, -30f)),
        new(0.50f, WalkPose.Standing),
        // Right arm and left leg are forward (+Z).
        new(0.75f, new WalkPose(35f, -35f, -30f, 30f)),
        new(1.00f, WalkPose.Standing),
    ];

    public static WalkPose Sample(float phase)
    {
        if (!float.IsFinite(phase))
        {
            throw new ArgumentOutOfRangeException(nameof(phase), "Animation phase must be finite.");
        }

        var wrappedPhase = phase - MathF.Floor(phase);
        var rightIndex = 1;
        while (wrappedPhase > Keyframes[rightIndex].Phase)
        {
            rightIndex++;
        }

        var left = Keyframes[rightIndex - 1];
        var right = Keyframes[rightIndex];
        var segmentT = (wrappedPhase - left.Phase) / (right.Phase - left.Phase);
        var smoothT = segmentT * segmentT * (3f - (2f * segmentT));

        return new WalkPose(
            Lerp(left.Pose.LeftArmPitchDegrees, right.Pose.LeftArmPitchDegrees, smoothT),
            Lerp(left.Pose.RightArmPitchDegrees, right.Pose.RightArmPitchDegrees, smoothT),
            Lerp(left.Pose.LeftLegPitchDegrees, right.Pose.LeftLegPitchDegrees, smoothT),
            Lerp(left.Pose.RightLegPitchDegrees, right.Pose.RightLegPitchDegrees, smoothT));
    }

    private static float Lerp(float from, float to, float t) => from + ((to - from) * t);

    private readonly record struct Keyframe(float Phase, WalkPose Pose);
}
