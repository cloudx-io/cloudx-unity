using System;
using UnityEngine;

namespace CloudX
{
    public class RevenueListenerProxy : AndroidJavaProxy
    {
        public event Action<CloudXAd> AdRevenuePaid;

        public RevenueListenerProxy()
            : base("io.cloudx.sdk.CloudXAdRevenueListener")
        {
            CloudXSdk.Log.LogDebug(() => $"RevenueListenerProxy created");
        }

        // Called when ad revenue is paid (Android thread)
        public void onAdRevenuePaid(AndroidJavaObject cloudXAdObject)
        {
            // Convert on Android thread to minimize main thread work
            var cloudXAd = cloudXAdObject.ToCloudXAd();
            CloudXSdk.Log.LogDebug(() => $"onAdRevenuePaid callback received: {cloudXAd}");

            AdRevenuePaid?.Invoke(cloudXAd);
        }
    }
}
