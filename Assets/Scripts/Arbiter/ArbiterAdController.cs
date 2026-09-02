using System;
using System.Collections.Generic;
using CloudX;
using GoogleMobileAds.Api;
using UnityEngine;

/*
 * Shared base for the Arbiter/TPA controllers. Every format follows the same
 * Trusted Arbiter rule - CloudX and AdMob load in parallel, the loaded ones
 * become bids, CloudXSdk.Arbiter picks the platform - so the bookkeeping the
 * formats share lives here once: ids, the loaded CloudXAd (the CloudX bid is
 * built from it), the arbiter call with its in-flight guard, the AdMob bid, and
 * the Google paid-event forwarding that prices AdMob bids. The two families
 * split below: fullscreen (interstitial, rewarded) prepares a winner ahead of
 * the placement in ArbiterFullscreenController; inline (banner, MREC)
 * arbitrates and then renders on a refresh cycle in ArbiterInlineController.
 *
 * Platform identity uses the SDK's CloudXArbiterPlatform everywhere (CloudX,
 * AdMob) so the same value names a loaded side, a bid, and a winner.
 *
 * A publisher copying this into an app takes this base plus the one family base
 * and the one concrete they need.
 */
public abstract class ArbiterAdController : IDisposable
{
    private const string TAG = "CloudXUnityDemo";

    public event Action<CloudXArbiterPlatform> AdLoaded;
    public event Action<CloudXArbiterPlatform, string> AdLoadFailed;
    public event Action<CloudXArbiterPlatform> AdClicked;
    /* Every arbiter result, with the number of bids submitted. */
    public event Action<CloudXArbiterResult, int> ArbiterCompleted;

    protected string CloudXAdUnitId { get; }
    protected string AdMobAdUnitId { get; }

    /*
     * When CloudX initialization failed, its callbacks may never fire and its
     * bridge is not initialized, so the controller skips the CloudX leg and
     * decides locally: AdMob is the only candidate, so AdMob wins.
     */
    protected bool CloudXAvailable { get; }

    protected bool IsDisposed { get; private set; }
    protected bool IsLoadingCloudX { get; set; }
    protected bool IsLoadingAdMob { get; set; }
    protected bool ArbiterInFlight { get; private set; }

    /* The CloudXAd from OnAdLoadSuccess; its AdValues carry the arbiter payload. */
    protected CloudXAd LoadedCloudXAd { get; set; }

    /*
     * Bumped by Hide/Dispose so an arbiter result that was in flight at that
     * moment is reported but never acted on.
     */
    private int _generation;

    protected ArbiterAdController(string cloudXAdUnitId, string adMobAdUnitId, bool cloudXAvailable)
    {
        CloudXAdUnitId = cloudXAdUnitId;
        AdMobAdUnitId = adMobAdUnitId;
        CloudXAvailable = cloudXAvailable;
    }

    protected static void Log(string message) => Debug.Log($"[{TAG}][Arbiter] {message}");

    public abstract void Load();

    protected void RaiseAdLoaded(CloudXArbiterPlatform platform) => AdLoaded?.Invoke(platform);
    protected void RaiseAdLoadFailed(CloudXArbiterPlatform platform, string message) => AdLoadFailed?.Invoke(platform, message);
    protected void RaiseAdClicked(CloudXArbiterPlatform platform) => AdClicked?.Invoke(platform);

    protected void InvalidateInFlightArbiter() => _generation++;

    /*
     * Runs one arbiter round over the loaded candidates. The SDK owns the
     * timeout and the fallback and always completes: one bid wins without a
     * service call, several bids go to the arbiter service or, when it is
     * unavailable, to the local highest-price fallback. Nothing here wraps the
     * call in a timer or compares prices - the docs forbid both. The callback
     * arrives on the Unity main thread.
     */
    protected void RunArbiter(List<CloudXArbiterBid> bids, Action<CloudXArbiterResult> onResult)
    {
        ArbiterInFlight = true;
        var generation = _generation;
        Log($"Arbiter: {bids.Count} bid(s) for {CloudXAdUnitId}");

        Decide(bids, result =>
        {
            ArbiterInFlight = false;

            if (IsDisposed)
            {
                return;
            }

            Log($"Arbiter result: platform={result.Platform} platformName={result.PlatformName} " +
                $"id={result.Id} bidId={result.BidId ?? "-"} bids={bids.Count}");
            ArbiterCompleted?.Invoke(result, bids.Count);

            if (generation == _generation)
            {
                onResult(result);
            }
            else
            {
                OnArbiterResultInvalidated();
            }
        });
    }

