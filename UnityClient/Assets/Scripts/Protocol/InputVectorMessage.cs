using System;

namespace ServerAuthoritativeAnimation.Client.Protocol
{
    /// <summary>
    /// The complete client-to-server gameplay payload. Keep this DTO limited to x and z.
    /// </summary>
    [Serializable]
    public sealed class InputVectorMessage
    {
        public float x;
        public float z;

        public InputVectorMessage(float x, float z)
        {
            this.x = x;
            this.z = z;
        }
    }
}

