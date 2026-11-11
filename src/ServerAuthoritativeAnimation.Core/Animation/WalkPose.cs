namespace ServerAuthoritativeAnimation.Core.Animation;

/// <summary>
/// Local joint pitch angles. Negative X rotation moves a downward limb toward local +Z.
/// </summary>
public readonly record struct WalkPose(
    float LeftArmPitchDegrees,
    float RightArmPitchDegrees,
    float LeftLegPitchDegrees,
    float RightLegPitchDegrees)
{
    public static WalkPose Standing => default;
}
