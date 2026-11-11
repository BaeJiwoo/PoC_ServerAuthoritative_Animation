using System.Text.Json;
using ServerAuthoritativeAnimation.Core;

namespace ServerAuthoritativeAnimation.Server.Networking;

/// <summary>
/// Parses the complete client protocol. Only an object with one numeric "x" and one numeric "z" is accepted.
/// </summary>
public static class InputMessageParser
{
    public static bool TryParse(ReadOnlyMemory<byte> utf8Json, out MovementInput input)
    {
        input = MovementInput.Zero;

        try
        {
            using var document = JsonDocument.Parse(utf8Json);
            if (document.RootElement.ValueKind is not JsonValueKind.Object)
            {
                return false;
            }

            var hasX = false;
            var hasZ = false;
            var propertyCount = 0;
            var x = 0f;
            var z = 0f;

            foreach (var property in document.RootElement.EnumerateObject())
            {
                propertyCount++;
                if (property.Value.ValueKind is not JsonValueKind.Number ||
                    !property.Value.TryGetSingle(out var component) ||
                    !float.IsFinite(component))
                {
                    return false;
                }

                switch (property.Name)
                {
                    case "x" when !hasX:
                        x = component;
                        hasX = true;
                        break;
                    case "z" when !hasZ:
                        z = component;
                        hasZ = true;
                        break;
                    default:
                        // Unknown properties and duplicate x/z properties are both outside the protocol.
                        return false;
                }
            }

            if (propertyCount != 2 || !hasX || !hasZ)
            {
                return false;
            }

            input = new MovementInput(x, z).ClampToUnit();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
