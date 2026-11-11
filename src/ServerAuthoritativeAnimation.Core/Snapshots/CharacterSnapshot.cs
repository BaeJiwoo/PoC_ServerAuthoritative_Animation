using System.Text.Json.Serialization;

namespace ServerAuthoritativeAnimation.Core.Snapshots;

public sealed record CharacterSnapshot(
    [property: JsonPropertyName("serverTime")] double ServerTime,
    [property: JsonPropertyName("root")] RootSnapshot Root,
    [property: JsonPropertyName("isMoving")] bool IsMoving,
    [property: JsonPropertyName("animationPhase")] float AnimationPhase,
    [property: JsonPropertyName("hitboxes")] HitboxSnapshot[] Hitboxes,
    [property: JsonPropertyName("skeletonId")] string SkeletonId = "",
    [property: JsonPropertyName("bones")] BoneSnapshot[]? Bones = null);

public sealed record BoneSnapshot(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("localPosition")] Float3 LocalPosition,
    [property: JsonPropertyName("localRotation")] Rotation LocalRotation,
    [property: JsonPropertyName("localScale")] Float3 LocalScale,
    [property: JsonPropertyName("worldPosition")] Float3 WorldPosition,
    [property: JsonPropertyName("worldRotation")] Rotation WorldRotation);

public sealed record RootSnapshot(
    [property: JsonPropertyName("position")] Float3 Position,
    [property: JsonPropertyName("rotation")] Rotation Rotation,
    [property: JsonPropertyName("yawDegrees")] float YawDegrees);

public sealed record HitboxSnapshot(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("center")] Float3 Center,
    [property: JsonPropertyName("rotation")] Rotation Rotation,
    [property: JsonPropertyName("size")] Float3 Size);
