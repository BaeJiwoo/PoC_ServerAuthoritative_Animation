using System;
using UnityEngine;

namespace ServerAuthoritativeAnimation.Client.Protocol
{
    [Serializable]
    public sealed class ServerSnapshotMessage
    {
        public double serverTime;
        public RootSnapshot root;
        public bool isMoving;
        public float animationPhase;
        public HitboxSnapshot[] hitboxes;
        public string skeletonId;
        public BoneSnapshot[] bones;
    }

    [Serializable]
    public sealed class BoneSnapshot
    {
        public string path;
        public SnapshotVector3 localPosition;
        public SnapshotQuaternion localRotation;
        public SnapshotVector3 localScale;
        public SnapshotVector3 worldPosition;
        public SnapshotQuaternion worldRotation;
    }

    [Serializable]
    public sealed class RootSnapshot
    {
        public SnapshotVector3 position;
        public SnapshotQuaternion rotation;
        public float yawDegrees;
    }

    [Serializable]
    public sealed class HitboxSnapshot
    {
        public string name;
        public SnapshotVector3 center;
        public SnapshotQuaternion rotation;
        public SnapshotVector3 size;
    }

    [Serializable]
    public struct SnapshotVector3
    {
        public float x;
        public float y;
        public float z;

        public Vector3 ToUnityVector()
        {
            return new Vector3(x, y, z);
        }
    }

    [Serializable]
    public struct SnapshotQuaternion
    {
        public float x;
        public float y;
        public float z;
        public float w;

        public Quaternion ToUnityQuaternion()
        {
            var value = new Quaternion(x, y, z, w);
            var lengthSquared = x * x + y * y + z * z + w * w;
            if (lengthSquared < 0.000001f)
            {
                return Quaternion.identity;
            }

            return Quaternion.Normalize(value);
        }
    }
}

