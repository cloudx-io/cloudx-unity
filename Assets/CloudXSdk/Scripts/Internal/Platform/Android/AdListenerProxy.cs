using System;
using UnityEngine;
using CloudX.Internal.Threading;

namespace CloudX
{
    internal abstract class AdListenerProxy : AndroidJavaProxy
    {
        public event Action<CloudXAd> AdLoaded;
        public event Action<string, CloudXError> AdLoadFailed;
        public event Action<CloudXAd> AdDisplayed;
        public event Action<CloudXAd, CloudXError> AdDisplayFailed;
        public event Action<CloudXAd> AdHidden;
        public event Action<CloudXAd> AdClicked;

        protected AdListenerProxy(string javaInterface)
            : base(javaInterface)
        {
            CloudXSdk.Log.LogDebug(() => $"AdListenerProxy created for interface: {javaInterface}");
        }

        // Called when ad is loaded (Android thread)
        public void onAdLoaded(AndroidJavaObject cloudXAdObject)
        {
            // Convert on Android thread to minimize main thread work
            var cloudXAd = cloudXAdObject.ToCloudXAd();
            CloudXSdk.Log.LogDebug(() => $"onAdLoaded callback received: {cloudXAd}");

            // Dispatch to Unity main thread
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                AdLoaded?.Invoke(cloudXAd);
            });
        }

        // Called when ad fails to load (Android thread)
        public void onAdLoadFailed(string adUnitId, AndroidJavaObject cloudXErrorObject)
        {
            // Convert on Android thread to minimize main thread work
            var cloudXError = cloudXErrorObject.ToCloudXError();
            CloudXSdk.Log.LogDebug(() => $"onAdLoadFailed callback received, adUnitId={adUnitId}, error={cloudXError}");

            // Dispatch to Unity main thread
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                AdLoadFailed?.Invoke(adUnitId, cloudXError);
            });
        }

        // Called when ad is displayed (Android thread)
        public void onAdDisplayed(AndroidJavaObject cloudXAdObject)
        {
            // Convert on Android thread to minimize main thread work
            var cloudXAd = cloudXAdObject.ToCloudXAd();
            CloudXSdk.Log.LogDebug(() => $"onAdDisplayed callback received: {cloudXAd}");

            // Dispatch to Unity main thread
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                AdDisplayed?.Invoke(cloudXAd);
            });
        }

        // Called when ad fails to display (Android thread)
        public void onAdDisplayFailed(AndroidJavaObject cloudXAdObject, AndroidJavaObject cloudXErrorObject)
        {
            // Convert on Android thread to minimize main thread work
            var cloudXAd = cloudXAdObject.ToCloudXAd();
            var cloudXError = cloudXErrorObject.ToCloudXError();
            CloudXSdk.Log.LogDebug(() => $"onAdDisplayFailed callback received: {cloudXAd}, error={cloudXError}");

            // Dispatch to Unity main thread
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                AdDisplayFailed?.Invoke(cloudXAd, cloudXError);
            });
        }

        // Called when ad is hidden (closed) (Android thread)
        public void onAdHidden(AndroidJavaObject cloudXAdObject)
        {
            // Convert on Android thread to minimize main thread work
            var cloudXAd = cloudXAdObject.ToCloudXAd();
            CloudXSdk.Log.LogDebug(() => $"onAdHidden callback received: {cloudXAd}");

            // Dispatch to Unity main thread
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                AdHidden?.Invoke(cloudXAd);
            });
        }

        // Called when ad is clicked (Android thread)
        public void onAdClicked(AndroidJavaObject cloudXAdObject)
        {
            // Convert on Android thread to minimize main thread work
            var cloudXAd = cloudXAdObject.ToCloudXAd();
            CloudXSdk.Log.LogDebug(() => $"onAdClicked callback received: {cloudXAd}");

            // Dispatch to Unity main thread
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                AdClicked?.Invoke(cloudXAd);
            });
        }
    }
}
