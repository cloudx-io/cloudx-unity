using UnityEngine;

namespace CloudX
{
    internal class BannerListenerProxy : AdListenerProxy
    {
        public BannerListenerProxy()
            : base("io.cloudx.sdk.CloudXAdViewListener")
        {
        }

        // CloudXAdViewListener-specific methods (not in base CloudXAdListener)
        public void onAdExpanded(AndroidJavaObject cloudXAdObject)
        {
            // Not currently used
        }

        public void onAdCollapsed(AndroidJavaObject cloudXAdObject)
        {
            // Not currently used
        }
    }
}
