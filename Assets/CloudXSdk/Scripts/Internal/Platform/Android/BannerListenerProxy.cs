using UnityEngine;
using CloudX.Internal.Threading;

namespace CloudX
{
    internal class BannerListenerProxy : AdListenerProxy
    {
        public BannerListenerProxy()
            : base("io.cloudx.sdk.CloudXAdViewListener")
        {
        }

        /*
         * CloudXAdViewListener-specific methods (not in base CloudXAdListener). Empty today, but
         * guarded already so a future implementation cannot leak an exception into the Java caller.
         */
        public void onAdExpanded(AndroidJavaObject cloudXAdObject) => JniCallbackGuard.Run("CloudXAdViewListener.onAdExpanded", () =>
        {
            // Not currently used
        });

        public void onAdCollapsed(AndroidJavaObject cloudXAdObject) => JniCallbackGuard.Run("CloudXAdViewListener.onAdCollapsed", () =>
        {
            // Not currently used
        });
    }
}
