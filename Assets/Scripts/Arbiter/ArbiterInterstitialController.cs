using System;
using System.Collections.Generic;
using CloudX;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using UnityEngine;

/*
 * Trusted Arbiter for interstitials. CloudX and AdMob load in parallel, the
 * candidates that fill become bids, and CloudXSdk.Arbiter picks the platform
 * to show.
 *
 * This is the prepare-ahead rule from docs.cloudx.io -> Trusted Arbiter. The
 * arbiter runs as soon as both candidates have settled (loaded or failed) and
 * stores the result. Show() then shows the stored winner immediately, with no
 * arbiter call and no network call on the show path. It returns false when no
 * winner is prepared, in which case the caller carries on with the game. The
 * cycle restarts after the ad closes.
 *
 * AdMob bids carry no price. CloudX prices them from the revenue this
 * controller forwards after every AdMob impression through ReportRevenueData.
 * That forwarding is a required part of the integration, not telemetry.
 *
 * Copy this file to take the complete interstitial integration. It needs the
 * CloudX SDK package and Google Mobile Ads Unity plugin com.google.ads.mobile
 * 11.3.0. The demo ids live in Assets/Scripts/DemoConfig.cs; copy that file or
 * pass your own ids to the constructor. ArbiterScreen.cs brings both SDKs up
 * and is demo-only. Rewarded follows this controller with the rewarded SDK
 * calls substituted and one reward event added.
 */
public sealed class ArbiterInterstitialController : IDisposable
{
    private const string TAG = "CloudXUnityDemo";
    private const string AdFormatName = "interstitial";

    private readonly string _cloudXAdUnitId;
    private readonly string _adMobAdUnitId;
    private readonly bool _includeCloudXInArbitration;

    private bool _isDisposed;
    private bool _isLoadingCloudX;
    private bool _isLoadingAdMob;
    private bool _cloudXSettled;
    private bool _adMobSettled;
    private bool _arbiterInFlight;
    private bool _isShowing;
    private CloudXAd _loadedCloudXAd;
    private InterstitialAd _adMobInterstitial;
    private CloudXArbiterResult _nextWinner;

    public event Action<CloudXArbiterPlatform> AdLoaded;
    public event Action<CloudXArbiterPlatform, string> AdLoadFailed;
    public event Action<CloudXArbiterResult, int> ArbiterCompleted;
    public event Action<CloudXArbiterPlatform> AdShown;
    public event Action<CloudXArbiterPlatform, string> AdShowFailed;
    public event Action<CloudXArbiterPlatform> AdClosed;
    public event Action<CloudXArbiterPlatform> AdClicked;
    public event Action<CloudXRevenueData, bool> RevenueReported;

    public ArbiterInterstitialController(
        string cloudXAdUnitId,
        string adMobAdUnitId,
        bool cloudXAvailable)
    {
        _cloudXAdUnitId = cloudXAdUnitId;
        _adMobAdUnitId = adMobAdUnitId;
        _includeCloudXInArbitration = cloudXAvailable;
        _cloudXSettled = !cloudXAvailable;

        SubscribeCloudXCallbacks();
    }

    /* The platform Show() would use right now; null when no winner is stored. */
    public CloudXArbiterPlatform? PreparedWinner =>
        _nextWinner == null || _nextWinner.Platform == CloudXArbiterPlatform.None
            ? null
            : _nextWinner.Platform;

    /* True between a show call and its close or show-failure callback. */
    public bool IsShowing => _isShowing;

    /*
     * True when nothing is loaded, loading, decided or showing, so only a fresh
     * Load() can move things forward. The screen's backoff retry keys off this.
     */
    public bool NeedsRetry =>
        !_isDisposed && !_arbiterInFlight && !_isShowing && PreparedWinner == null
        && !_isLoadingCloudX && !_isLoadingAdMob && !CloudXHasBid && !AdMobCanShow();

    private bool CloudXHasBid =>
        _loadedCloudXAd != null && CloudXSdk.IsInterstitialReady(_cloudXAdUnitId);

    private static void Log(string message) => Debug.Log($"[{TAG}][Arbiter] {message}");

