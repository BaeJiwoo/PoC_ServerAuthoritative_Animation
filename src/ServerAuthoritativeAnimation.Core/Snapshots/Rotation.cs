using System.Numerics;
using System.Text.Json.Serialization;

namespace ServerAuthoritativeAnimation.Core.Snapshots;

public readonly record struct Rotation
{
    [JsonConstructor]
    public Rotation(float x, float y, float z, float w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    [JsonPropertyName("x")]
    public float X { get; }

    [JsonPropertyName("y")]
    public float Y { get; }

    [JsonPropertyName("z")]
    public float Z { get; }

    [JsonPropertyName("w")]
    public float W { get; }

    public static Rotation FromQuaternion(Quaternion value) => new(value.X, value.Y, value.Z, value.W);
}
