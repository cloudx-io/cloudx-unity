//
//  CLXUnityAdManager.h
//  CloudX Unity Plugin for iOS
//
//  Copyright (c) 2024 CloudX. All rights reserved.
//

#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <CloudXCore/CloudXCore.h>

NS_ASSUME_NONNULL_BEGIN

/// Callback function pointer for forwarding events to Unity
typedef void (*CLXUnityBackgroundCallback)(const char* args);

/**
 * CLXUnityAdManager manages all CloudX ads for the Unity plugin.
 * Singleton pattern matching AppLovin's MAUnityAdManager.
 *
 * SDK 2.0.0: CLXAdRevenueDelegate is now separate from main ad delegates.
 */
@interface CLXUnityAdManager : NSObject <CLXBannerDelegate, CLXInterstitialDelegate, CLXAppOpenDelegate, CLXRewardedDelegate, CLXAdRevenueDelegate>

#pragma mark - Ad Instance Storage

/// Banner ads indexed by ad unit ID
@property (nonatomic, strong, readonly) NSMutableDictionary<NSString *, CLXBannerAdView *> *banners;

/// MREC ads indexed by ad unit ID
@property (nonatomic, strong, readonly) NSMutableDictionary<NSString *, CLXBannerAdView *> *mrecs;

/// Interstitial ads indexed by ad unit ID
@property (nonatomic, strong, readonly) NSMutableDictionary<NSString *, CLXInterstitial *> *interstitials;

/// App open ads indexed by ad unit ID
@property (nonatomic, strong, readonly) NSMutableDictionary<NSString *, CLXAppOpen *> *appOpens;

/// Rewarded ads indexed by ad unit ID
@property (nonatomic, strong, readonly) NSMutableDictionary<NSString *, CLXRewarded *> *rewardedAds;

/// Banner position storage
@property (nonatomic, strong, readonly) NSMutableDictionary<NSString *, NSString *> *bannerPositions;

/// MREC position storage
@property (nonatomic, strong, readonly) NSMutableDictionary<NSString *, NSString *> *mrecPositions;

#pragma mark - Singleton

/// Returns the shared singleton instance
+ (instancetype)shared;

/// Sets the Unity callback for forwarding events
+ (void)setUnityBackgroundCallback:(CLXUnityBackgroundCallback)callback;

#pragma mark - SDK Initialization

/// Initialize the CloudX SDK
- (void)initializeWithAppKey:(NSString *)appKey pluginVersion:(NSString *)pluginVersion;

/// Check if SDK is initialized
- (BOOL)isInitialized;

#pragma mark - Configuration

/// Set the hashed user ID
- (void)setHashedUserId:(NSString *)hashedUserId;

/// Set a user-level key-value pair
- (void)setUserKeyValue:(NSString *)key value:(NSString *)value;

/// Set an app-level key-value pair
- (void)setAppKeyValue:(NSString *)key value:(NSString *)value;

/// Clear all key-value pairs
- (void)clearAllKeyValues;

/// Set minimum log level
- (void)setMinLogLevel:(NSString *)logLevel;

/// Set the manual GDPR consent override
- (void)setHasUserConsent:(nullable NSNumber *)hasUserConsent;

/// Set the manual CCPA do-not-sell override
- (void)setDoNotSell:(nullable NSNumber *)doNotSell;

/// Report publisher-observed impression-level revenue
- (BOOL)reportRevenueDataWithJson:(NSString *)json;

#pragma mark - Banner Methods

/// Create a banner ad
- (void)createBannerWithAdUnitId:(NSString *)adUnitId position:(NSString *)position;

/// Create a vertical banner ad rotated 90 degrees flush against the left or right safe-area edge (inset past any notch / Dynamic Island)
- (void)createVerticalBannerWithAdUnitId:(NSString *)adUnitId verticalPosition:(NSString *)verticalPosition;

/// Show a banner ad
- (void)showBannerWithAdUnitId:(NSString *)adUnitId;

/// Hide a banner ad
- (void)hideBannerWithAdUnitId:(NSString *)adUnitId;

/// Load a banner ad
- (void)loadBannerWithAdUnitId:(NSString *)adUnitId;

/// Start auto-refresh for a banner
- (void)startBannerAutoRefreshForAdUnitId:(NSString *)adUnitId;

/// Stop auto-refresh for a banner
- (void)stopBannerAutoRefreshForAdUnitId:(NSString *)adUnitId;

/// Set placement for a banner ad
- (void)setBannerPlacement:(NSString *)placement forAdUnit:(NSString *)adUnitId;

/// Set custom data for a banner ad
- (void)setBannerCustomData:(NSString *)customData forAdUnit:(NSString *)adUnitId;

