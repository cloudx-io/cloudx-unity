#nullable enable

using System;
using UnityEngine;
using CloudX.Internal.Threading;

namespace CloudX
{
    /// <summary>
    /// Callbacks for SDK initialization lifecycle events.
    /// Subscribe to these events before calling CloudXSdk.Initialize().
    /// </summary>
    public static class CloudXInitializationCallbacks
    {
        /// <summary>
        /// Fired when the SDK has been successfully initialized.
        /// Runs on the Unity main thread unless <see cref="CloudXSdk.InvokeEventsOnUnityMainThread"/> is false.
        /// </summary>
        public static event Action<CloudXSdkConfiguration>? OnSdkInitializedEvent;

        /// <summary>
        /// Fired when SDK initialization has failed.
        /// Runs on the Unity main thread unless <see cref="CloudXSdk.InvokeEventsOnUnityMainThread"/> is false.
        /// </summary>
        public static event Action<CloudXError>? OnSdkInitializationFailedEvent;

        /// <summary>
        /// Internal method to trigger the OnSdkInitializedEvent.
        /// </summary>
        internal static void OnSdkInitializedInternal(CloudXSdkConfiguration configuration)
        {
            CallbackInvoker.Invoke(OnSdkInitializedEvent, configuration, "OnSdkInitializedEvent");
        }

        /// <summary>
        /// Internal method to trigger the OnSdkInitializationFailedEvent.
        /// </summary>
        internal static void OnSdkInitializationFailedInternal(CloudXError error)
        {
            CallbackInvoker.Invoke(OnSdkInitializationFailedEvent, error, "OnSdkInitializationFailedEvent");
        }

        /// <summary>
        /// Reset all static events to null at the start of each Play Mode session.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetEvents()
        {
            OnSdkInitializedEvent = null;
            OnSdkInitializationFailedEvent = null;
        }
    }
}
