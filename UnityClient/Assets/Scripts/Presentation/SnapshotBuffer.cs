using System;
using System.Collections.Generic;
using ServerAuthoritativeAnimation.Client.Protocol;

namespace ServerAuthoritativeAnimation.Client.Presentation
{
    /// <summary>Renders a delayed server timeline; never extrapolates past received poses.</summary>
    public sealed class SnapshotBuffer
    {
        public const double DelaySeconds = 0.1;
        private readonly List<ServerSnapshotMessage> snapshots = new List<ServerSnapshotMessage>();
        private double clockOffset;
        private double lastRenderTime = double.NegativeInfinity;

        public void Clear()
        {
            snapshots.Clear();
            lastRenderTime = double.NegativeInfinity;
        }

        public void Add(ServerSnapshotMessage snapshot, double receivedAt)
        {
            if (snapshot == null || snapshot.root == null || double.IsNaN(snapshot.serverTime) ||
                double.IsInfinity(snapshot.serverTime)) return;
            if (snapshots.Count > 0)
            {
                var latest = snapshots[snapshots.Count - 1];
                if (snapshot.serverTime <= latest.serverTime) return;
                // A long outage or changed rig starts a new presentation timeline.
                if (snapshot.serverTime - latest.serverTime > 1 || snapshot.skeletonId != latest.skeletonId)
                    Clear();
            }
            var offset = snapshot.serverTime - receivedAt;
            clockOffset = snapshots.Count == 0 ? offset : Math.Max(clockOffset, offset);
            snapshots.Add(snapshot);
            if (snapshots.Count > 32) snapshots.RemoveAt(0);
        }

        public bool Sample(double now, out ServerSnapshotMessage from, out ServerSnapshotMessage to, out float t)
        {
            from = to = null;
            t = 0;
            if (snapshots.Count == 0) return false;
            var renderTime = Math.Max(lastRenderTime, now + clockOffset - DelaySeconds);
            lastRenderTime = renderTime;
            while (snapshots.Count > 2 && snapshots[1].serverTime <= renderTime)
                snapshots.RemoveAt(0);
            from = snapshots[0];
            to = snapshots[snapshots.Count > 1 ? 1 : 0];
            if (to.serverTime > from.serverTime)
                t = (float)Math.Clamp((renderTime - from.serverTime) / (to.serverTime - from.serverTime), 0, 1);
            return true;
        }
    }
}
