using System;
using UnityEngine;
using CloudX.Internal.Threading;

namespace CloudX
{
public static class CloudXAdsCallbacks
{
    public static class Banner
    {
        /// <summary>
        /// Fired when a banner ad finishes loading successfully.
        /// Runs on the Unity main thread unless <see cref="CloudXSdk.InvokeEventsOnUnityMainThread"/> is false.
        /// </summary>
        public static event Action<CloudXAd> OnAdLoadSuccess;
        /// <summary>
        /// Fired when a banner ad fails to load.
        /// Runs on the Unity main thread unless <see cref="CloudXSdk.InvokeEventsOnUnityMainThread"/> is false.
        /// </summary>
        public static event Action<string, CloudXError> OnAdLoadFailed;
        /// <summary>
        /// Fired when a banner ad is clicked.
        /// Runs on the Unity main thread unless <see cref="CloudXSdk.InvokeEventsOnUnityMainThread"/> is false.
        /// </summary>
        public static event Action<CloudXAd> OnAdClicked;
        /// <summary>
        /// Fired as soon as impression revenue is available.
        /// Runs on the Unity main thread unless <see cref="CloudXSdk.InvokeEventsOnUnityMainThread"/> is false.
        /// </summary>
        public static event Action<CloudXAd> OnAdRevenuePaid;

        // Internal methods to trigger the events
        internal static void OnAdLoadSuccessInternal(CloudXAd cloudXAd)
        {
            CallbackInvoker.Invoke(OnAdLoadSuccess, cloudXAd, "Banner.OnAdLoadSuccess");
        }

        internal static void OnAdLoadFailedInternal(string adUnitId, CloudXError cloudXError)
        {
            CallbackInvoker.Invoke(OnAdLoadFailed, adUnitId, cloudXError, "Banner.OnAdLoadFailed");
        }

        internal static void OnAdClickedInternal(CloudXAd cloudXAd)
        {
            CallbackInvoker.Invoke(OnAdClicked, cloudXAd, "Banner.OnAdClicked");
        }

        internal static void OnAdRevenuePaidInternal(CloudXAd cloudXAd)
        {
            CallbackInvoker.Invoke(OnAdRevenuePaid, cloudXAd, "Banner.OnAdRevenuePaid");
        }

        // Add a method to clear all events
        internal static void ResetEvents()
        {
            OnAdLoadSuccess = null;
            OnAdLoadFailed = null;
            OnAdClicked = null;
            OnAdRevenuePaid = null;
        }
    }

    public static class Mrec
    {
        /// <summary>
        /// Fired when an MREC ad finishes loading successfully.
        /// Runs on the Unity main thread unless <see cref="CloudXSdk.InvokeEventsOnUnityMainThread"/> is false.
        /// </summary>
        public static event Action<CloudXAd> OnAdLoadSuccess;
        /// <summary>
        /// Fired when an MREC ad fails to load.
        /// Runs on the Unity main thread unless <see cref="CloudXSdk.InvokeEventsOnUnityMainThread"/> is false.
        /// </summary>
        public static event Action<string, CloudXError> OnAdLoadFailed;
        /// <summary>
        /// Fired when an MREC ad is clicked.
        /// Runs on the Unity main thread unless <see cref="CloudXSdk.InvokeEventsOnUnityMainThread"/> is false.
        /// </summary>
        public static event Action<CloudXAd> OnAdClicked;
        /// <summary>
        /// Fired as soon as impression revenue is available.
        /// Runs on the Unity main thread unless <see cref="CloudXSdk.InvokeEventsOnUnityMainThread"/> is false.
        /// </summary>
        public static event Action<CloudXAd> OnAdRevenuePaid;

        // Internal methods to trigger the events
        internal static void OnAdLoadSuccessInternal(CloudXAd cloudXAd)
        {
            CallbackInvoker.Invoke(OnAdLoadSuccess, cloudXAd, "Mrec.OnAdLoadSuccess");
        }

        internal static void OnAdLoadFailedInternal(string adUnitId, CloudXError cloudXError)
        {
            CallbackInvoker.Invoke(OnAdLoadFailed, adUnitId, cloudXError, "Mrec.OnAdLoadFailed");
        }

        internal static void OnAdClickedInternal(CloudXAd cloudXAd)
        {
            CallbackInvoker.Invoke(OnAdClicked, cloudXAd, "Mrec.OnAdClicked");
        }

        internal static void OnAdRevenuePaidInternal(CloudXAd cloudXAd)
        {
            CallbackInvoker.Invoke(OnAdRevenuePaid, cloudXAd, "Mrec.OnAdRevenuePaid");
        }

