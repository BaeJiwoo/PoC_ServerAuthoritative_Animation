using System.Numerics;
using ServerAuthoritativeAnimation.Core.Animation;
using ServerAuthoritativeAnimation.Core.Snapshots;

namespace ServerAuthoritativeAnimation.Core.Simulation;

/// <summary>
/// Builds world-space oriented hitboxes from the final local bone pose.
/// </summary>
public static class HitboxCalculator
{
    private static readonly Vector3 HeadCenter = new(0f, 2.15f, 0f);
    private static readonly Vector3 TorsoCenter = new(0f, 1.45f, 0f);
    private static readonly Vector3 LeftShoulder = new(-0.48f, 1.80f, 0f);
    private static readonly Vector3 RightShoulder = new(0.48f, 1.80f, 0f);
    private static readonly Vector3 LeftHip = new(-0.20f, 0.95f, 0f);
    private static readonly Vector3 RightHip = new(0.20f, 0.95f, 0f);

    public static HitboxSnapshot[] Calculate(Vector3 rootPosition, Quaternion rootRotation, WalkPose pose)
    {
        if (!IsFinite(rootPosition))
        {
            throw new ArgumentOutOfRangeException(nameof(rootPosition), "Root position must be finite.");
        }

        var rotationLengthSquared =
            ((double)rootRotation.X * rootRotation.X) +
            ((double)rootRotation.Y * rootRotation.Y) +
            ((double)rootRotation.Z * rootRotation.Z) +
            ((double)rootRotation.W * rootRotation.W);
        if (!IsFinite(rootRotation) ||
            !double.IsFinite(rotationLengthSquared) ||
            rotationLengthSquared <= 1e-12d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rootRotation),
                "Root rotation must be finite and non-zero.");
        }

        var inverseRotationLength = 1d / Math.Sqrt(rotationLengthSquared);
        rootRotation = new Quaternion(
            (float)(rootRotation.X * inverseRotationLength),
            (float)(rootRotation.Y * inverseRotationLength),
            (float)(rootRotation.Z * inverseRotationLength),
            (float)(rootRotation.W * inverseRotationLength));

        return
        [
            CreateStatic("Head", HeadCenter, new Vector3(0.42f, 0.42f, 0.42f), rootPosition, rootRotation),
            CreateStatic("Torso", TorsoCenter, new Vector3(0.68f, 0.90f, 0.36f), rootPosition, rootRotation),
            CreateLimb("LeftArm", LeftShoulder, 0.90f, 0.24f, pose.LeftArmPitchDegrees, rootPosition, rootRotation),
            CreateLimb("RightArm", RightShoulder, 0.90f, 0.24f, pose.RightArmPitchDegrees, rootPosition, rootRotation),
            CreateLimb("LeftLeg", LeftHip, 0.95f, 0.30f, pose.LeftLegPitchDegrees, rootPosition, rootRotation),
            CreateLimb("RightLeg", RightHip, 0.95f, 0.30f, pose.RightLegPitchDegrees, rootPosition, rootRotation),
        ];
    }

    private static HitboxSnapshot CreateStatic(
        string name,
        Vector3 localCenter,
        Vector3 size,
        Vector3 rootPosition,
        Quaternion rootRotation)
    {
        var worldCenter = rootPosition + Vector3.Transform(localCenter, rootRotation);
        return new HitboxSnapshot(
            name,
            Float3.FromVector3(worldCenter),
            Rotation.FromQuaternion(rootRotation),
            Float3.FromVector3(size));
    }

    private static HitboxSnapshot CreateLimb(
        string name,
        Vector3 localPivot,
        float length,
        float width,
        float pitchDegrees,
        Vector3 rootPosition,
        Quaternion rootRotation)
    {
        var pitchRadians = pitchDegrees * (MathF.PI / 180f);
        var localRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, pitchRadians);
        var localCenter = localPivot + Vector3.Transform(new Vector3(0f, -length * 0.5f, 0f), localRotation);
        var worldCenter = rootPosition + Vector3.Transform(localCenter, rootRotation);
        var worldRotation = Quaternion.Normalize(Quaternion.Concatenate(localRotation, rootRotation));

        return new HitboxSnapshot(
            name,
            Float3.FromVector3(worldCenter),
            Rotation.FromQuaternion(worldRotation),
            new Float3(width, length, width));
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static bool IsFinite(Quaternion value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) &&
        float.IsFinite(value.Z) && float.IsFinite(value.W);
}
