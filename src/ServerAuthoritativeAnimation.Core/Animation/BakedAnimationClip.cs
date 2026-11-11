using System.Numerics;
using System.Text.Json;
using ServerAuthoritativeAnimation.Core.Snapshots;

namespace ServerAuthoritativeAnimation.Core.Animation;

/// <summary>Unity evaluates an FBX once during authoring; the server evaluates only these sampled transforms.</summary>
public sealed class BakedAnimationClip
{
    private readonly BakedClipData data;

    public BakedAnimationClip(BakedClipData data)
    {
        this.data = data ?? throw new ArgumentNullException(nameof(data));
        Validate();
    }

    public string Id => data.Id;
    public string Name => data.Name;
    public float DurationSeconds => data.Duration;
    public int BoneCount => data.Bones.Length;

    public static BakedAnimationClip Load(string path)
    {
        using var stream = File.OpenRead(path);
        return new BakedAnimationClip(JsonSerializer.Deserialize<BakedClipData>(
            stream, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException("Animation JSON is empty."));
    }

    public (BoneSnapshot[] Bones, HitboxSnapshot[] Hitboxes) Sample(
        float phase, bool moving, Vector3 rootPosition, Quaternion rootRotation)
    {
        if (!float.IsFinite(phase))
            throw new ArgumentOutOfRangeException(nameof(phase));

        var frame = (phase - MathF.Floor(phase)) * data.Frames.Length;
        var first = Math.Min((int)frame, data.Frames.Length - 1);
        var second = (first + 1) % data.Frames.Length;
        var t = frame - first;
        var result = new BoneSnapshot[data.Bones.Length];
        var worldPositions = new Vector3[result.Length];
        var worldRotations = new Quaternion[result.Length];
        var worldScales = new Vector3[result.Length];

        for (var i = 0; i < result.Length; i++)
        {
            var a = moving ? data.Frames[first].Pose[i] : data.ReferencePose[i];
            var b = moving ? data.Frames[second].Pose[i] : a;
            var position = Vector3.Lerp(Vector(a.Position), Vector(b.Position), t);
            var rotation = Quaternion.Slerp(Quat(a.Rotation), Quat(b.Rotation), t);
            var scale = Vector3.Lerp(Vector(a.Scale), Vector(b.Scale), t);
            var parent = data.Bones[i].Parent;
            var parentPosition = parent < 0 ? rootPosition : worldPositions[parent];
            var parentRotation = parent < 0 ? rootRotation : worldRotations[parent];
            var parentScale = parent < 0 ? Vector3.One : worldScales[parent];
            worldPositions[i] = parentPosition + Vector3.Transform(position * parentScale, parentRotation);
            worldRotations[i] = Quaternion.Normalize(parentRotation * rotation);
            worldScales[i] = parentScale * scale;
            result[i] = new BoneSnapshot(data.Bones[i].Path,
                Float3.FromVector3(position), Rotation.FromQuaternion(rotation), Float3.FromVector3(scale),
                Float3.FromVector3(worldPositions[i]), Rotation.FromQuaternion(worldRotations[i]));
        }

        var hitboxes = new HitboxSnapshot[data.Hitboxes.Length];
        for (var i = 0; i < hitboxes.Length; i++)
        {
            var binding = data.Hitboxes[i];
            var start = worldPositions[binding.StartBone];
            var rotation = worldRotations[binding.StartBone];
            Vector3 center;
            Vector3 size;
            if (binding.EndBone >= 0)
            {
                var end = worldPositions[binding.EndBone];
                var delta = end - start;
                center = (start + end) * 0.5f;
                size = new Vector3(binding.Width, Math.Max(delta.Length(), 0.01f), binding.Depth);
                if (delta.LengthSquared() > 1e-10f)
                    rotation = AlignYAxis(rotation, Vector3.Normalize(delta));
            }
            else
            {
                center = start + Vector3.Transform(Vector(binding.Offset), rotation);
                size = Vector(binding.Size);
            }
            hitboxes[i] = new HitboxSnapshot(binding.Name, Float3.FromVector3(center),
                Rotation.FromQuaternion(rotation), Float3.FromVector3(size));
        }
        return (result, hitboxes);
    }

    private void Validate()
    {
        if (data.SchemaVersion != 1 || string.IsNullOrWhiteSpace(data.Id) ||
            !float.IsFinite(data.Duration) || data.Duration <= 0f ||
            data.Bones is not { Length: > 0 and <= 512 } ||
            data.Frames is not { Length: >= 2 } || data.ReferencePose?.Length != data.Bones.Length ||
            data.Hitboxes is not { Length: > 0 })
            throw new InvalidDataException("Invalid baked animation header, bones, frames or reference pose.");
        var paths = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < data.Bones.Length; i++)
        {
            var bone = data.Bones[i];
            if (bone == null || string.IsNullOrWhiteSpace(bone.Path) || !paths.Add(bone.Path) ||
                bone.Parent < -1 || bone.Parent >= i)
                throw new InvalidDataException("Bones must have unique paths and parent-before-child ordering.");
        }
        CheckPose(data.ReferencePose);
        foreach (var frame in data.Frames)
            CheckPose(frame?.Pose);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var binding in data.Hitboxes)
        {
            if (binding == null || string.IsNullOrWhiteSpace(binding.Name) || !names.Add(binding.Name) ||
                binding.StartBone < 0 || binding.StartBone >= data.Bones.Length ||
                binding.EndBone < -1 || binding.EndBone >= data.Bones.Length ||
                !float.IsFinite(binding.Width) || binding.Width <= 0 ||
                !float.IsFinite(binding.Depth) || binding.Depth <= 0 || !Finite(binding.Offset) ||
                !Finite(binding.Size) || (binding.EndBone < 0 &&
                (binding.Size.X <= 0 || binding.Size.Y <= 0 || binding.Size.Z <= 0)))
                throw new InvalidDataException("Invalid bone hitbox binding.");
        }
    }

    private void CheckPose(LocalBonePose[]? pose)
    {
        if (pose == null || pose.Length != data.Bones.Length)
            throw new InvalidDataException("Every frame must contain the complete skeleton.");
        foreach (var bone in pose)
        {
            if (bone == null || !Finite(bone.Position) || !Finite(bone.Scale) ||
                bone.Scale.X <= 0 || bone.Scale.Y <= 0 || bone.Scale.Z <= 0 ||
                MathF.Abs(bone.Scale.X - bone.Scale.Y) > 1e-4f ||
                MathF.Abs(bone.Scale.X - bone.Scale.Z) > 1e-4f)
                throw new InvalidDataException("Bone transforms require finite, positive uniform scale.");
            var q = new Quaternion(bone.Rotation.X, bone.Rotation.Y, bone.Rotation.Z, bone.Rotation.W);
            if (!float.IsFinite(q.LengthSquared()) || q.LengthSquared() < 1e-10f)
                throw new InvalidDataException("Bone rotation must be finite and non-zero.");
        }
    }

    private static bool Finite(Float3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    private static Vector3 Vector(Float3 value) => new(value.X, value.Y, value.Z);
    private static Quaternion Quat(Rotation value) => Quaternion.Normalize(new(value.X, value.Y, value.Z, value.W));
    private static Quaternion AlignYAxis(Quaternion boneRotation, Vector3 direction)
    {
        // Keep bone twist/root yaw while aligning the box's long axis to the actual endpoints.
        var up = Vector3.Transform(Vector3.UnitY, boneRotation);
        var dot = Math.Clamp(Vector3.Dot(up, direction), -1f, 1f);
        var correction = dot < -0.99999f
            ? Quaternion.CreateFromAxisAngle(Vector3.Transform(Vector3.UnitX, boneRotation), MathF.PI)
            : Quaternion.Normalize(new Quaternion(Vector3.Cross(up, direction), 1f + dot));
        return Quaternion.Normalize(correction * boneRotation);
    }
}

public sealed class BakedClipData
{
    public int SchemaVersion { get; set; } = 1;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public float Duration { get; set; }
    public BoneDefinition[] Bones { get; set; } = [];
    public LocalBonePose[] ReferencePose { get; set; } = [];
    public BakedFrame[] Frames { get; set; } = [];
    public BoneHitboxBinding[] Hitboxes { get; set; } = [];
}

public sealed class BoneDefinition
{
    public string Path { get; set; } = "";
    public int Parent { get; set; }
}

public sealed class LocalBonePose
{
    public Float3 Position { get; set; }
    public Rotation Rotation { get; set; }
    public Float3 Scale { get; set; } = new(1, 1, 1);
}

public sealed class BakedFrame
{
    public LocalBonePose[] Pose { get; set; } = [];
}

public sealed class BoneHitboxBinding
{
    public string Name { get; set; } = "";
    public int StartBone { get; set; }
    public int EndBone { get; set; } = -1;
    public float Width { get; set; } = 0.15f;
    public float Depth { get; set; } = 0.15f;
    public Float3 Offset { get; set; }
    public Float3 Size { get; set; }
}