        // Add a method to clear all events
        internal static void ResetEvents()
        {
            OnAdLoadSuccess = null;
            OnAdLoadFailed = null;
            OnAdClicked = null;
            OnAdRevenuePaid = null;
        }
    }

    public static class Interstitial
    {
        /// <summary>
        /// Fired when an interstitial ad finishes loading successfully.
        /// Runs on the Unity main thread unless <see cref="CloudXSdk.InvokeEventsOnUnityMainThread"/> is false.
        /// </summary>
        public static event Action<CloudXAd> OnAdLoadSuccess;
        /// <summary>
        /// Fired when an interstitial ad fails to load.
        /// Runs on the Unity main thread unless <see cref="CloudXSdk.InvokeEventsOnUnityMainThread"/> is false.
        /// </summary>
        public static event Action<string, CloudXError> OnAdLoadFailed;
        /// <summary>
        /// Fired when an interstitial ad is displayed.
        /// Runs on the Unity main thread unless <see cref="CloudXSdk.InvokeEventsOnUnityMainThread"/> is false.
        /// </summary>
        public static event Action<CloudXAd> OnAdShowSuccess;
        /// <summary>
        /// Fired when an interstitial ad fails to display.
        /// Runs on the Unity main thread unless <see cref="CloudXSdk.InvokeEventsOnUnityMainThread"/> is false.
        /// </summary>
        public static event Action<CloudXAd, CloudXError> OnAdShowFailed;
        /// <summary>
        /// Fired when an interstitial ad is hidden after being shown.
        /// Runs on the Unity main thread unless <see cref="CloudXSdk.InvokeEventsOnUnityMainThread"/> is false.
        /// </summary>
        public static event Action<CloudXAd> OnAdHidden;
        /// <summary>
        /// Fired when an interstitial ad is clicked.
        /// Runs on the Unity main thread unless <see cref="CloudXSdk.InvokeEventsOnUnityMainThread"/> is false.
        /// </summary>
        public static event Action<CloudXAd> OnAdClicked;
        /// <summary>
        /// Fired as soon as impression revenue is available.
        /// By default delivered immediately on the native callback thread, which is not the Unity main
        /// thread, so handlers must not use Unity APIs directly. Set
        /// <see cref="CloudXSdk.InvokeEventsOnUnityMainThread"/> to true to receive it on the Unity main thread.
        /// </summary>
        public static event Action<CloudXAd> OnAdRevenuePaid;

        // Internal methods to trigger the events
        internal static void OnAdLoadSuccessInternal(CloudXAd cloudXAd)
        {
            CallbackInvoker.Invoke(OnAdLoadSuccess, cloudXAd, "Interstitial.OnAdLoadSuccess");
        }

        internal static void OnAdLoadFailedInternal(string adUnitId, CloudXError cloudXError)
        {
            CallbackInvoker.Invoke(OnAdLoadFailed, adUnitId, cloudXError, "Interstitial.OnAdLoadFailed");
        }

        internal static void OnAdShowSuccessInternal(CloudXAd cloudXAd)
        {
            CallbackInvoker.Invoke(OnAdShowSuccess, cloudXAd, "Interstitial.OnAdShowSuccess");
        }

        internal static void OnAdShowFailedInternal(CloudXAd cloudXAd, CloudXError cloudXError)
        {
            CallbackInvoker.Invoke(OnAdShowFailed, cloudXAd, cloudXError, "Interstitial.OnAdShowFailed");
        }

        internal static void OnAdHiddenInternal(CloudXAd cloudXAd)
        {
            CallbackInvoker.Invoke(OnAdHidden, cloudXAd, "Interstitial.OnAdHidden");
        }

        internal static void OnAdClickedInternal(CloudXAd cloudXAd)
        {
            CallbackInvoker.Invoke(OnAdClicked, cloudXAd, "Interstitial.OnAdClicked");
        }

        internal static void OnAdRevenuePaidInternal(CloudXAd cloudXAd)
        {
            CallbackInvoker.Invoke(OnAdRevenuePaid, cloudXAd, "Interstitial.OnAdRevenuePaid");
        }

        // Add a method to clear all events
        internal static void ResetEvents()
        {
            OnAdLoadSuccess = null;
            OnAdLoadFailed = null;
            OnAdShowSuccess = null;
            OnAdShowFailed = null;
            OnAdHidden = null;
            OnAdClicked = null;
            OnAdRevenuePaid = null;
        }
    }

