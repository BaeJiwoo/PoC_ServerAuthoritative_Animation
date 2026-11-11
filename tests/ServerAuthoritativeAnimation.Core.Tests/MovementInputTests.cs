using System.Text.Json;
using ServerAuthoritativeAnimation.Core;

namespace ServerAuthoritativeAnimation.Core.Tests;

[TestFixture]
public sealed class MovementInputTests
{
    private const float Tolerance = 1e-5f;

    [TestCase(0f, 1f, 0f, 1f, TestName = "W_is_positive_Z")]
    [TestCase(0f, -1f, 0f, -1f, TestName = "S_is_negative_Z")]
    [TestCase(-1f, 0f, -1f, 0f, TestName = "A_is_negative_X")]
    [TestCase(1f, 0f, 1f, 0f, TestName = "D_is_positive_X")]
    public void ClampToUnit_PreservesCardinalInputs(float x, float z, float expectedX, float expectedZ)
    {
        var result = new MovementInput(x, z).ClampToUnit();

        Assert.Multiple(() =>
        {
            Assert.That(result.X, Is.EqualTo(expectedX).Within(Tolerance));
            Assert.That(result.Z, Is.EqualTo(expectedZ).Within(Tolerance));
            Assert.That(result.Magnitude, Is.EqualTo(1f).Within(Tolerance));
        });
    }

    [TestCase(-1f, 1f, TestName = "WA_is_normalized")]
    [TestCase(1f, 1f, TestName = "WD_is_normalized")]
    [TestCase(-1f, -1f, TestName = "SA_is_normalized")]
    [TestCase(1f, -1f, TestName = "SD_is_normalized")]
    public void ClampToUnit_NormalizesWasdDiagonals(float x, float z)
    {
        var result = new MovementInput(x, z).ClampToUnit();
        var inverseSqrtTwo = 1f / MathF.Sqrt(2f);

        Assert.Multiple(() =>
        {
            Assert.That(result.Magnitude, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(result.X, Is.EqualTo(MathF.Sign(x) * inverseSqrtTwo).Within(Tolerance));
            Assert.That(result.Z, Is.EqualTo(MathF.Sign(z) * inverseSqrtTwo).Within(Tolerance));
        });
    }

    [Test]
    public void ClampToUnit_LeavesZeroAsNotMoving()
    {
        var result = MovementInput.Zero.ClampToUnit();

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(MovementInput.Zero));
            Assert.That(result.IsMoving, Is.False);
        });
    }

    [Test]
    public void JsonContract_ContainsOnlyLowercaseXAndZ()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var input = new MovementInput(0.25f, -0.75f);

        var json = JsonSerializer.Serialize(input, options);
        using var document = JsonDocument.Parse(json);
        var properties = document.RootElement.EnumerateObject().ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(properties.Select(property => property.Name), Is.EquivalentTo(new[] { "x", "z" }));
            Assert.That(properties, Has.Length.EqualTo(2));
            Assert.That(document.RootElement.GetProperty("x").GetSingle(), Is.EqualTo(input.X));
            Assert.That(document.RootElement.GetProperty("z").GetSingle(), Is.EqualTo(input.Z));
        });

        Assert.That(JsonSerializer.Deserialize<MovementInput>(json, options), Is.EqualTo(input));
    }
}
