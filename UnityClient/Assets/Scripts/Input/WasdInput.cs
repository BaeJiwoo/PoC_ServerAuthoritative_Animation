using UnityEngine;

namespace ServerAuthoritativeAnimation.Client.Input
{
    public static class WasdInput
    {
        public static Vector2 ReadCurrent()
        {
            return FromButtons(
                UnityEngine.Input.GetKey(KeyCode.W),
                UnityEngine.Input.GetKey(KeyCode.S),
                UnityEngine.Input.GetKey(KeyCode.A),
                UnityEngine.Input.GetKey(KeyCode.D));
        }

        public static Vector2 FromButtons(bool w, bool s, bool a, bool d)
        {
            var x = (d ? 1f : 0f) - (a ? 1f : 0f);
            var z = (w ? 1f : 0f) - (s ? 1f : 0f);
            var input = new Vector2(x, z);

            return input.sqrMagnitude > 1f ? input.normalized : input;
        }
    }
}

