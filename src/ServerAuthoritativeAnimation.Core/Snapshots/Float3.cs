using System.Numerics;
using System.Text.Json.Serialization;

namespace ServerAuthoritativeAnimation.Core.Snapshots;

public readonly record struct Float3
{
    [JsonConstructor]
    public Float3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    [JsonPropertyName("x")]
    public float X { get; }

    [JsonPropertyName("y")]
    public float Y { get; }

    [JsonPropertyName("z")]
    public float Z { get; }

    public static Float3 FromVector3(Vector3 value) => new(value.X, value.Y, value.Z);
}
