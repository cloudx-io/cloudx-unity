#nullable enable

using System;
using UnityEngine;
using CloudX.Internal.Threading;
// ReSharper disable InconsistentNaming

namespace CloudX
{
public class InitializationListenerProxy : AndroidJavaProxy
{
    private readonly Action<CloudXSdkConfiguration> _onSuccess;
    private readonly Action<CloudXError> _onFailure;

    public InitializationListenerProxy(
        Action<CloudXSdkConfiguration> onSuccess,
        Action<CloudXError> onFailure
    )
        : base("io.cloudx.sdk.CloudXInitializationListener")
    {
        CloudXSdk.Log.LogDebug(() => $"InitializationListenerProxy created");
        _onSuccess = onSuccess;
        _onFailure = onFailure;
    }

    // Called from Android when initialization succeeds (Android thread)
    public void onInitialized(AndroidJavaObject configuration) => JniCallbackGuard.Run(OnInitializedName, () =>
    {
        // Convert on Android thread to minimize main thread work
        var sdkConfiguration = configuration.ToCloudXSdkConfiguration();
        CloudXSdk.Log.LogDebug(() => $"onInitialized callback received, configuration={sdkConfiguration}");

        CallbackDispatcher.Dispatch(OnInitializedName, keepInBackground: false,
            () => _onSuccess(sdkConfiguration));
    });

    // Called from Android when initialization fails (Android thread)
    public void onInitializationFailed(AndroidJavaObject cloudXErrorObject) => JniCallbackGuard.Run(OnInitializationFailedName, () =>
    {
        // Convert on Android thread to minimize main thread work
        var cloudXError = cloudXErrorObject.ToCloudXError();
        CloudXSdk.Log.LogDebug(() => $"onInitializationFailed callback received, error={cloudXError}");

        CallbackDispatcher.Dispatch(OnInitializationFailedName, keepInBackground: false,
            () => _onFailure(cloudXError));
    });

    private const string OnInitializedName = "CloudXInitializationListener.onInitialized";
    private const string OnInitializationFailedName = "CloudXInitializationListener.onInitializationFailed";
}
}
