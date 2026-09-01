using System;
using UnityEngine;
using CloudX.Internal.Threading;

namespace CloudX
{
    public class RevenueListenerProxy : AndroidJavaProxy
    {
        public event Action<CloudXAd> AdRevenuePaid;

        private readonly bool _keepInBackground;

        /*
         * keepInBackground is the per-format default thread for revenue when
         * CloudXSdk.InvokeEventsOnUnityMainThread is unset: true for fullscreen formats, whose ad
         * Activity pauses the Unity player so main-thread delivery would wait until the ad closes;
         * false for banner/MREC, which ride the Unity main thread like every other callback.
         */
        public RevenueListenerProxy(bool keepInBackground)
            : base("io.cloudx.sdk.CloudXAdRevenueListener")
        {
            _keepInBackground = keepInBackground;
            CloudXSdk.Log.LogDebug(() => $"RevenueListenerProxy created, keepInBackground={keepInBackground}");
        }

        /*
         * Called when ad revenue is paid (Android thread). The publisher can override the
         * per-format default with CloudXSdk.InvokeEventsOnUnityMainThread. JniCallbackGuard and
         * CallbackDispatcher isolate conversion and handler exceptions so nothing propagates back
         * into the Java caller.
         */
        public void onAdRevenuePaid(AndroidJavaObject cloudXAdObject) => JniCallbackGuard.Run(OnAdRevenuePaidName, () =>
        {
            // Convert on Android thread to minimize main thread work
            var cloudXAd = cloudXAdObject.ToCloudXAd();
            CloudXSdk.Log.LogDebug(() => $"onAdRevenuePaid callback received: {cloudXAd}");

            CallbackDispatcher.Dispatch(OnAdRevenuePaidName, _keepInBackground,
                () => AdRevenuePaid?.Invoke(cloudXAd));
        });

        private const string OnAdRevenuePaidName = "CloudXAdRevenueListener.onAdRevenuePaid";
    }
}