    /*
     * A result arrived for a round that Hide() invalidated while it was in
     * flight. Nothing may be shown from it, but the family may need to start the
     * round the user asked for in the meantime.
     */
    protected virtual void OnArbiterResultInvalidated()
    {
    }

    private void Decide(List<CloudXArbiterBid> bids, Action<CloudXArbiterResult> onResult)
    {
        if (CloudXAvailable)
        {
            CloudXSdk.Arbiter(bids, onResult);
            return;
        }

        /*
         * CloudX never initialized, so the bids can only be the AdMob one. The
         * SDK would select a lone bid without a service call anyway; deciding
         * locally keeps the flow uniform without touching an uninitialized SDK.
         */
        onResult(new CloudXArbiterResult(
            Id: "local",
            Platform: CloudXArbiterPlatform.AdMob,
            PlatformName: "AdMob",
            BidId: null,
            Extras: new Dictionary<string, string>()));
    }

    /*
     * An AdMob bid carries no price: CloudX prices it from the realized revenue
     * reported through ReportAdMobPaidEvent. NetworkName is the ad source that
     * filled, when Google exposes it.
     */
    protected CloudXArbiterBid AdMobBid(string adSourceName) =>
        new CloudXArbiterBid.AdMob(AdMobAdUnitId, NetworkName: adSourceName ?? "admob");

    protected static string AdSourceName(ResponseInfo responseInfo) =>
        responseInfo?.GetLoadedAdapterResponseInfo()?.AdSourceName;

    /*
     * Required part of the AdMob integration: forward Google's impression-level
     * revenue so CloudX learns what AdMob demand actually pays. AdValue.Value is
     * in micro-units of CurrencyCode.
     */
    protected void ReportAdMobPaidEvent(AdValue adValue, string adFormat, string adSourceName)
    {
        if (!CloudXAvailable)
        {
            Log("AdMob paid event not forwarded: CloudX is not initialized");
            return;
        }

        var accepted = CloudXSdk.ReportRevenueData(new CloudXRevenueData(
            Platform: CloudXRevenuePlatform.AdMob,
            Revenue: adValue.Value / 1_000_000.0,
            AdFormat: adFormat,
            CurrencyCode: adValue.CurrencyCode,
            Precision: ToCloudXRevenuePrecision(adValue.Precision),
            NetworkName: adSourceName,
            AdUnitId: AdMobAdUnitId));

        Log($"ReportRevenueData({adFormat}, {adValue.Value} micros {adValue.CurrencyCode}, " +
            $"{adValue.Precision}) accepted={accepted}");
    }

    private static CloudXRevenuePrecision ToCloudXRevenuePrecision(AdValue.PrecisionType precision) =>
        precision switch
        {
            AdValue.PrecisionType.Precise => CloudXRevenuePrecision.Exact,
            AdValue.PrecisionType.Estimated => CloudXRevenuePrecision.Estimated,
            AdValue.PrecisionType.PublisherProvided => CloudXRevenuePrecision.PublisherDefined,
            _ => CloudXRevenuePrecision.Undefined,
        };

    /*
     * Subscribe/unsubscribe the CloudX callbacks. Called from the concrete
     * constructor (not here) so the subclass is fully constructed first.
     */
    protected abstract void SubscribeCloudXCallbacks();
    protected abstract void UnsubscribeCloudXCallbacks();
    protected abstract void DestroyCloudXAd();
    protected abstract void DestroyAdMobAd();

    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        InvalidateInFlightArbiter();

        UnsubscribeCloudXCallbacks();

        if (CloudXAvailable)
        {
            DestroyCloudXAd();
        }

        DestroyAdMobAd();
    }
}