    public static class AppOpen
    {
        /// <summary>
        /// Fired when an app open ad finishes loading successfully.
        /// Runs on the Unity main thread unless <see cref="CloudXSdk.InvokeEventsOnUnityMainThread"/> is false.
        /// </summary>
        public static event Action<CloudXAd> OnAdLoadSuccess;
        /// <summary>
        /// Fired when an app open ad fails to load.
        /// Runs on the Unity main thread unless <see cref="CloudXSdk.InvokeEventsOnUnityMainThread"/> is false.
        /// </summary>
        public static event Action<string, CloudXError> OnAdLoadFailed;
        /// <summary>
        /// Fired when an app open ad is displayed.
        /// Runs on the Unity main thread unless <see cref="CloudXSdk.InvokeEventsOnUnityMainThread"/> is false.
        /// </summary>
        public static event Action<CloudXAd> OnAdShowSuccess;
        /// <summary>
        /// Fired when an app open ad fails to display.
        /// Runs on the Unity main thread unless <see cref="CloudXSdk.InvokeEventsOnUnityMainThread"/> is false.
        /// </summary>
        public static event Action<CloudXAd, CloudXError> OnAdShowFailed;
        /// <summary>
        /// Fired when an app open ad is hidden after being shown.
        /// Runs on the Unity main thread unless <see cref="CloudXSdk.InvokeEventsOnUnityMainThread"/> is false.
        /// </summary>
        public static event Action<CloudXAd> OnAdHidden;
        /// <summary>
        /// Fired when an app open ad is clicked.
        /// Runs on the Unity main thread unless <see cref="CloudXSdk.InvokeEventsOnUnityMainThread"/> is false.
        /// </summary>
        public static event Action<CloudXAd> OnAdClicked;
        /// <summary>
        /// Fired as soon as impression revenue is available.
        /// By default delivered immediately on the native callback thread, which is not the Unity main
        /// thread, so handlers must not use Unity APIs directly. Set
        /// <see cref="CloudXSdk.InvokeEventsOnUnityMainThread"/> to true to receive it on the Unity main thread.
        /// </summary>
        public static event Action<CloudXAd> OnAdRevenuePaid;

        internal static void OnAdLoadSuccessInternal(CloudXAd cloudXAd)
        {
            CallbackInvoker.Invoke(OnAdLoadSuccess, cloudXAd, "AppOpen.OnAdLoadSuccess");
        }

        internal static void OnAdLoadFailedInternal(string adUnitId, CloudXError cloudXError)
        {
            CallbackInvoker.Invoke(OnAdLoadFailed, adUnitId, cloudXError, "AppOpen.OnAdLoadFailed");
        }

        internal static void OnAdShowSuccessInternal(CloudXAd cloudXAd)
        {
            CallbackInvoker.Invoke(OnAdShowSuccess, cloudXAd, "AppOpen.OnAdShowSuccess");
        }

        internal static void OnAdShowFailedInternal(CloudXAd cloudXAd, CloudXError cloudXError)
        {
            CallbackInvoker.Invoke(OnAdShowFailed, cloudXAd, cloudXError, "AppOpen.OnAdShowFailed");
        }

        internal static void OnAdHiddenInternal(CloudXAd cloudXAd)
        {
            CallbackInvoker.Invoke(OnAdHidden, cloudXAd, "AppOpen.OnAdHidden");
        }

        internal static void OnAdClickedInternal(CloudXAd cloudXAd)
        {
            CallbackInvoker.Invoke(OnAdClicked, cloudXAd, "AppOpen.OnAdClicked");
        }

        internal static void OnAdRevenuePaidInternal(CloudXAd cloudXAd)
        {
            CallbackInvoker.Invoke(OnAdRevenuePaid, cloudXAd, "AppOpen.OnAdRevenuePaid");
        }

        internal static void ResetEvents()
        {
            OnAdLoadSuccess = null;
            OnAdLoadFailed = null;
            OnAdShowSuccess = null;
            OnAdShowFailed = null;
            OnAdHidden = null;
            OnAdClicked = null;
            OnAdRevenuePaid = null;
        }
    }

