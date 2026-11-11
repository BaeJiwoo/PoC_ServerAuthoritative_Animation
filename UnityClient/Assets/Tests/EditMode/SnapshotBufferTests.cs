using NUnit.Framework;
using ServerAuthoritativeAnimation.Client.Presentation;
using ServerAuthoritativeAnimation.Client.Protocol;

namespace ServerAuthoritativeAnimation.Client.Tests
{
    public sealed class SnapshotBufferTests
    {
        private static ServerSnapshotMessage Snapshot(double time, string rig = "bob") =>
            new ServerSnapshotMessage { serverTime = time, root = new RootSnapshot(), skeletonId = rig };

        [Test]
        public void DelayedTimelineAdvancesBetweenPacketsAndClampsDuringOutage()
        {
            var buffer = new SnapshotBuffer();
            for (var i = 0; i < 5; i++) buffer.Add(Snapshot(1000 + i * .05), 10 + i * .05);
            Assert.That(buffer.Sample(10.225, out var a, out var b, out var t), Is.True);
            Assert.That(a.serverTime, Is.EqualTo(1000.1).Within(.00001));
            Assert.That(b.serverTime, Is.EqualTo(1000.15).Within(.00001));
            Assert.That(t, Is.EqualTo(.5f).Within(.0001));
            buffer.Sample(10.235, out _, out _, out var later);
            Assert.That(later, Is.GreaterThan(t));
            buffer.Sample(20, out _, out b, out t);
            Assert.That(b.serverTime, Is.EqualTo(1000.2).Within(.00001));
            Assert.That(t, Is.EqualTo(1));
        }

        [Test]
        public void LateArrivalDoesNotRewindTheTimeline()
        {
            var buffer = new SnapshotBuffer();
            buffer.Add(Snapshot(100), 0);
            buffer.Add(Snapshot(100.05), .05);
            buffer.Add(Snapshot(100.1), .1);
            buffer.Sample(.175, out _, out _, out var before);
            buffer.Add(Snapshot(100.15), .2);
            buffer.Add(Snapshot(100.05), .201); // stale/duplicate is ignored
            buffer.Sample(.175, out _, out _, out var after);
            Assert.That(after, Is.EqualTo(before).Within(.0001));
        }

        [Test]
        public void ReconnectAndRigChangesDiscardOldPoses()
        {
            var buffer = new SnapshotBuffer();
            buffer.Add(Snapshot(100), 0);
            buffer.Clear();
            Assert.That(buffer.Sample(1, out _, out _, out _), Is.False);
            buffer.Add(Snapshot(1), 1);
            buffer.Add(Snapshot(1.05, "new-rig"), 1.05);
            buffer.Sample(1.05, out var a, out var b, out _);
            Assert.That(a.skeletonId, Is.EqualTo("new-rig"));
            Assert.That(b, Is.SameAs(a));
            buffer.Add(Snapshot(5, "new-rig"), 5);
            buffer.Sample(5, out a, out b, out _);
            Assert.That(a.serverTime, Is.EqualTo(5));
            Assert.That(b, Is.SameAs(a));
        }
    }
}
