using System.Numerics;
using ServerAuthoritativeAnimation.Core.Animation;
using ServerAuthoritativeAnimation.Core.Snapshots;

namespace ServerAuthoritativeAnimation.Core.Simulation;

/// <summary>
/// Deterministic, server-authoritative character movement and animation state.
/// </summary>
public sealed class CharacterSimulation
{
    public const float DefaultMoveSpeed = 3f;

    private Quaternion _rootRotation = Quaternion.Identity;
    private readonly BakedAnimationClip? animation;

    public CharacterSimulation(float moveSpeed = DefaultMoveSpeed, BakedAnimationClip? animation = null)
    {
        if (!float.IsFinite(moveSpeed) || moveSpeed <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(moveSpeed), "Move speed must be positive and finite.");
        }

        MoveSpeed = moveSpeed;
        this.animation = animation;
    }

    public float MoveSpeed { get; }

    public Vector3 Position { get; private set; }

    public Quaternion RootRotation => _rootRotation;

    public float YawDegrees { get; private set; }

    /// <summary>A normalized value in the half-open range [0, 1).</summary>
    public float AnimationPhase { get; private set; }

    public bool IsMoving { get; private set; }

    public void Tick(MovementInput input, float deltaTimeSeconds)
    {
        if (!float.IsFinite(deltaTimeSeconds) || deltaTimeSeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaTimeSeconds), "Delta time must be non-negative and finite.");
        }

        var clampedInput = input.ClampToUnit();
        IsMoving = clampedInput.IsMoving;
        if (!IsMoving)
        {
            return;
        }

        var direction = clampedInput.Direction();
        var yawRadians = MathF.Atan2(direction.X, direction.Z);
        YawDegrees = yawRadians * (180f / MathF.PI);
        _rootRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, yawRadians);

        Position += new Vector3(clampedInput.X, 0f, clampedInput.Z) * (MoveSpeed * deltaTimeSeconds);
        AnimationPhase = WrapPhase(AnimationPhase + (deltaTimeSeconds /
            (animation?.DurationSeconds ?? WalkForwardAnimation.DurationSeconds)));
    }

    public CharacterSnapshot CreateSnapshot(double serverTime)
    {
        if (!double.IsFinite(serverTime))
        {
            throw new ArgumentOutOfRangeException(nameof(serverTime), "Server time must be finite.");
        }

        // Phase is intentionally preserved while idle, but the visible pose returns to standing.
        HitboxSnapshot[] hitboxes;
        BoneSnapshot[]? bones = null;
        if (animation != null)
        {
            (bones, hitboxes) = animation.Sample(AnimationPhase, IsMoving, Position, _rootRotation);
        }
        else
        {
            // Retained only for the original minimal-core test fixture; the executable always loads FBX data.
            var pose = IsMoving ? WalkForwardAnimation.Sample(AnimationPhase) : WalkPose.Standing;
            hitboxes = HitboxCalculator.Calculate(Position, _rootRotation, pose);
        }

        return new CharacterSnapshot(
            serverTime,
            new RootSnapshot(
                Float3.FromVector3(Position),
                Rotation.FromQuaternion(_rootRotation),
                YawDegrees),
            IsMoving,
            AnimationPhase,
            hitboxes,
            animation?.Id ?? "",
            bones);
    }

    private static float WrapPhase(float phase) => phase - MathF.Floor(phase);
}
