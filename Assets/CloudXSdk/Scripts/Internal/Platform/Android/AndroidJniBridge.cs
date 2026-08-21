using UnityEngine;

namespace CloudX
{
    internal static class AndroidJniBridge
    {
        public static readonly AndroidJavaClass JniBridgeClass =
            new("io.cloudx.sdk.jni_bridge.UnityBridge");
    }
}
