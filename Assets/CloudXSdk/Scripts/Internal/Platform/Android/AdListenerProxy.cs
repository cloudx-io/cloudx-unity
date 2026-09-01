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

        /*
         * Log labels passed to JniCallbackGuard and CallbackDispatcher so their error lines name
         * the exact native callback that was in flight, e.g. "InterstitialListener.onAdLoaded".
         * The "<JavaListener>.<javaMethod>" form matches how the native SDK logs the callback, so
         * a Unity-side line can be correlated with native logcat output. Built once here because
         * the listener interface is fixed at construction and the labels are used on every call.
         */
        private readonly string _onAdLoaded;
        private readonly string _onAdLoadFailed;
        private readonly string _onAdDisplayed;
        private readonly string _onAdDisplayFailed;
        private readonly string _onAdHidden;
        private readonly string _onAdClicked;

        protected AdListenerProxy(string javaInterface)
            : base(javaInterface)
        {
            // "io.cloudx.sdk.InterstitialListener" -> "InterstitialListener"
            var listenerName = javaInterface.Substring(javaInterface.LastIndexOf('.') + 1);
            _onAdLoaded = listenerName + ".onAdLoaded";
            _onAdLoadFailed = listenerName + ".onAdLoadFailed";
            _onAdDisplayed = listenerName + ".onAdDisplayed";
            _onAdDisplayFailed = listenerName + ".onAdDisplayFailed";
            _onAdHidden = listenerName + ".onAdHidden";
            _onAdClicked = listenerName + ".onAdClicked";
            CloudXSdk.Log.LogDebug(() => $"AdListenerProxy created for interface: {javaInterface}");
        }

        // Called when ad is loaded (Android thread); CallbackDispatcher picks the delivery thread
        public void onAdLoaded(AndroidJavaObject cloudXAdObject) => JniCallbackGuard.Run(_onAdLoaded, () =>
        {
            // Convert on Android thread to minimize main thread work
            var cloudXAd = cloudXAdObject.ToCloudXAd();
            CloudXSdk.Log.LogDebug(() => $"onAdLoaded callback received: {cloudXAd}");

            CallbackDispatcher.Dispatch(_onAdLoaded, keepInBackground: false, () => AdLoaded?.Invoke(cloudXAd));
        });

        // Called when ad fails to load (Android thread)
        public void onAdLoadFailed(string adUnitId, AndroidJavaObject cloudXErrorObject) => JniCallbackGuard.Run(_onAdLoadFailed, () =>
        {
            // Convert on Android thread to minimize main thread work
            var cloudXError = cloudXErrorObject.ToCloudXError();
            CloudXSdk.Log.LogDebug(() => $"onAdLoadFailed callback received, adUnitId={adUnitId}, error={cloudXError}");

            CallbackDispatcher.Dispatch(_onAdLoadFailed, keepInBackground: false, () => AdLoadFailed?.Invoke(adUnitId, cloudXError));
        });

        // Called when ad is displayed (Android thread)
        public void onAdDisplayed(AndroidJavaObject cloudXAdObject) => JniCallbackGuard.Run(_onAdDisplayed, () =>
        {
            // Convert on Android thread to minimize main thread work
            var cloudXAd = cloudXAdObject.ToCloudXAd();
            CloudXSdk.Log.LogDebug(() => $"onAdDisplayed callback received: {cloudXAd}");

            CallbackDispatcher.Dispatch(_onAdDisplayed, keepInBackground: false, () => AdDisplayed?.Invoke(cloudXAd));
        });

        // Called when ad fails to display (Android thread)
        public void onAdDisplayFailed(AndroidJavaObject cloudXAdObject, AndroidJavaObject cloudXErrorObject) => JniCallbackGuard.Run(_onAdDisplayFailed, () =>
        {
            // Convert on Android thread to minimize main thread work
            var cloudXAd = cloudXAdObject.ToCloudXAd();
            var cloudXError = cloudXErrorObject.ToCloudXError();
            CloudXSdk.Log.LogDebug(() => $"onAdDisplayFailed callback received: {cloudXAd}, error={cloudXError}");

            CallbackDispatcher.Dispatch(_onAdDisplayFailed, keepInBackground: false, () => AdDisplayFailed?.Invoke(cloudXAd, cloudXError));
        });

        // Called when ad is hidden (closed) (Android thread)
        public void onAdHidden(AndroidJavaObject cloudXAdObject) => JniCallbackGuard.Run(_onAdHidden, () =>
        {
            // Convert on Android thread to minimize main thread work
            var cloudXAd = cloudXAdObject.ToCloudXAd();
            CloudXSdk.Log.LogDebug(() => $"onAdHidden callback received: {cloudXAd}");

            CallbackDispatcher.Dispatch(_onAdHidden, keepInBackground: false, () => AdHidden?.Invoke(cloudXAd));
        });

        // Called when ad is clicked (Android thread)
        public void onAdClicked(AndroidJavaObject cloudXAdObject) => JniCallbackGuard.Run(_onAdClicked, () =>
        {
            // Convert on Android thread to minimize main thread work
            var cloudXAd = cloudXAdObject.ToCloudXAd();
            CloudXSdk.Log.LogDebug(() => $"onAdClicked callback received: {cloudXAd}");

            CallbackDispatcher.Dispatch(_onAdClicked, keepInBackground: false, () => AdClicked?.Invoke(cloudXAd));
        });
    }
}