    public static class Rewarded
    {
        /// <summary>
        /// Fired when a rewarded ad finishes loading successfully.
        /// Runs on the Unity main thread unless <see cref="CloudXSdk.InvokeEventsOnUnityMainThread"/> is false.
        /// </summary>
        public static event Action<CloudXAd> OnAdLoadSuccess;
        /// <summary>
        /// Fired when a rewarded ad fails to load.
        /// Runs on the Unity main thread unless <see cref="CloudXSdk.InvokeEventsOnUnityMainThread"/> is false.
        /// </summary>
        public static event Action<string, CloudXError> OnAdLoadFailed;
        /// <summary>
        /// Fired when a rewarded ad is displayed.
        /// Runs on the Unity main thread unless <see cref="CloudXSdk.InvokeEventsOnUnityMainThread"/> is false.
        /// </summary>
        public static event Action<CloudXAd> OnAdShowSuccess;
        /// <summary>
        /// Fired when a rewarded ad fails to display.
        /// Runs on the Unity main thread unless <see cref="CloudXSdk.InvokeEventsOnUnityMainThread"/> is false.
        /// </summary>
        public static event Action<CloudXAd, CloudXError> OnAdShowFailed;
        /// <summary>
        /// Fired when a rewarded ad is hidden after being shown.
        /// Runs on the Unity main thread unless <see cref="CloudXSdk.InvokeEventsOnUnityMainThread"/> is false.
        /// </summary>
        public static event Action<CloudXAd> OnAdHidden;
        /// <summary>
        /// Fired when a rewarded ad is clicked.
        /// Runs on the Unity main thread unless <see cref="CloudXSdk.InvokeEventsOnUnityMainThread"/> is false.
        /// </summary>
        public static event Action<CloudXAd> OnAdClicked;
        /// <summary>
        /// Fired when the user earns the rewarded ad payout.
        /// Runs on the Unity main thread unless <see cref="CloudXSdk.InvokeEventsOnUnityMainThread"/> is false.
        /// </summary>
        public static event Action<CloudXAd, CloudXReward> OnAdRewarded;
        /// <summary>
        /// Fired as soon as impression revenue is available.
        /// By default delivered immediately on the native callback thread, which is not the Unity main
        /// thread, so handlers must not use Unity APIs directly. Set
        /// <see cref="CloudXSdk.InvokeEventsOnUnityMainThread"/> to true to receive it on the Unity main thread.
        /// </summary>
        public static event Action<CloudXAd> OnAdRevenuePaid;

        // Internal methods to trigger the events
        internal static void OnAdLoadSuccessInternal(CloudXAd cloudXAd)
        {
            CallbackInvoker.Invoke(OnAdLoadSuccess, cloudXAd, "Rewarded.OnAdLoadSuccess");
        }

        internal static void OnAdLoadFailedInternal(string adUnitId, CloudXError cloudXError)
        {
            CallbackInvoker.Invoke(OnAdLoadFailed, adUnitId, cloudXError, "Rewarded.OnAdLoadFailed");
        }

        internal static void OnAdShowSuccessInternal(CloudXAd cloudXAd)
        {
            CallbackInvoker.Invoke(OnAdShowSuccess, cloudXAd, "Rewarded.OnAdShowSuccess");
        }

        internal static void OnAdShowFailedInternal(CloudXAd cloudXAd, CloudXError cloudXError)
        {
            CallbackInvoker.Invoke(OnAdShowFailed, cloudXAd, cloudXError, "Rewarded.OnAdShowFailed");
        }

        internal static void OnAdHiddenInternal(CloudXAd cloudXAd)
        {
            CallbackInvoker.Invoke(OnAdHidden, cloudXAd, "Rewarded.OnAdHidden");
        }

        internal static void OnAdClickedInternal(CloudXAd cloudXAd)
        {
            CallbackInvoker.Invoke(OnAdClicked, cloudXAd, "Rewarded.OnAdClicked");
        }

        internal static void OnAdRewardedInternal(CloudXAd cloudXAd, CloudXReward cloudXReward)
        {
            CallbackInvoker.Invoke(OnAdRewarded, cloudXAd, cloudXReward, "Rewarded.OnAdRewarded");
        }

        internal static void OnAdRevenuePaidInternal(CloudXAd cloudXAd)
        {
            CallbackInvoker.Invoke(OnAdRevenuePaid, cloudXAd, "Rewarded.OnAdRevenuePaid");
        }

        // Add a method to clear all events
        internal static void ResetEvents()
        {
            OnAdLoadSuccess = null;
            OnAdLoadFailed = null;
            OnAdShowSuccess = null;
            OnAdShowFailed = null;
            OnAdHidden = null;
            OnAdClicked = null;
            OnAdRewarded = null;
            OnAdRevenuePaid = null;
        }
    }

    // Reset all static events to null at the start of each Play Mode session
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetEvents()
    {
        Banner.ResetEvents();
        Mrec.ResetEvents();
        Interstitial.ResetEvents();
        AppOpen.ResetEvents();
        Rewarded.ResetEvents();
    }
}
}