/// Set an extra parameter for a banner ad
- (void)setBannerExtraParameter:(nullable id)value key:(NSString *)key forAdUnit:(NSString *)adUnitId;

/// Destroy a banner ad
- (void)destroyBannerWithAdUnitId:(NSString *)adUnitId;

#pragma mark - MREC Methods

/// Create an MREC ad
- (void)createMrecWithAdUnitId:(NSString *)adUnitId position:(NSString *)position;

/// Show an MREC ad
- (void)showMrecWithAdUnitId:(NSString *)adUnitId;

/// Hide an MREC ad
- (void)hideMrecWithAdUnitId:(NSString *)adUnitId;

/// Load an MREC ad
- (void)loadMrecWithAdUnitId:(NSString *)adUnitId;

/// Start auto-refresh for an MREC
- (void)startMrecAutoRefreshForAdUnitId:(NSString *)adUnitId;

/// Stop auto-refresh for an MREC
- (void)stopMrecAutoRefreshForAdUnitId:(NSString *)adUnitId;

/// Set placement for an MREC ad
- (void)setMrecPlacement:(NSString *)placement forAdUnit:(NSString *)adUnitId;

/// Set custom data for an MREC ad
- (void)setMrecCustomData:(NSString *)customData forAdUnit:(NSString *)adUnitId;

/// Set an extra parameter for an MREC ad
- (void)setMrecExtraParameter:(nullable id)value key:(NSString *)key forAdUnit:(NSString *)adUnitId;

/// Destroy an MREC ad
- (void)destroyMrecWithAdUnitId:(NSString *)adUnitId;

#pragma mark - Interstitial Methods

/// Load an interstitial ad
- (void)loadInterstitialWithAdUnitId:(NSString *)adUnitId;

/// Show an interstitial ad with optional placement and custom data
- (void)showInterstitialForAdUnitId:(NSString *)adUnitId
                            placement:(nullable NSString *)placement
                           customData:(nullable NSString *)customData;

/// Set an extra parameter for an interstitial ad
- (void)setInterstitialExtraParameter:(nullable id)value key:(NSString *)key forAdUnit:(NSString *)adUnitId;

/// Check if an interstitial is ready
- (BOOL)isInterstitialReadyWithAdUnitId:(NSString *)adUnitId;

/// Destroy an interstitial ad
- (void)destroyInterstitialWithAdUnitId:(NSString *)adUnitId;

#pragma mark - App Open Methods

/// Load an app open ad
- (void)loadAppOpenWithAdUnitId:(NSString *)adUnitId;

/// Show an app open ad with optional placement and custom data
- (void)showAppOpenForAdUnitId:(NSString *)adUnitId
                       placement:(nullable NSString *)placement
                      customData:(nullable NSString *)customData;

/// Set an extra parameter for an app open ad
- (void)setAppOpenExtraParameter:(nullable id)value key:(NSString *)key forAdUnit:(NSString *)adUnitId;

/// Check if an app open ad is ready
- (BOOL)isAppOpenReadyWithAdUnitId:(NSString *)adUnitId;

/// Destroy an app open ad
- (void)destroyAppOpenWithAdUnitId:(NSString *)adUnitId;

#pragma mark - Rewarded Methods

/// Load a rewarded ad
- (void)loadRewardedWithAdUnitId:(NSString *)adUnitId;

/// Show a rewarded ad with optional placement and custom data
- (void)showRewardedForAdUnitId:(NSString *)adUnitId
                        placement:(nullable NSString *)placement
                       customData:(nullable NSString *)customData;

/// Set an extra parameter for a rewarded ad
- (void)setRewardedExtraParameter:(nullable id)value key:(NSString *)key forAdUnit:(NSString *)adUnitId;

/// Check if a rewarded ad is ready
- (BOOL)isRewardedReadyWithAdUnitId:(NSString *)adUnitId;

/// Destroy a rewarded ad
- (void)destroyRewardedWithAdUnitId:(NSString *)adUnitId;

#pragma mark - Arbiter

/// Run a trusted-arbiter auction. callId is a caller-generated correlation id echoed
/// back in the OnArbiterCompletedEvent so concurrent calls can be routed correctly.
- (void)runArbiterWithCallId:(NSString *)callId bidsJson:(NSString *)bidsJson;

#pragma mark - Utilities

/// Serialize a dictionary to JSON string
+ (NSString *)serializeParameters:(NSDictionary<NSString *, id> *)dict;

/// Unavailable - use shared singleton
- (instancetype)init NS_UNAVAILABLE;

@end

NS_ASSUME_NONNULL_END
