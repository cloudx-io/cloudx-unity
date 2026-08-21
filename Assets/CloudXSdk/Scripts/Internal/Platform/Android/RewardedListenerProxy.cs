using System;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using CloudX.Internal.Threading;

namespace CloudX
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal class RewardedListenerProxy : AdListenerProxy
    {
        public event Action<CloudXAd, CloudXReward> AdRewarded;

        public RewardedListenerProxy()
            : base("io.cloudx.sdk.CloudXRewardedListener")
        {
        }

        // Called when user is rewarded (Android thread)
        public void onUserRewarded(AndroidJavaObject cloudXAdObject, AndroidJavaObject cloudXRewardObject)
        {
            // Convert on Android thread to minimize main thread work
            var cloudXAd = cloudXAdObject.ToCloudXAd();
            var cloudXReward = cloudXRewardObject.ToCloudXReward();

            CloudXSdk.Log.LogDebug(() => $"onUserRewarded callback received: {cloudXAd}, reward={cloudXReward}");

            // Dispatch to Unity main thread
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                AdRewarded?.Invoke(cloudXAd, cloudXReward);
            });
        }
    }
}
