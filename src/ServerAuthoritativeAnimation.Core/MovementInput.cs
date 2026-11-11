using System.Text.Json.Serialization;

namespace ServerAuthoritativeAnimation.Core;

/// <summary>
/// A movement request on Unity's horizontal X/Z plane.
/// </summary>
public readonly record struct MovementInput(
    [property: JsonPropertyName("x")] float X,
    [property: JsonPropertyName("z")] float Z)
{
    private const float MovementEpsilonSquared = 1e-10f;

    [JsonIgnore]
    public static MovementInput Zero => default;

    [JsonIgnore]
    public float MagnitudeSquared => (X * X) + (Z * Z);

    [JsonIgnore]
    public float Magnitude => MathF.Sqrt(MagnitudeSquared);

    [JsonIgnore]
    public bool IsMoving => MagnitudeSquared > MovementEpsilonSquared;

    /// <summary>
    /// Preserves input inside the unit circle and normalizes input outside it.
    /// WASD diagonals such as (1, 1) therefore have the same maximum speed as cardinal input.
    /// </summary>
    public MovementInput ClampToUnit()
    {
        EnsureFinite();

        var magnitudeSquared = PreciseMagnitudeSquared();
        if (magnitudeSquared <= MovementEpsilonSquared)
        {
            return Zero;
        }

        if (magnitudeSquared <= 1f)
        {
            return this;
        }

        var inverseMagnitude = 1d / Math.Sqrt(magnitudeSquared);
        return new MovementInput((float)(X * inverseMagnitude), (float)(Z * inverseMagnitude));
    }

    public MovementInput Direction()
    {
        EnsureFinite();

        var magnitudeSquared = PreciseMagnitudeSquared();
        if (magnitudeSquared <= MovementEpsilonSquared)
        {
            return Zero;
        }

        var inverseMagnitude = 1d / Math.Sqrt(magnitudeSquared);
        return new MovementInput((float)(X * inverseMagnitude), (float)(Z * inverseMagnitude));
    }

    private double PreciseMagnitudeSquared() => ((double)X * X) + ((double)Z * Z);

    private void EnsureFinite()
    {
        if (!float.IsFinite(X) || !float.IsFinite(Z))
        {
            throw new ArgumentOutOfRangeException(nameof(MovementInput), "Movement components must be finite.");
        }
    }
}
