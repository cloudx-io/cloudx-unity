#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using CloudX.Internal.Threading;

namespace CloudX
{
internal sealed class ArbiterListenerProxy : AndroidJavaProxy
{
    private readonly Action<CloudXArbiterResult> _onCompleted;

    public ArbiterListenerProxy(Action<CloudXArbiterResult> onCompleted)
        : base("io.cloudx.sdk.CloudXArbiterListener")
    {
        _onCompleted = onCompleted;
    }

    public void onCompleted(AndroidJavaObject result)
    {
        CloudXSdk.Log.LogDebug(() => "ArbiterListenerProxy.onCompleted received");
        var arbiterResult = result.ToCloudXArbiterResult();
        UnityMainThreadDispatcher.Instance().Enqueue(() => _onCompleted(arbiterResult));
    }
}
}
