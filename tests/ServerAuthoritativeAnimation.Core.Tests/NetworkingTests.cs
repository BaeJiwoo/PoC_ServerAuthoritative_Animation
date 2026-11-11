using System.Text;
using ServerAuthoritativeAnimation.Core;
using ServerAuthoritativeAnimation.Server.Networking;

namespace ServerAuthoritativeAnimation.Core.Tests;

[TestFixture]
public sealed class InputMessageParserTests
{
    [TestCase("{\"x\":0,\"z\":1}", 0f, 1f)]
    [TestCase("{\"z\":-1,\"x\":0}", 0f, -1f)]
    [TestCase("{\"x\":1,\"z\":0}", 1f, 0f)]
    [TestCase("{\"x\":-1,\"z\":0}", -1f, 0f)]
    public void TryParse_AcceptsExactlyTheTwoNumericComponents(
        string json,
        float expectedX,
        float expectedZ)
    {
        var parsed = InputMessageParser.TryParse(Bytes(json), out var input);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(input.X, Is.EqualTo(expectedX));
            Assert.That(input.Z, Is.EqualTo(expectedZ));
        });
    }

    [Test]
    public void TryParse_NormalizesAnOversizedDiagonal()
    {
        Assert.That(InputMessageParser.TryParse(Bytes("{\"x\":1,\"z\":1}"), out var input), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(input.Magnitude, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(input.X, Is.EqualTo(input.Z).Within(1e-5f));
        });
    }

    [TestCase("[]")]
    [TestCase("{\"x\":0}")]
    [TestCase("{\"x\":0,\"z\":1,\"animation\":\"walk\"}")]
    [TestCase("{\"x\":0,\"x\":1,\"z\":1}")]
    [TestCase("{\"X\":0,\"z\":1}")]
    [TestCase("{\"x\":\"0\",\"z\":1}")]
    [TestCase("{\"x\":1e999,\"z\":1}")]
    [TestCase("not json")]
    public void TryParse_RejectsAnythingOutsideTheInputVectorProtocol(string json)
    {
        Assert.That(InputMessageParser.TryParse(Bytes(json), out _), Is.False);
    }

    private static ReadOnlyMemory<byte> Bytes(string json) => Encoding.UTF8.GetBytes(json);
}

[TestFixture]
public sealed class LatestInputStateTests
{
    [Test]
    public void ReadActive_ReturnsZeroUntilTheFirstPacket()
    {
        var state = new LatestInputState(new ManualTimeProvider());

        Assert.That(state.ReadActive(TimeSpan.FromMilliseconds(500)), Is.EqualTo(MovementInput.Zero));
    }

    [Test]
    public void ReadActive_ExpiresAtTheConfiguredTimeout()
    {
        var time = new ManualTimeProvider();
        var state = new LatestInputState(time);
        var input = new MovementInput(0f, 1f);
        state.Update(input);

        time.Advance(TimeSpan.FromMilliseconds(499));
        Assert.That(state.ReadActive(TimeSpan.FromMilliseconds(500)), Is.EqualTo(input));

        time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.That(state.ReadActive(TimeSpan.FromMilliseconds(500)), Is.EqualTo(MovementInput.Zero));
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => timestamp;

        public void Advance(TimeSpan elapsed)
        {
            timestamp += elapsed.Ticks;
        }
    }
}