    /*
     * Loads whichever network does not hold a fill. After one side failed, a
     * retry reloads only that side and re-arbitrates when it settles. While an
     * ad is showing nothing is loaded because the shown AdMob object must not
     * be destroyed under the user. The close callback restarts the cycle.
     */
    public void Load()
    {
        if (_isDisposed || _arbiterInFlight || _isShowing)
        {
            return;
        }

        if (_includeCloudXInArbitration && !CloudXHasBid && !_isLoadingCloudX)
        {
            _cloudXSettled = false;
            _loadedCloudXAd = null;
            _isLoadingCloudX = true;
            CloudXSdk.LoadInterstitial(_cloudXAdUnitId);
        }

        if (!AdMobCanShow() && !_isLoadingAdMob)
        {
            _adMobSettled = false;
            _isLoadingAdMob = true;
            DestroyAdMobAd();
            AdMobLoad();
        }

        /*
         * Both sides may already hold a fill with no winner stored after a None
         * result or after a stale winner was dropped. Nothing loads in that case,
         * so run the round again from here.
         */
        if (_nextWinner == null)
        {
            MaybePrepareWinner();
        }
    }

    /*
     * Shows the stored winner. A stale winner is dropped and false is returned,
     * so the caller can continue and reload. A second call while an ad is
     * showing also returns false; use IsShowing to distinguish those cases.
     */
    public bool Show()
    {
        if (_isDisposed || _isShowing || _nextWinner == null)
        {
            return false;
        }

        var winner = _nextWinner.Platform;
        _nextWinner = null;

        switch (winner)
        {
            case CloudXArbiterPlatform.CloudX when CloudXHasBid:
                _isShowing = true;
                CloudXSdk.ShowInterstitial(_cloudXAdUnitId);
                return true;
            case CloudXArbiterPlatform.CloudX:
                _loadedCloudXAd = null;
                return false;
            case CloudXArbiterPlatform.AdMob when AdMobCanShow():
                _isShowing = true;
                _adMobInterstitial.Show();
                return true;
            case CloudXArbiterPlatform.AdMob:
                DestroyAdMobAd();
                return false;
            default:
                return false;
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        UnsubscribeCloudXCallbacks();

        if (_includeCloudXInArbitration)
        {
            CloudXSdk.DestroyInterstitial(_cloudXAdUnitId);
        }

        DestroyAdMobAd();
    }

    /*
     * Runs one arbiter round once both networks have settled and stores the
     * result. The SDK owns the timeout and fallback. One bid wins without a
     * service call; several bids use the arbiter service or its local fallback.
     */
    private void MaybePrepareWinner()
    {
        if (_isDisposed || _arbiterInFlight || !_cloudXSettled || !_adMobSettled)
        {
            return;
        }

        var bids = new List<CloudXArbiterBid>();

        if (_includeCloudXInArbitration && CloudXHasBid)
        {
            bids.Add(new CloudXArbiterBid.CloudX(_loadedCloudXAd));
        }

        if (AdMobCanShow())
        {
            bids.Add(new CloudXArbiterBid.AdMob(
                _adMobAdUnitId,
                NetworkName: AdMobNetworkName(_adMobInterstitial.GetResponseInfo())));
        }

        if (bids.Count == 0)
        {
            return;
        }

        RunArbiter(bids);
    }

    private void RunArbiter(List<CloudXArbiterBid> bids)
    {
        _arbiterInFlight = true;
        Log($"Arbiter: {bids.Count} bid(s) for {_cloudXAdUnitId}");

        void OnResult(CloudXArbiterResult result)
        {
            _arbiterInFlight = false;

            if (_isDisposed)
            {
                return;
            }

            Log($"Arbiter result: platform={result.Platform} platformName={result.PlatformName} " +
                $"id={result.Id} bidId={result.BidId ?? "-"} bids={bids.Count}");

            /* None is not a winner; storing it would leave the slot wedged. */
            _nextWinner = result.Platform == CloudXArbiterPlatform.None ? null : result;
            ArbiterCompleted?.Invoke(result, bids.Count);
        }

        if (_includeCloudXInArbitration)
        {
            CloudXSdk.Arbiter(bids, OnResult);
            return;
        }

        /*
         * CloudX never initialized, so AdMob is the only possible candidate.
         * Decide locally instead of calling an uninitialized SDK bridge.
         */
        OnResult(new CloudXArbiterResult(
            Id: "local",
            Platform: CloudXArbiterPlatform.AdMob,
            PlatformName: "AdMob",
            BidId: null,
            Extras: new Dictionary<string, string>()));
    }

    private void SubscribeCloudXCallbacks()
    {
        CloudXAdsCallbacks.Interstitial.OnAdLoadSuccess += CloudXOnLoadSuccess;
        CloudXAdsCallbacks.Interstitial.OnAdLoadFailed += CloudXOnLoadFailed;
        CloudXAdsCallbacks.Interstitial.OnAdShowSuccess += CloudXOnShowSuccess;
        CloudXAdsCallbacks.Interstitial.OnAdShowFailed += CloudXOnShowFailed;
        CloudXAdsCallbacks.Interstitial.OnAdHidden += CloudXOnHidden;
        CloudXAdsCallbacks.Interstitial.OnAdClicked += CloudXOnClicked;
    }

    private void UnsubscribeCloudXCallbacks()
    {
        CloudXAdsCallbacks.Interstitial.OnAdLoadSuccess -= CloudXOnLoadSuccess;
        CloudXAdsCallbacks.Interstitial.OnAdLoadFailed -= CloudXOnLoadFailed;
        CloudXAdsCallbacks.Interstitial.OnAdShowSuccess -= CloudXOnShowSuccess;
        CloudXAdsCallbacks.Interstitial.OnAdShowFailed -= CloudXOnShowFailed;
        CloudXAdsCallbacks.Interstitial.OnAdHidden -= CloudXOnHidden;
        CloudXAdsCallbacks.Interstitial.OnAdClicked -= CloudXOnClicked;
    }

    private void CloudXOnLoadSuccess(CloudXAd ad)
    {
        if (_isDisposed || ad.AdUnitId != _cloudXAdUnitId)
        {
            return;
        }

        _isLoadingCloudX = false;
        _loadedCloudXAd = ad;
        _cloudXSettled = true;
        Log($"CloudX loaded: {ad.NetworkName} ${ad.Revenue:0.0000}");
        AdLoaded?.Invoke(CloudXArbiterPlatform.CloudX);
        MaybePrepareWinner();
    }

    private void CloudXOnLoadFailed(string adUnitId, CloudXError error)
    {
        if (_isDisposed || adUnitId != _cloudXAdUnitId)
        {
            return;
        }

        _isLoadingCloudX = false;
        _loadedCloudXAd = null;
        _cloudXSettled = true;
        AdLoadFailed?.Invoke(CloudXArbiterPlatform.CloudX, FailureText(error));
        MaybePrepareWinner();
    }

    private void CloudXOnShowSuccess(CloudXAd ad)
    {
        if (!_isDisposed && ad.AdUnitId == _cloudXAdUnitId)
        {
            AdShown?.Invoke(CloudXArbiterPlatform.CloudX);
        }
    }

    private void CloudXOnShowFailed(CloudXAd ad, CloudXError error)
    {
        if (_isDisposed || ad.AdUnitId != _cloudXAdUnitId)
        {
            return;
        }

        _loadedCloudXAd = null;
        _isShowing = false;
        AdShowFailed?.Invoke(CloudXArbiterPlatform.CloudX, FailureText(error));
    }

    private void CloudXOnHidden(CloudXAd ad)
    {
        if (_isDisposed || ad.AdUnitId != _cloudXAdUnitId)
        {
            return;
        }

        _loadedCloudXAd = null;
        _isShowing = false;
        AdClosed?.Invoke(CloudXArbiterPlatform.CloudX);
    }

    private void CloudXOnClicked(CloudXAd ad)
    {
        if (!_isDisposed && ad.AdUnitId == _cloudXAdUnitId)
        {
            AdClicked?.Invoke(CloudXArbiterPlatform.CloudX);
        }
    }

    private bool AdMobCanShow() =>
        _adMobInterstitial != null && _adMobInterstitial.CanShowAd();

    private void AdMobLoad()
    {
        /*
         * Google callbacks do not run on the Unity main thread. Move each whole
         * callback body onto it so controller state and UI events stay ordered.
         */
        InterstitialAd.Load(
            _adMobAdUnitId,
            new AdRequest(),
            (ad, error) => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                if (_isDisposed)
                {
                    ad?.Destroy();
                    return;
                }

                if (error != null || ad == null)
                {
                    _isLoadingAdMob = false;
                    _adMobSettled = true;
                    AdLoadFailed?.Invoke(
                        CloudXArbiterPlatform.AdMob,
                        error?.GetMessage() ?? "AdMob returned no ad");
                    MaybePrepareWinner();
                    return;
                }

                _adMobInterstitial = ad;
                RegisterAdMobEvents(ad);
                _isLoadingAdMob = false;
                _adMobSettled = true;
                AdLoaded?.Invoke(CloudXArbiterPlatform.AdMob);
                MaybePrepareWinner();
            }));
    }

    private void RegisterAdMobEvents(InterstitialAd ad)
    {
        ad.OnAdFullScreenContentOpened += () => MobileAdsEventExecutor.ExecuteInUpdate(() =>
        {
            if (!_isDisposed)
            {
                AdShown?.Invoke(CloudXArbiterPlatform.AdMob);
            }
        });

        ad.OnAdFullScreenContentClosed += () => MobileAdsEventExecutor.ExecuteInUpdate(() =>
        {
            DestroyAdMobAd();
            _isShowing = false;

            if (!_isDisposed)
            {
                AdClosed?.Invoke(CloudXArbiterPlatform.AdMob);
            }
        });

        ad.OnAdFullScreenContentFailed += error => MobileAdsEventExecutor.ExecuteInUpdate(() =>
        {
            DestroyAdMobAd();
            _isShowing = false;

            if (!_isDisposed)
            {
                AdShowFailed?.Invoke(CloudXArbiterPlatform.AdMob, error.GetMessage());
            }
        });

        ad.OnAdClicked += () => MobileAdsEventExecutor.ExecuteInUpdate(() =>
        {
            if (!_isDisposed)
            {
                AdClicked?.Invoke(CloudXArbiterPlatform.AdMob);
            }
        });

        /* Required: this is how CloudX learns what the AdMob bid was worth. */
        ad.OnAdPaid += adValue => MobileAdsEventExecutor.ExecuteInUpdate(() =>
        {
            if (!_isDisposed)
            {
                ReportAdMobPaidEvent(adValue, AdSourceName(ad.GetResponseInfo()));
            }
        });
    }

    private void DestroyAdMobAd()
    {
        _adMobInterstitial?.Destroy();
        _adMobInterstitial = null;
    }

    private void ReportAdMobPaidEvent(AdValue adValue, string adSourceName)
    {
        if (!CloudXSdk.IsInitialized())
        {
            Log("AdMob paid event not forwarded: CloudX is not initialized");
            return;
        }

        var revenueData = new CloudXRevenueData(
            Platform: CloudXRevenuePlatform.AdMob,
            Revenue: adValue.Value / 1_000_000.0,
            AdFormat: AdFormatName,
            CurrencyCode: adValue.CurrencyCode,
            Precision: ToCloudXRevenuePrecision(adValue.Precision),
            NetworkName: adSourceName,
            AdUnitId: _adMobAdUnitId);
        var accepted = CloudXSdk.ReportRevenueData(revenueData);

        Log($"ReportRevenueData({adValue.Value} micros {adValue.CurrencyCode}, " +
            $"{adValue.Precision}) accepted={accepted}");
        RevenueReported?.Invoke(revenueData, accepted);
    }

    private static CloudXRevenuePrecision ToCloudXRevenuePrecision(
        AdValue.PrecisionType precision) =>
        precision switch
        {
            AdValue.PrecisionType.Precise => CloudXRevenuePrecision.Exact,
            AdValue.PrecisionType.Estimated => CloudXRevenuePrecision.Estimated,
            AdValue.PrecisionType.PublisherProvided => CloudXRevenuePrecision.PublisherDefined,
            _ => CloudXRevenuePrecision.Undefined,
        };

    private static string AdMobNetworkName(ResponseInfo responseInfo)
    {
        var adSourceName = AdSourceName(responseInfo);
        return string.IsNullOrWhiteSpace(adSourceName) ? "admob" : adSourceName;
    }

    private static string AdSourceName(ResponseInfo responseInfo) =>
        responseInfo?.GetLoadedAdapterResponseInfo()?.AdSourceName;

    private static string FailureText(CloudXError error) =>
        $"{error.Message} ({error.ErrorCodeName}[{error.ErrorCode}])";
}
