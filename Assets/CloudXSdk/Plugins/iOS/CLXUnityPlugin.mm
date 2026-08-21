//
//  CLXUnityPlugin.mm
//  CloudX Unity Plugin for iOS
//
//  Copyright (c) 2024 CloudX. All rights reserved.
//
//  This file provides C-linkage functions that Unity can call via P/Invoke.
//  All functions are prefixed with _CLX to avoid naming conflicts.
//

#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wdeprecated-declarations"

#import "CLXUnityAdManager.h"
#import <CloudXCore/CloudXCore.h>

#define NSSTRING(_X) ((_X != NULL) ? [NSString stringWithCString:_X encoding:NSUTF8StringEncoding] : nil)

// When native code plugin is implemented in .mm / .cpp file, then functions
// should be surrounded with extern "C" block to conform C function naming rules
extern "C"
{
    static NSString *const TAG = @"CLXUnityPlugin";
    
    // Forward declarations for Unity functions
    UIView* UnityGetGLView(void);
    
    // Helper to get the ad manager singleton
    static CLXUnityAdManager* getAdManager()
    {
        return [CLXUnityAdManager shared];
    }
    
    // Helper to create C string copy (caller responsible for freeing)
    static const char* cStringCopy(NSString *string)
    {
        const char *value = string.UTF8String;
        return value ? strdup(value) : NULL;
    }
    
    // Helper to log errors
    static void clx_unity_log(NSString *message)
    {
        NSLog(@"[%@] %@", TAG, message);
    }
    
    #pragma mark - Callback Registration
    
    void _CLXSetBackgroundCallback(CLXUnityBackgroundCallback backgroundCallback)
    {
        [CLXUnityAdManager setUnityBackgroundCallback:backgroundCallback];
    }
    
    #pragma mark - SDK Initialization
    
    void _CLXInitialize(const char *appKey, const char *pluginVersion)
    {
        NSString *appKeyStr = NSSTRING(appKey);
        if (!appKeyStr) {
            clx_unity_log(@"Initialize failed: appKey is null");
            return;
        }

        NSString *pluginVersionStr = NSSTRING(pluginVersion);
        [getAdManager() initializeWithAppKey:appKeyStr pluginVersion:pluginVersionStr];
    }
    
    bool _CLXIsInitialized()
    {
        return [getAdManager() isInitialized];
    }
    
    #pragma mark - Configuration
    
    void _CLXSetHashedUserId(const char *hashedUserId)
    {
        NSString *userId = NSSTRING(hashedUserId);
        if (userId) {
            [getAdManager() setHashedUserId:userId];
        }
    }
    
    void _CLXSetUserKeyValue(const char *key, const char *value)
    {
        NSString *keyStr = NSSTRING(key);
        NSString *valueStr = NSSTRING(value);
        if (keyStr && valueStr) {
            [getAdManager() setUserKeyValue:keyStr value:valueStr];
        }
    }
    
    void _CLXSetAppKeyValue(const char *key, const char *value)
    {
        NSString *keyStr = NSSTRING(key);
        NSString *valueStr = NSSTRING(value);
        if (keyStr && valueStr) {
            [getAdManager() setAppKeyValue:keyStr value:valueStr];
        }
    }
    
    void _CLXClearAllKeyValues()
    {
        [getAdManager() clearAllKeyValues];
    }

    bool _CLXReportRevenueData(const char *json)
    {
        NSString *jsonString = NSSTRING(json);
        if (!jsonString) {
            clx_unity_log(@"reportRevenueData failed: json is null");
            return false;
        }

        return [getAdManager() reportRevenueDataWithJson:jsonString];
    }
    
    void _CLXSetMinLogLevel(const char *logLevel)
    {
        NSString *level = NSSTRING(logLevel);
        if (level) {
            [getAdManager() setMinLogLevel:level];
        }
    }

    void _CLXSetHasUserConsent(int hasValue, int hasUserConsent)
    {
        NSNumber *consentValue = hasValue != 0 ? @(hasUserConsent != 0) : nil;
        clx_unity_log([NSString stringWithFormat:@"setHasUserConsent: %@", consentValue ?: @"nil"]);
        [getAdManager() setHasUserConsent:consentValue];
    }

    void _CLXSetDoNotSell(int hasValue, int doNotSell)
    {
        NSNumber *doNotSellValue = hasValue != 0 ? @(doNotSell != 0) : nil;
        clx_unity_log([NSString stringWithFormat:@"setDoNotSell: %@", doNotSellValue ?: @"nil"]);
        [getAdManager() setDoNotSell:doNotSellValue];
    }
    
    #pragma mark - Banner Methods
    
    void _CLXCreateBanner(const char *adUnitId, const char *position)
    {
        NSString *adUnitIdString = NSSTRING(adUnitId);
        NSString *pos = NSSTRING(position);
        if (adUnitIdString) {
            [getAdManager() createBannerWithAdUnitId:adUnitIdString position:pos ?: @"Bottom"];
        }
    }

    void _CLXCreateVerticalBanner(const char *adUnitId, const char *verticalPosition)
    {
        NSString *adUnitIdString = NSSTRING(adUnitId);
        NSString *position = NSSTRING(verticalPosition);
        if (adUnitIdString) {
            [getAdManager() createVerticalBannerWithAdUnitId:adUnitIdString verticalPosition:position ?: @"Left"];
        }
    }
    
    void _CLXShowBanner(const char *adUnitId)
    {
        NSString *adUnitIdString = NSSTRING(adUnitId);
        if (adUnitIdString) {
            [getAdManager() showBannerWithAdUnitId:adUnitIdString];
        }
    }
    
    void _CLXHideBanner(const char *adUnitId)
    {
        NSString *adUnitIdString = NSSTRING(adUnitId);
        if (adUnitIdString) {
            [getAdManager() hideBannerWithAdUnitId:adUnitIdString];
        }
    }
    
    void _CLXLoadBanner(const char *adUnitId)
    {
        NSString *adUnitIdString = NSSTRING(adUnitId);
        if (adUnitIdString) {
            [getAdManager() loadBannerWithAdUnitId:adUnitIdString];
        }
    }
    
    void _CLXStartBannerAutoRefresh(const char *adUnitId)
    {
        NSString *adUnitIdString = NSSTRING(adUnitId);
        if (adUnitIdString) {
            [getAdManager() startBannerAutoRefreshForAdUnitId:adUnitIdString];
        }
    }
    
    void _CLXStopBannerAutoRefresh(const char *adUnitId)
    {
        NSString *adUnitIdString = NSSTRING(adUnitId);
        if (adUnitIdString) {
            [getAdManager() stopBannerAutoRefreshForAdUnitId:adUnitIdString];
        }
    }

    void _CLXSetBannerPlacement(const char *adUnitId, const char *placement)
    {
        NSString *adUnitIdStr = NSSTRING(adUnitId);
        if (adUnitIdStr) {
            [getAdManager() setBannerPlacement:NSSTRING(placement) forAdUnit:adUnitIdStr];
        }
    }

    void _CLXSetBannerCustomData(const char *adUnitId, const char *customData)
    {
        NSString *adUnitIdStr = NSSTRING(adUnitId);
        if (adUnitIdStr) {
            [getAdManager() setBannerCustomData:NSSTRING(customData) forAdUnit:adUnitIdStr];
        }
    }

    /*
     * Parses the enveloped JSON ({"value": <x>}) produced by the Unity layer and invokes apply
     * with the decoded value (NSDictionary / NSArray / NSNumber / NSString). A NULL json means
     * "clear" (apply nil). Malformed JSON is ignored so a previously-stored value is left
     * untouched, matching native "drop + log, never fail".
     */
    static void clx_apply_extra_parameter_json(const char *adUnitId,
                                               const char *key,
                                               const char *json,
                                               void (^apply)(NSString *adUnitIdStr, NSString *keyStr, id value))
    {
        NSString *adUnitIdStr = NSSTRING(adUnitId);
        NSString *keyStr = NSSTRING(key);
        if (!adUnitIdStr || !keyStr) {
            return;
        }

        if (json == NULL) {
            apply(adUnitIdStr, keyStr, nil);
            return;
        }

        NSData *data = [NSSTRING(json) dataUsingEncoding:NSUTF8StringEncoding];
        id root = data ? [NSJSONSerialization JSONObjectWithData:data options:0 error:nil] : nil;
        if (![root isKindOfClass:[NSDictionary class]]) {
            clx_unity_log([NSString stringWithFormat:@"Ignoring malformed extra parameter JSON for key %@", keyStr]);
            return;
        }

        id value = [(NSDictionary *)root objectForKey:@"value"];
        if (value == nil) {
            // A well-formed envelope always carries "value"; its absence means the input was not a
            // valid envelope, so treat it as malformed and leave prior state untouched (not "clear").
            clx_unity_log([NSString stringWithFormat:@"Ignoring extra parameter JSON without \"value\" for key %@", keyStr]);
            return;
        }
        if ([value isKindOfClass:[NSNull class]]) {
            value = nil;
        }
        apply(adUnitIdStr, keyStr, value);
    }

    void _CLXSetBannerExtraParameterJson(const char *adUnitId, const char *key, const char *json)
    {
        clx_apply_extra_parameter_json(adUnitId, key, json, ^(NSString *a, NSString *k, id v) {
            [getAdManager() setBannerExtraParameter:v key:k forAdUnit:a];
        });
    }

    void _CLXDestroyBanner(const char *adUnitId)
    {
        NSString *adUnitIdString = NSSTRING(adUnitId);
        if (adUnitIdString) {
            [getAdManager() destroyBannerWithAdUnitId:adUnitIdString];
        }
    }
    
    #pragma mark - MREC Methods
    
    void _CLXCreateMrec(const char *adUnitId, const char *position)
    {
        NSString *adUnitIdString = NSSTRING(adUnitId);
        NSString *pos = NSSTRING(position);
        if (adUnitIdString) {
            [getAdManager() createMrecWithAdUnitId:adUnitIdString position:pos ?: @"Center"];
        }
    }
    
    void _CLXShowMrec(const char *adUnitId)
    {
        NSString *adUnitIdString = NSSTRING(adUnitId);
        if (adUnitIdString) {
            [getAdManager() showMrecWithAdUnitId:adUnitIdString];
        }
    }
    
    void _CLXHideMrec(const char *adUnitId)
    {
        NSString *adUnitIdString = NSSTRING(adUnitId);
        if (adUnitIdString) {
            [getAdManager() hideMrecWithAdUnitId:adUnitIdString];
        }
    }
    
    void _CLXLoadMrec(const char *adUnitId)
    {
        NSString *adUnitIdString = NSSTRING(adUnitId);
        if (adUnitIdString) {
            [getAdManager() loadMrecWithAdUnitId:adUnitIdString];
        }
    }
    
    void _CLXStartMrecAutoRefresh(const char *adUnitId)
    {
        NSString *adUnitIdString = NSSTRING(adUnitId);
        if (adUnitIdString) {
            [getAdManager() startMrecAutoRefreshForAdUnitId:adUnitIdString];
        }
    }
    
    void _CLXStopMrecAutoRefresh(const char *adUnitId)
    {
        NSString *adUnitIdString = NSSTRING(adUnitId);
        if (adUnitIdString) {
            [getAdManager() stopMrecAutoRefreshForAdUnitId:adUnitIdString];
        }
    }

    void _CLXSetMrecPlacement(const char *adUnitId, const char *placement)
    {
        NSString *adUnitIdStr = NSSTRING(adUnitId);
        if (adUnitIdStr) {
            [getAdManager() setMrecPlacement:NSSTRING(placement) forAdUnit:adUnitIdStr];
        }
    }

    void _CLXSetMrecCustomData(const char *adUnitId, const char *customData)
    {
        NSString *adUnitIdStr = NSSTRING(adUnitId);
        if (adUnitIdStr) {
            [getAdManager() setMrecCustomData:NSSTRING(customData) forAdUnit:adUnitIdStr];
        }
    }

    void _CLXSetMrecExtraParameterJson(const char *adUnitId, const char *key, const char *json)
    {
        clx_apply_extra_parameter_json(adUnitId, key, json, ^(NSString *a, NSString *k, id v) {
            [getAdManager() setMrecExtraParameter:v key:k forAdUnit:a];
        });
    }

    void _CLXDestroyMrec(const char *adUnitId)
    {
        NSString *adUnitIdString = NSSTRING(adUnitId);
        if (adUnitIdString) {
            [getAdManager() destroyMrecWithAdUnitId:adUnitIdString];
        }
    }
    
    #pragma mark - Interstitial Methods
    
    void _CLXLoadInterstitial(const char *adUnitId)
    {
        NSString *adUnitIdString = NSSTRING(adUnitId);
        if (adUnitIdString) {
            [getAdManager() loadInterstitialWithAdUnitId:adUnitIdString];
        }
    }
    
    void _CLXShowInterstitial(const char *adUnitId, const char *placement, const char *customData)
    {
        NSString *adUnitIdStr = NSSTRING(adUnitId);
        if (adUnitIdStr) {
            [getAdManager() showInterstitialForAdUnitId:adUnitIdStr
                                                placement:NSSTRING(placement)
                                               customData:NSSTRING(customData)];
        }
    }

    void _CLXSetInterstitialExtraParameterJson(const char *adUnitId, const char *key, const char *json)
    {
        clx_apply_extra_parameter_json(adUnitId, key, json, ^(NSString *a, NSString *k, id v) {
            [getAdManager() setInterstitialExtraParameter:v key:k forAdUnit:a];
        });
    }
    
    bool _CLXIsInterstitialReady(const char *adUnitId)
    {
        NSString *adUnitIdString = NSSTRING(adUnitId);
        if (adUnitIdString) {
            return [getAdManager() isInterstitialReadyWithAdUnitId:adUnitIdString];
        }
        return false;
    }

    void _CLXDestroyInterstitial(const char *adUnitId)
    {
        NSString *adUnitIdString = NSSTRING(adUnitId);
        if (adUnitIdString) {
            [getAdManager() destroyInterstitialWithAdUnitId:adUnitIdString];
        }
    }

    #pragma mark - App Open Methods

    void _CLXLoadAppOpen(const char *adUnitId)
    {
        NSString *adUnitIdString = NSSTRING(adUnitId);
        if (adUnitIdString) {
            [getAdManager() loadAppOpenWithAdUnitId:adUnitIdString];
        }
    }

    void _CLXShowAppOpen(const char *adUnitId, const char *placement, const char *customData)
    {
        NSString *adUnitIdStr = NSSTRING(adUnitId);
        if (adUnitIdStr) {
            [getAdManager() showAppOpenForAdUnitId:adUnitIdStr
                                           placement:NSSTRING(placement)
                                          customData:NSSTRING(customData)];
        }
    }

    void _CLXSetAppOpenExtraParameterJson(const char *adUnitId, const char *key, const char *json)
    {
        clx_apply_extra_parameter_json(adUnitId, key, json, ^(NSString *a, NSString *k, id v) {
            [getAdManager() setAppOpenExtraParameter:v key:k forAdUnit:a];
        });
    }

    bool _CLXIsAppOpenReady(const char *adUnitId)
    {
        NSString *adUnitIdString = NSSTRING(adUnitId);
        if (adUnitIdString) {
            return [getAdManager() isAppOpenReadyWithAdUnitId:adUnitIdString];
        }
        return false;
    }

    void _CLXDestroyAppOpen(const char *adUnitId)
    {
        NSString *adUnitIdString = NSSTRING(adUnitId);
        if (adUnitIdString) {
            [getAdManager() destroyAppOpenWithAdUnitId:adUnitIdString];
        }
    }
    
    #pragma mark - Rewarded Methods
    
    void _CLXLoadRewarded(const char *adUnitId)
    {
        NSString *adUnitIdString = NSSTRING(adUnitId);
        if (adUnitIdString) {
            [getAdManager() loadRewardedWithAdUnitId:adUnitIdString];
        }
    }
    
    void _CLXShowRewarded(const char *adUnitId, const char *placement, const char *customData)
    {
        NSString *adUnitIdStr = NSSTRING(adUnitId);
        if (adUnitIdStr) {
            [getAdManager() showRewardedForAdUnitId:adUnitIdStr
                                            placement:NSSTRING(placement)
                                           customData:NSSTRING(customData)];
        }
    }

    void _CLXSetRewardedExtraParameterJson(const char *adUnitId, const char *key, const char *json)
    {
        clx_apply_extra_parameter_json(adUnitId, key, json, ^(NSString *a, NSString *k, id v) {
            [getAdManager() setRewardedExtraParameter:v key:k forAdUnit:a];
        });
    }
    
    bool _CLXIsRewardedReady(const char *adUnitId)
    {
        NSString *adUnitIdString = NSSTRING(adUnitId);
        if (adUnitIdString) {
            return [getAdManager() isRewardedReadyWithAdUnitId:adUnitIdString];
        }
        return false;
    }

    void _CLXDestroyRewarded(const char *adUnitId)
    {
        NSString *adUnitIdString = NSSTRING(adUnitId);
        if (adUnitIdString) {
            [getAdManager() destroyRewardedWithAdUnitId:adUnitIdString];
        }
    }
    
    #pragma mark - Utility Methods
    
    const char* _CLXGetSdkVersion()
    {
        NSString *version = [[CloudXCore shared] sdkVersion];
        return cStringCopy(version ?: @"unknown");
    }
    
    bool _CLXIsTablet()
    {
        return [UIDevice currentDevice].userInterfaceIdiom == UIUserInterfaceIdiomPad;
    }
    
    float _CLXScreenDensity()
    {
        return [UIScreen mainScreen].nativeScale;
    }
    
    #pragma mark - Visual Debugging
    
    void _CLXSetVisualDebuggingEnabled(bool enabled)
    {
        [CloudXCore setVisualDebuggingEnabled:enabled];
    }
    
    bool _CLXIsVisualDebuggingEnabled()
    {
        return [CloudXCore isVisualDebuggingEnabled];
    }

    #pragma mark - Arbiter

    void _CLXArbiter(const char* callId, const char* bidsJson)
    {
        [getAdManager() runArbiterWithCallId:NSSTRING(callId) ?: @""
                                    bidsJson:NSSTRING(bidsJson) ?: @"[]"];
    }
}

#pragma clang diagnostic pop
