//
//  CLXUnityAdManager.m
//  CloudX Unity Plugin for iOS
//
//  Copyright (c) 2024 CloudX. All rights reserved.
//

#import "CLXUnityAdManager.h"
#import <math.h>

#ifdef __cplusplus
extern "C" {
#endif
    // Unity functions
    UIViewController* UnityGetGLViewController(void);
    UIWindow* UnityGetMainWindow(void);
    
    void clx_unity_dispatch_on_main_thread(dispatch_block_t block)
    {
        if (block) {
            if ([NSThread isMainThread]) {
                block();
            } else {
                dispatch_async(dispatch_get_main_queue(), block);
            }
        }
    }
#ifdef __cplusplus
}
#endif

typedef NS_ENUM(NSInteger, AdLoadType) {
    AdLoadTypeBanner = 0,
    AdLoadTypeMrec = 1,
    AdLoadTypeInterstitial = 2,
    AdLoadTypeAppOpen = 3,
    AdLoadTypeRewarded = 4,
};

@interface CLXUnityAdManager ()

@property (nonatomic, strong, readwrite) NSMutableDictionary<NSString *, CLXBannerAdView *> *banners;
@property (nonatomic, strong, readwrite) NSMutableDictionary<NSString *, CLXBannerAdView *> *mrecs;
@property (nonatomic, strong, readwrite) NSMutableDictionary<NSString *, CLXInterstitial *> *interstitials;
@property (nonatomic, strong, readwrite) NSMutableDictionary<NSString *, CLXAppOpen *> *appOpens;
@property (nonatomic, strong, readwrite) NSMutableDictionary<NSString *, CLXRewarded *> *rewardedAds;
@property (nonatomic, strong, readwrite) NSMutableDictionary<NSString *, NSString *> *bannerPositions;
@property (nonatomic, strong, readwrite) NSMutableDictionary<NSString *, NSString *> *mrecPositions;
/*
 * Records whether each banner was created through the vertical entry point, so
 * the deferred attach in showBanner can apply the matching layout. Storing the
 * positioner blocks here instead would retain self through their captures.
 */
@property (nonatomic, strong, readwrite) NSMutableDictionary<NSString *, NSNumber *> *bannerVerticalKinds;

/// Tracks which ad type is currently loading (for error callbacks without ad instance)
/// TODO(CXD-610): Refactor Unity iOS load-failure routing to match Android bridge callback wiring.
@property (nonatomic, strong) NSMutableDictionary<NSString *, NSNumber *> *loadingAdTypes;
@property (nonatomic, strong) NSMutableSet<NSString *> *disabledAutoRefreshPlacements;
@property (nonatomic, strong) NSMutableDictionary<NSString *, NSString *> *pendingBannerPlacements;
@property (nonatomic, strong) NSMutableDictionary<NSString *, NSString *> *pendingBannerCustomData;
@property (nonatomic, strong) NSMutableDictionary<NSString *, NSMutableDictionary<NSString *, id> *> *pendingBannerExtraParameters;
@property (nonatomic, strong) NSMutableDictionary<NSString *, NSString *> *pendingMrecPlacements;
@property (nonatomic, strong) NSMutableDictionary<NSString *, NSString *> *pendingMrecCustomData;
@property (nonatomic, strong) NSMutableDictionary<NSString *, NSMutableDictionary<NSString *, id> *> *pendingMrecExtraParameters;
@property (nonatomic, strong) NSMutableDictionary<NSString *, NSMutableDictionary<NSString *, id> *> *pendingInterstitialExtraParameters;
@property (nonatomic, strong) NSMutableDictionary<NSString *, NSMutableDictionary<NSString *, id> *> *pendingAppOpenExtraParameters;
@property (nonatomic, strong) NSMutableDictionary<NSString *, NSMutableDictionary<NSString *, id> *> *pendingRewardedExtraParameters;
/*
 * Serial background queue every Unity callback is forwarded on. The C# side (CallbackDispatcher)
 * then either marshals to the Unity main thread or runs the publisher handler inline on this
 * queue thread, so a callback that CloudXSdk.InvokeEventsOnUnityMainThread resolves to "not on
 * the Unity main thread" is genuinely off it on iOS as well as Android.
 * maxConcurrentOperationCount = 1 keeps the forwarding order into C#; the order the publisher
 * observes can still differ when the C# side delivers some events inline on this queue thread
 * and marshals others to the next Update(). The queue is not suspended while the app is
 * inactive: main-thread work already waits in UnityMainThreadDispatcher until Unity resumes,
 * and Android has no such suspension either.
 */
@property (nonatomic, strong) NSOperationQueue *backgroundCallbackEventsQueue;

@end

static CLXUnityBackgroundCallback _backgroundCallback;
static NSString *const TAG = @"CLXUnityAdManager";

@implementation CLXUnityAdManager

#pragma mark - Singleton

+ (instancetype)shared {
    static CLXUnityAdManager *instance;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        instance = [[CLXUnityAdManager alloc] initPrivate];
    });
    return instance;
}

- (instancetype)initPrivate {
    if (self = [super init]) {
        _banners = [NSMutableDictionary new];
        _mrecs = [NSMutableDictionary new];
        _interstitials = [NSMutableDictionary new];
        _appOpens = [NSMutableDictionary new];
        _rewardedAds = [NSMutableDictionary new];
        _bannerPositions = [NSMutableDictionary new];
        _mrecPositions = [NSMutableDictionary new];
        _bannerVerticalKinds = [NSMutableDictionary new];
        _loadingAdTypes = [NSMutableDictionary new];
        _disabledAutoRefreshPlacements = [NSMutableSet new];
        _pendingBannerPlacements = [NSMutableDictionary new];
        _pendingBannerCustomData = [NSMutableDictionary new];
        _pendingBannerExtraParameters = [NSMutableDictionary new];
        _pendingMrecPlacements = [NSMutableDictionary new];
        _pendingMrecCustomData = [NSMutableDictionary new];
        _pendingMrecExtraParameters = [NSMutableDictionary new];
        _pendingInterstitialExtraParameters = [NSMutableDictionary new];
        _pendingAppOpenExtraParameters = [NSMutableDictionary new];
        _pendingRewardedExtraParameters = [NSMutableDictionary new];
        _backgroundCallbackEventsQueue = [[NSOperationQueue alloc] init];
        _backgroundCallbackEventsQueue.name = @"io.cloudx.unity.callbacks";
        _backgroundCallbackEventsQueue.maxConcurrentOperationCount = 1;
    }
    return self;
}

+ (void)setUnityBackgroundCallback:(CLXUnityBackgroundCallback)callback {
    _backgroundCallback = callback;
}

#pragma mark - SDK Initialization

- (void)initializeWithAppKey:(NSString *)appKey pluginVersion:(NSString *)pluginVersion {
    clx_unity_dispatch_on_main_thread(^{
        CLXInitializationConfiguration *config =
            [CLXInitializationConfiguration configurationWithAppKey:appKey
                builderBlock:^(CLXInitializationConfigurationBuilder *builder) {
                    builder.pluginVersion = pluginVersion;
                }];

        [[CloudXCore shared] initializeWithConfiguration:config completion:^(CLXSdkConfiguration * _Nullable sdkConfiguration, CLXError * _Nullable error) {
            BOOL success = (sdkConfiguration != nil);
            if (success) {
                [self forwardEventWithArgs:@{
                    @"name": @"OnSdkInitializedEvent",
                    @"success": @YES
                }];
            } else {
                [self log:@"SDK initialization failed: %@", error.localizedDescription];
                [self forwardEventWithArgs:@{
                    @"name": @"OnSdkInitializedEvent",
                    @"success": @NO,
                    @"errorCode": @(error.code),
                    @"errorMessage": error.localizedDescription ?: @""
                }];
            }
        }];
    });
}

- (BOOL)isInitialized {
    return [[CloudXCore shared] isInitialized];
}

#pragma mark - Configuration

- (void)setHashedUserId:(NSString *)hashedUserId {
    [[CloudXCore shared] setHashedUserID:hashedUserId];
}

- (void)setUserKeyValue:(NSString *)key value:(NSString *)value {
    [[CloudXCore shared] setUserKeyValue:key value:value];
}

- (void)setAppKeyValue:(NSString *)key value:(NSString *)value {
    [[CloudXCore shared] setAppKeyValue:key value:value];
}

- (void)clearAllKeyValues {
    [[CloudXCore shared] clearAllKeyValues];
}

- (void)setMinLogLevel:(NSString *)logLevel {
    CLXLogLevel level = CLXLogLevelInfo;
    
    if ([logLevel caseInsensitiveCompare:@"Verbose"] == NSOrderedSame) {
        level = CLXLogLevelVerbose;
    } else if ([logLevel caseInsensitiveCompare:@"Debug"] == NSOrderedSame) {
        level = CLXLogLevelDebug;
    } else if ([logLevel caseInsensitiveCompare:@"Info"] == NSOrderedSame) {
        level = CLXLogLevelInfo;
    } else if ([logLevel caseInsensitiveCompare:@"Warn"] == NSOrderedSame) {
        level = CLXLogLevelWarn;
    } else if ([logLevel caseInsensitiveCompare:@"Error"] == NSOrderedSame) {
        level = CLXLogLevelError;
    }
    
    [CloudXCore setMinLogLevel:level];
}

- (void)setHasUserConsent:(nullable NSNumber *)hasUserConsent {
    [CloudXCore setHasUserConsent:hasUserConsent];
}

- (void)setDoNotSell:(nullable NSNumber *)doNotSell {
    [CloudXCore setDoNotSell:doNotSell];
}

- (BOOL)reportRevenueDataWithJson:(NSString *)json {
    if (json.length == 0) {
        [self log:@"reportRevenueData: empty JSON"];
        return NO;
    }

    NSData *jsonData = [json dataUsingEncoding:NSUTF8StringEncoding];
    if (!jsonData) {
        [self log:@"reportRevenueData: empty JSON"];
        return NO;
    }

    NSError *parseError = nil;
    id parsed = [NSJSONSerialization JSONObjectWithData:jsonData options:0 error:&parseError];
    if (parseError || ![parsed isKindOfClass:[NSDictionary class]]) {
        [self log:@"reportRevenueData: failed to parse JSON: %@", parseError];
        return NO;
    }

    CLXRevenueData *revenueData = [self revenueDataFromDictionary:(NSDictionary *)parsed];
    if (!revenueData) {
        return NO;
    }

    BOOL accepted = [[CloudXCore shared] reportRevenueData:revenueData];
    [self log:@"reportRevenueData: native SDK returned %@", accepted ? @"YES" : @"NO"];
    return accepted;
}

#pragma mark - Banner Methods

- (void)createBannerWithAdUnitId:(NSString *)adUnitId position:(NSString *)position {
    [self createBannerWithAdUnitId:adUnitId
               positionDescription:position
                          vertical:NO];
}

- (void)createVerticalBannerWithAdUnitId:(NSString *)adUnitId verticalPosition:(NSString *)verticalPosition {
    [self createBannerWithAdUnitId:adUnitId
               positionDescription:verticalPosition
                          vertical:YES];
}

/*
 * Shared banner creation flow for the horizontal and vertical entry points.
 * positionDescription and vertical are stored so the deferred attach in
 * showBanner can lay the banner out with the matching positioner.
 */
- (void)createBannerWithAdUnitId:(NSString *)adUnitId
             positionDescription:(NSString *)positionDescription
                        vertical:(BOOL)vertical {
    clx_unity_dispatch_on_main_thread(^{
        BOOL shouldKeepAutoRefreshDisabled = [self.disabledAutoRefreshPlacements containsObject:adUnitId];
        if (self.banners[adUnitId]) {
            [self destroyBannerWithAdUnitId:adUnitId];
            if (shouldKeepAutoRefreshDisabled) {
                [self.disabledAutoRefreshPlacements addObject:adUnitId];
            }
        }

        UIViewController *viewController = [self unityViewController];
        CLXBannerAdView *banner = [[CloudXCore shared] createBannerWithAdUnitId:adUnitId
                                                                   viewController:viewController];

        if (banner) {
            banner.delegate = self;
            banner.revenueDelegate = self;
            self.banners[adUnitId] = banner;
            self.bannerPositions[adUnitId] = positionDescription;
            self.bannerVerticalKinds[adUnitId] = @(vertical);

            /*
             * Deliberately left detached from the view hierarchy until showBanner.
             * Loading while attached-but-hidden makes CloudXCore take a prefetch
             * path whose hidden->visible transition never inserts the adapter
             * subview into the container: CLXBannerAdView reaches
             * displayBannerIfNeeded: only from didLoadAd:, a wrapper rotation, or
             * didMoveToWindow, and -setHidden: triggers none of them. The banner
             * then stays blank until an unrelated event re-parents the view.
             * Loading detached takes the "display when added to a window" path, so
             * the addSubview: in showBanner renders the ad immediately.
             */
            NSString *pendingPlacement = self.pendingBannerPlacements[adUnitId];
            if (pendingPlacement) {
                [self log:@"Applying pending banner placement for \"%@\": %@", adUnitId, pendingPlacement];
                banner.placement = pendingPlacement;
                [self.pendingBannerPlacements removeObjectForKey:adUnitId];
            }

            NSString *pendingCustomData = self.pendingBannerCustomData[adUnitId];
            if (pendingCustomData) {
                [self log:@"Applying pending banner custom data for \"%@\"", adUnitId];
                banner.customData = pendingCustomData;
                [self.pendingBannerCustomData removeObjectForKey:adUnitId];
            }

            NSDictionary<NSString *, id> *pendingExtraParameters = self.pendingBannerExtraParameters[adUnitId];
            if (pendingExtraParameters) {
                for (NSString *key in pendingExtraParameters) {
                    [self log:@"Applying pending banner extra parameter for \"%@\": %@", adUnitId, key];
                    [banner setExtraParameter:key value:pendingExtraParameters[key]];
                }
                [self.pendingBannerExtraParameters removeObjectForKey:adUnitId];
            }

            self.loadingAdTypes[adUnitId] = @(AdLoadTypeBanner);
            [banner load];
            [banner stopAutoRefresh]; // Keep auto-refresh paused after the initial create-time load until the banner is shown.
        }
    });
}

/*
 * Attaches a created-but-detached ad view on first show. The addSubview: is what
 * makes CloudXCore render an already-loaded creative, so callers must clear
 * hidden before calling this. Positioning follows the attach because the layout
 * constraints need a common ancestor.
 */
- (void)attachBannerIfNeeded:(CLXBannerAdView *)banner adUnitId:(NSString *)adUnitId {
    if (banner.superview) {
        return;
    }

    UIViewController *viewController = [self unityViewController];
    if (!viewController) {
        [self log:@"Cannot attach banner for \"%@\": no Unity view controller", adUnitId];
        return;
    }

    [viewController.view addSubview:banner];

    NSString *positionDescription = self.bannerPositions[adUnitId];
    if ([self.bannerVerticalKinds[adUnitId] boolValue]) {
        [self positionVerticalAdView:banner verticalPosition:positionDescription];
    } else {
        [self positionAdView:banner position:positionDescription];
    }
}

- (void)attachMrecIfNeeded:(CLXBannerAdView *)mrec adUnitId:(NSString *)adUnitId {
    if (mrec.superview) {
        return;
    }

    UIViewController *viewController = [self unityViewController];
    if (!viewController) {
        [self log:@"Cannot attach MREC for \"%@\": no Unity view controller", adUnitId];
        return;
    }

    [viewController.view addSubview:mrec];
    [self positionAdView:mrec position:self.mrecPositions[adUnitId]];
}

- (void)showBannerWithAdUnitId:(NSString *)adUnitId {
    clx_unity_dispatch_on_main_thread(^{
        CLXBannerAdView *banner = self.banners[adUnitId];
        if (!banner) {
            return;
        }

        /*
         * Clear hidden BEFORE attaching. The attach fires didMoveToWindow, and
         * CloudXCore only renders there when the view is already unhidden, so
         * attaching first would blank a banner that was hidden before its first
         * show.
         */
        banner.hidden = NO;
        [self attachBannerIfNeeded:banner adUnitId:adUnitId];

        if (![self.disabledAutoRefreshPlacements containsObject:adUnitId]) {
            [banner startAutoRefresh];
        }
    });
}

- (void)hideBannerWithAdUnitId:(NSString *)adUnitId {
    clx_unity_dispatch_on_main_thread(^{
        CLXBannerAdView *banner = self.banners[adUnitId];
        if (!banner) {
            return;
        }

        banner.hidden = YES;
        [banner stopAutoRefresh];
    });
}

- (void)loadBannerWithAdUnitId:(NSString *)adUnitId {
    clx_unity_dispatch_on_main_thread(^{
        CLXBannerAdView *banner = self.banners[adUnitId];
        if (!banner) {
            return;
        }

        if (![self.disabledAutoRefreshPlacements containsObject:adUnitId]) {
            [self log:@"Auto-refresh is enabled for banner \"%@\". You must stop auto-refresh before calling loadBanner.", adUnitId];
            return;
        }

        self.loadingAdTypes[adUnitId] = @(AdLoadTypeBanner);
        [banner load];
    });
}

- (void)startBannerAutoRefreshForAdUnitId:(NSString *)adUnitId {
    clx_unity_dispatch_on_main_thread(^{
        [self.disabledAutoRefreshPlacements removeObject:adUnitId];
        CLXBannerAdView *banner = self.banners[adUnitId];
        if (!banner) {
            return;
        }

        [banner startAutoRefresh];
    });
}

- (void)stopBannerAutoRefreshForAdUnitId:(NSString *)adUnitId {
    clx_unity_dispatch_on_main_thread(^{
        [self.disabledAutoRefreshPlacements addObject:adUnitId];
        CLXBannerAdView *banner = self.banners[adUnitId];
        if (!banner) {
            return;
        }

        [banner stopAutoRefresh];
    });
}

- (void)setBannerPlacement:(NSString *)placement forAdUnit:(NSString *)adUnitId {
    clx_unity_dispatch_on_main_thread(^{
        CLXBannerAdView *banner = self.banners[adUnitId];
        if (!banner) {
            if (placement) {
                [self log:@"Queueing banner placement for \"%@\": %@", adUnitId, placement];
                self.pendingBannerPlacements[adUnitId] = placement;
            } else {
                [self log:@"Clearing pending banner placement for \"%@\"", adUnitId];
                [self.pendingBannerPlacements removeObjectForKey:adUnitId];
            }
            return;
        }

        banner.placement = placement;
    });
}

- (void)setBannerCustomData:(NSString *)customData forAdUnit:(NSString *)adUnitId {
    clx_unity_dispatch_on_main_thread(^{
        CLXBannerAdView *banner = self.banners[adUnitId];
        if (!banner) {
            if (customData) {
                [self log:@"Queueing banner custom data for \"%@\"", adUnitId];
                self.pendingBannerCustomData[adUnitId] = customData;
            } else {
                [self log:@"Clearing pending banner custom data for \"%@\"", adUnitId];
                [self.pendingBannerCustomData removeObjectForKey:adUnitId];
            }
            return;
        }

        banner.customData = customData;
    });
}

- (void)setBannerExtraParameter:(nullable id)value key:(NSString *)key forAdUnit:(NSString *)adUnitId {
    clx_unity_dispatch_on_main_thread(^{
        CLXBannerAdView *banner = self.banners[adUnitId];
        if (!banner) {
            if (value) {
                [self log:@"Queueing banner extra parameter for \"%@\": %@", adUnitId, key];
                NSMutableDictionary<NSString *, id> *parameters = self.pendingBannerExtraParameters[adUnitId];
                if (!parameters) {
                    parameters = [NSMutableDictionary new];
                    self.pendingBannerExtraParameters[adUnitId] = parameters;
                }
                parameters[key] = value;
            } else {
                [self log:@"Clearing pending banner extra parameter for \"%@\": %@", adUnitId, key];
                [self.pendingBannerExtraParameters[adUnitId] removeObjectForKey:key];
                if (self.pendingBannerExtraParameters[adUnitId].count == 0) {
                    [self.pendingBannerExtraParameters removeObjectForKey:adUnitId];
                }
            }
            return;
        }

        [banner setExtraParameter:key value:value];
    });
}

- (void)destroyBannerWithAdUnitId:(NSString *)adUnitId {
    clx_unity_dispatch_on_main_thread(^{
        CLXBannerAdView *banner = self.banners[adUnitId];
        if (!banner) {
            [self.bannerPositions removeObjectForKey:adUnitId];
            [self.bannerVerticalKinds removeObjectForKey:adUnitId];
            [self.loadingAdTypes removeObjectForKey:adUnitId];
            [self.disabledAutoRefreshPlacements removeObject:adUnitId];
            [self.pendingBannerPlacements removeObjectForKey:adUnitId];
            [self.pendingBannerCustomData removeObjectForKey:adUnitId];
            [self.pendingBannerExtraParameters removeObjectForKey:adUnitId];
            return;
        }

        [banner destroy];
        [banner removeFromSuperview];
        [self.banners removeObjectForKey:adUnitId];
        [self.bannerPositions removeObjectForKey:adUnitId];
        [self.bannerVerticalKinds removeObjectForKey:adUnitId];
        [self.loadingAdTypes removeObjectForKey:adUnitId];
        [self.disabledAutoRefreshPlacements removeObject:adUnitId];
        [self.pendingBannerPlacements removeObjectForKey:adUnitId];
        [self.pendingBannerCustomData removeObjectForKey:adUnitId];
        [self.pendingBannerExtraParameters removeObjectForKey:adUnitId];
    });
}

#pragma mark - MREC Methods

- (void)createMrecWithAdUnitId:(NSString *)adUnitId position:(NSString *)position {
    clx_unity_dispatch_on_main_thread(^{
        BOOL shouldKeepAutoRefreshDisabled = [self.disabledAutoRefreshPlacements containsObject:adUnitId];
        if (self.mrecs[adUnitId]) {
            [self destroyMrecWithAdUnitId:adUnitId];
            if (shouldKeepAutoRefreshDisabled) {
                [self.disabledAutoRefreshPlacements addObject:adUnitId];
            }
        }
        
        UIViewController *viewController = [self unityViewController];
        CLXBannerAdView *mrec = [[CloudXCore shared] createMRECWithAdUnitId:adUnitId
                                                              viewController:viewController];

        if (mrec) {
            mrec.delegate = self;
            mrec.revenueDelegate = self;
            self.mrecs[adUnitId] = mrec;
            self.mrecPositions[adUnitId] = position;

            /* Left detached until showMrec for the reason documented in createBanner. */
            NSString *pendingPlacement = self.pendingMrecPlacements[adUnitId];
            if (pendingPlacement) {
                [self log:@"Applying pending MREC placement for \"%@\": %@", adUnitId, pendingPlacement];
                mrec.placement = pendingPlacement;
                [self.pendingMrecPlacements removeObjectForKey:adUnitId];
            }

            NSString *pendingCustomData = self.pendingMrecCustomData[adUnitId];
            if (pendingCustomData) {
                [self log:@"Applying pending MREC custom data for \"%@\"", adUnitId];
                mrec.customData = pendingCustomData;
                [self.pendingMrecCustomData removeObjectForKey:adUnitId];
            }

            NSDictionary<NSString *, id> *pendingExtraParameters = self.pendingMrecExtraParameters[adUnitId];
            if (pendingExtraParameters) {
                for (NSString *key in pendingExtraParameters) {
                    [self log:@"Applying pending MREC extra parameter for \"%@\": %@", adUnitId, key];
                    [mrec setExtraParameter:key value:pendingExtraParameters[key]];
                }
                [self.pendingMrecExtraParameters removeObjectForKey:adUnitId];
            }

            self.loadingAdTypes[adUnitId] = @(AdLoadTypeMrec);
            [mrec load];
            [mrec stopAutoRefresh]; // Keep auto-refresh paused after the initial create-time load until the MREC is shown.
        }
    });
}

- (void)showMrecWithAdUnitId:(NSString *)adUnitId {
    clx_unity_dispatch_on_main_thread(^{
        CLXBannerAdView *mrec = self.mrecs[adUnitId];
        if (!mrec) {
            return;
        }

        /* Unhide before attaching, for the reason documented in showBanner. */
        mrec.hidden = NO;
        [self attachMrecIfNeeded:mrec adUnitId:adUnitId];

        if (![self.disabledAutoRefreshPlacements containsObject:adUnitId]) {
            [mrec startAutoRefresh];
        }
    });
}

- (void)hideMrecWithAdUnitId:(NSString *)adUnitId {
    clx_unity_dispatch_on_main_thread(^{
        CLXBannerAdView *mrec = self.mrecs[adUnitId];
        if (!mrec) {
            return;
        }

        mrec.hidden = YES;
        [mrec stopAutoRefresh];
    });
}

- (void)loadMrecWithAdUnitId:(NSString *)adUnitId {
    clx_unity_dispatch_on_main_thread(^{
        CLXBannerAdView *mrec = self.mrecs[adUnitId];
        if (!mrec) {
            return;
        }

        if (![self.disabledAutoRefreshPlacements containsObject:adUnitId]) {
            [self log:@"Auto-refresh is enabled for MREC \"%@\". You must stop auto-refresh before calling loadMrec.", adUnitId];
            return;
        }

        self.loadingAdTypes[adUnitId] = @(AdLoadTypeMrec);
        [mrec load];
    });
}

- (void)startMrecAutoRefreshForAdUnitId:(NSString *)adUnitId {
    clx_unity_dispatch_on_main_thread(^{
        [self.disabledAutoRefreshPlacements removeObject:adUnitId];
        CLXBannerAdView *mrec = self.mrecs[adUnitId];
        if (!mrec) {
            return;
        }

        [mrec startAutoRefresh];
    });
}

- (void)stopMrecAutoRefreshForAdUnitId:(NSString *)adUnitId {
    clx_unity_dispatch_on_main_thread(^{
        [self.disabledAutoRefreshPlacements addObject:adUnitId];
        CLXBannerAdView *mrec = self.mrecs[adUnitId];
        if (!mrec) {
            return;
        }

        [mrec stopAutoRefresh];
    });
}

- (void)setMrecPlacement:(NSString *)placement forAdUnit:(NSString *)adUnitId {
    clx_unity_dispatch_on_main_thread(^{
        CLXBannerAdView *mrec = self.mrecs[adUnitId];
        if (!mrec) {
            if (placement) {
                [self log:@"Queueing MREC placement for \"%@\": %@", adUnitId, placement];
                self.pendingMrecPlacements[adUnitId] = placement;
            } else {
                [self log:@"Clearing pending MREC placement for \"%@\"", adUnitId];
                [self.pendingMrecPlacements removeObjectForKey:adUnitId];
            }
            return;
        }

        mrec.placement = placement;
    });
}

- (void)setMrecCustomData:(NSString *)customData forAdUnit:(NSString *)adUnitId {
    clx_unity_dispatch_on_main_thread(^{
        CLXBannerAdView *mrec = self.mrecs[adUnitId];
        if (!mrec) {
            if (customData) {
                [self log:@"Queueing MREC custom data for \"%@\"", adUnitId];
                self.pendingMrecCustomData[adUnitId] = customData;
            } else {
                [self log:@"Clearing pending MREC custom data for \"%@\"", adUnitId];
                [self.pendingMrecCustomData removeObjectForKey:adUnitId];
            }
            return;
        }

        mrec.customData = customData;
    });
}

- (void)setMrecExtraParameter:(nullable id)value key:(NSString *)key forAdUnit:(NSString *)adUnitId {
    clx_unity_dispatch_on_main_thread(^{
        CLXBannerAdView *mrec = self.mrecs[adUnitId];
        if (!mrec) {
            if (value) {
                [self log:@"Queueing MREC extra parameter for \"%@\": %@", adUnitId, key];
                NSMutableDictionary<NSString *, id> *parameters = self.pendingMrecExtraParameters[adUnitId];
                if (!parameters) {
                    parameters = [NSMutableDictionary new];
                    self.pendingMrecExtraParameters[adUnitId] = parameters;
                }
                parameters[key] = value;
            } else {
                [self log:@"Clearing pending MREC extra parameter for \"%@\": %@", adUnitId, key];
                [self.pendingMrecExtraParameters[adUnitId] removeObjectForKey:key];
                if (self.pendingMrecExtraParameters[adUnitId].count == 0) {
                    [self.pendingMrecExtraParameters removeObjectForKey:adUnitId];
                }
            }
            return;
        }

        [mrec setExtraParameter:key value:value];
    });
}

- (void)destroyMrecWithAdUnitId:(NSString *)adUnitId {
    clx_unity_dispatch_on_main_thread(^{
        CLXBannerAdView *mrec = self.mrecs[adUnitId];
        if (!mrec) {
            [self.mrecPositions removeObjectForKey:adUnitId];
            [self.loadingAdTypes removeObjectForKey:adUnitId];
            [self.disabledAutoRefreshPlacements removeObject:adUnitId];
            [self.pendingMrecPlacements removeObjectForKey:adUnitId];
            [self.pendingMrecCustomData removeObjectForKey:adUnitId];
            [self.pendingMrecExtraParameters removeObjectForKey:adUnitId];
            return;
        }

        [mrec destroy];
        [mrec removeFromSuperview];
        [self.mrecs removeObjectForKey:adUnitId];
        [self.mrecPositions removeObjectForKey:adUnitId];
        [self.loadingAdTypes removeObjectForKey:adUnitId];
        [self.disabledAutoRefreshPlacements removeObject:adUnitId];
        [self.pendingMrecPlacements removeObjectForKey:adUnitId];
        [self.pendingMrecCustomData removeObjectForKey:adUnitId];
        [self.pendingMrecExtraParameters removeObjectForKey:adUnitId];
    });
}

#pragma mark - Interstitial Methods

- (void)loadInterstitialWithAdUnitId:(NSString *)adUnitId {
    clx_unity_dispatch_on_main_thread(^{
        CLXInterstitial *interstitial = self.interstitials[adUnitId];
        if (!interstitial) {
            interstitial = [[CloudXCore shared] createInterstitialWithAdUnitId:adUnitId];
            interstitial.delegate = self;
            interstitial.revenueDelegate = self;
            NSDictionary<NSString *, id> *pendingExtraParameters = self.pendingInterstitialExtraParameters[adUnitId];
            if (pendingExtraParameters) {
                for (NSString *key in pendingExtraParameters) {
                    [self log:@"Applying pending interstitial extra parameter for \"%@\": %@", adUnitId, key];
                    [interstitial setExtraParameter:key value:pendingExtraParameters[key]];
                }
                [self.pendingInterstitialExtraParameters removeObjectForKey:adUnitId];
            }
            self.interstitials[adUnitId] = interstitial;
        }
        
        self.loadingAdTypes[adUnitId] = @(AdLoadTypeInterstitial);
        [interstitial load];
    });
}

- (void)setInterstitialExtraParameter:(nullable id)value key:(NSString *)key forAdUnit:(NSString *)adUnitId {
    clx_unity_dispatch_on_main_thread(^{
        CLXInterstitial *interstitial = self.interstitials[adUnitId];
        if (!interstitial) {
            if (value) {
                [self log:@"Queueing interstitial extra parameter for \"%@\": %@", adUnitId, key];
                NSMutableDictionary<NSString *, id> *parameters = self.pendingInterstitialExtraParameters[adUnitId];
                if (!parameters) {
                    parameters = [NSMutableDictionary new];
                    self.pendingInterstitialExtraParameters[adUnitId] = parameters;
                }
                parameters[key] = value;
            } else {
                [self log:@"Clearing pending interstitial extra parameter for \"%@\": %@", adUnitId, key];
                [self.pendingInterstitialExtraParameters[adUnitId] removeObjectForKey:key];
                if (self.pendingInterstitialExtraParameters[adUnitId].count == 0) {
                    [self.pendingInterstitialExtraParameters removeObjectForKey:adUnitId];
                }
            }
            return;
        }

        [interstitial setExtraParameter:key value:value];
    });
}

- (void)showInterstitialForAdUnitId:(NSString *)adUnitId
                            placement:(NSString * _Nullable)placement
                           customData:(NSString * _Nullable)customData {
    clx_unity_dispatch_on_main_thread(^{
        CLXInterstitial *interstitial = self.interstitials[adUnitId];
        if (!interstitial) {
            [self log:@"showInterstitialForAdUnitId dropped: no interstitial tracked for adUnitId=%@", adUnitId];
            return;
        }
        /* No readiness check here: the core reports a not-ready show itself via
           didFailToDisplayAd: with AD_NOT_READY (400), the same behavior as Android. */
        UIViewController *viewController = [self unityViewController];
        [interstitial showFromViewController:viewController placement:placement customData:customData];
    });
}

- (BOOL)isInterstitialReadyWithAdUnitId:(NSString *)adUnitId {
    CLXInterstitial *interstitial = self.interstitials[adUnitId];
    return interstitial ? [interstitial isReady] : NO;
}

- (void)destroyInterstitialWithAdUnitId:(NSString *)adUnitId {
    clx_unity_dispatch_on_main_thread(^{
        CLXInterstitial *interstitial = self.interstitials[adUnitId];
        if (!interstitial) {
            [self.loadingAdTypes removeObjectForKey:adUnitId];
            [self.pendingInterstitialExtraParameters removeObjectForKey:adUnitId];
            return;
        }

        interstitial.delegate = nil;
        interstitial.revenueDelegate = nil;
        [interstitial destroy];
        [self.interstitials removeObjectForKey:adUnitId];
        [self.loadingAdTypes removeObjectForKey:adUnitId];
        [self.pendingInterstitialExtraParameters removeObjectForKey:adUnitId];
    });
}

#pragma mark - App Open Methods

- (void)loadAppOpenWithAdUnitId:(NSString *)adUnitId {
    clx_unity_dispatch_on_main_thread(^{
        CLXAppOpen *appOpen = self.appOpens[adUnitId];
        if (!appOpen) {
            appOpen = [[CloudXCore shared] createAppOpenWithAdUnitId:adUnitId];
            appOpen.delegate = self;
            appOpen.revenueDelegate = self;
            NSDictionary<NSString *, id> *pendingExtraParameters = self.pendingAppOpenExtraParameters[adUnitId];
            if (pendingExtraParameters) {
                for (NSString *key in pendingExtraParameters) {
                    [self log:@"Applying pending app open extra parameter for \"%@\": %@", adUnitId, key];
                    [appOpen setExtraParameter:key value:pendingExtraParameters[key]];
                }
                [self.pendingAppOpenExtraParameters removeObjectForKey:adUnitId];
            }
            self.appOpens[adUnitId] = appOpen;
        }

        self.loadingAdTypes[adUnitId] = @(AdLoadTypeAppOpen);
        [appOpen load];
    });
}

- (void)setAppOpenExtraParameter:(nullable id)value key:(NSString *)key forAdUnit:(NSString *)adUnitId {
    clx_unity_dispatch_on_main_thread(^{
        CLXAppOpen *appOpen = self.appOpens[adUnitId];
        if (!appOpen) {
            if (value) {
                [self log:@"Queueing app open extra parameter for \"%@\": %@", adUnitId, key];
                NSMutableDictionary<NSString *, id> *parameters = self.pendingAppOpenExtraParameters[adUnitId];
                if (!parameters) {
                    parameters = [NSMutableDictionary new];
                    self.pendingAppOpenExtraParameters[adUnitId] = parameters;
                }
                parameters[key] = value;
            } else {
                [self log:@"Clearing pending app open extra parameter for \"%@\": %@", adUnitId, key];
                [self.pendingAppOpenExtraParameters[adUnitId] removeObjectForKey:key];
                if (self.pendingAppOpenExtraParameters[adUnitId].count == 0) {
                    [self.pendingAppOpenExtraParameters removeObjectForKey:adUnitId];
                }
            }
            return;
        }

        [appOpen setExtraParameter:key value:value];
    });
}

- (void)showAppOpenForAdUnitId:(NSString *)adUnitId
                       placement:(NSString * _Nullable)placement
                      customData:(NSString * _Nullable)customData {
    clx_unity_dispatch_on_main_thread(^{
        CLXAppOpen *appOpen = self.appOpens[adUnitId];
        if (!appOpen) {
            [self log:@"showAppOpenForAdUnitId dropped: no app open tracked for adUnitId=%@", adUnitId];
            return;
        }
        /* No readiness check here: the core reports a not-ready show itself via
           didFailToDisplayAd: with AD_NOT_READY (400), the same behavior as Android. */
        UIViewController *viewController = [self unityViewController];
        [appOpen showFromViewController:viewController placement:placement customData:customData];
    });
}

- (BOOL)isAppOpenReadyWithAdUnitId:(NSString *)adUnitId {
    CLXAppOpen *appOpen = self.appOpens[adUnitId];
    return appOpen ? [appOpen isReady] : NO;
}

- (void)destroyAppOpenWithAdUnitId:(NSString *)adUnitId {
    clx_unity_dispatch_on_main_thread(^{
        CLXAppOpen *appOpen = self.appOpens[adUnitId];
        if (!appOpen) {
            [self.loadingAdTypes removeObjectForKey:adUnitId];
            [self.pendingAppOpenExtraParameters removeObjectForKey:adUnitId];
            return;
        }

        appOpen.delegate = nil;
        appOpen.revenueDelegate = nil;
        [appOpen destroy];
        [self.appOpens removeObjectForKey:adUnitId];
        [self.loadingAdTypes removeObjectForKey:adUnitId];
        [self.pendingAppOpenExtraParameters removeObjectForKey:adUnitId];
    });
}

#pragma mark - Rewarded Methods

- (void)loadRewardedWithAdUnitId:(NSString *)adUnitId {
    clx_unity_dispatch_on_main_thread(^{
        CLXRewarded *rewarded = self.rewardedAds[adUnitId];
        if (!rewarded) {
            rewarded = [[CloudXCore shared] createRewardedWithAdUnitId:adUnitId];
            rewarded.delegate = self;
            rewarded.revenueDelegate = self;
            NSDictionary<NSString *, id> *pendingExtraParameters = self.pendingRewardedExtraParameters[adUnitId];
            if (pendingExtraParameters) {
                for (NSString *key in pendingExtraParameters) {
                    [self log:@"Applying pending rewarded extra parameter for \"%@\": %@", adUnitId, key];
                    [rewarded setExtraParameter:key value:pendingExtraParameters[key]];
                }
                [self.pendingRewardedExtraParameters removeObjectForKey:adUnitId];
            }
            self.rewardedAds[adUnitId] = rewarded;
        }
        
        self.loadingAdTypes[adUnitId] = @(AdLoadTypeRewarded);
        [rewarded load];
    });
}

- (void)setRewardedExtraParameter:(nullable id)value key:(NSString *)key forAdUnit:(NSString *)adUnitId {
    clx_unity_dispatch_on_main_thread(^{
        CLXRewarded *rewarded = self.rewardedAds[adUnitId];
        if (!rewarded) {
            if (value) {
                [self log:@"Queueing rewarded extra parameter for \"%@\": %@", adUnitId, key];
                NSMutableDictionary<NSString *, id> *parameters = self.pendingRewardedExtraParameters[adUnitId];
                if (!parameters) {
                    parameters = [NSMutableDictionary new];
                    self.pendingRewardedExtraParameters[adUnitId] = parameters;
                }
                parameters[key] = value;
            } else {
                [self log:@"Clearing pending rewarded extra parameter for \"%@\": %@", adUnitId, key];
                [self.pendingRewardedExtraParameters[adUnitId] removeObjectForKey:key];
                if (self.pendingRewardedExtraParameters[adUnitId].count == 0) {
                    [self.pendingRewardedExtraParameters removeObjectForKey:adUnitId];
                }
            }
            return;
        }

        [rewarded setExtraParameter:key value:value];
    });
}

- (void)showRewardedForAdUnitId:(NSString *)adUnitId
                        placement:(NSString * _Nullable)placement
                       customData:(NSString * _Nullable)customData {
    clx_unity_dispatch_on_main_thread(^{
        CLXRewarded *rewarded = self.rewardedAds[adUnitId];
        if (!rewarded) {
            [self log:@"showRewardedForAdUnitId dropped: no rewarded tracked for adUnitId=%@", adUnitId];
            return;
        }
        /* No readiness check here: the core reports a not-ready show itself via
           didFailToDisplayAd: with AD_NOT_READY (400), the same behavior as Android. */
        UIViewController *viewController = [self unityViewController];
        [rewarded showFromViewController:viewController placement:placement customData:customData];
    });
}

- (BOOL)isRewardedReadyWithAdUnitId:(NSString *)adUnitId {
    CLXRewarded *rewarded = self.rewardedAds[adUnitId];
    return rewarded ? [rewarded isReady] : NO;
}

- (void)destroyRewardedWithAdUnitId:(NSString *)adUnitId {
    clx_unity_dispatch_on_main_thread(^{
        CLXRewarded *rewarded = self.rewardedAds[adUnitId];
        if (!rewarded) {
            [self.loadingAdTypes removeObjectForKey:adUnitId];
            [self.pendingRewardedExtraParameters removeObjectForKey:adUnitId];
            return;
        }

        rewarded.delegate = nil;
        rewarded.revenueDelegate = nil;
        [rewarded destroy];
        [self.rewardedAds removeObjectForKey:adUnitId];
        [self.loadingAdTypes removeObjectForKey:adUnitId];
        [self.pendingRewardedExtraParameters removeObjectForKey:adUnitId];
    });
}

#pragma mark - CLXAdDelegate (Common callbacks)

- (void)didLoadAd:(CLXAd *)ad {
    NSString *adUnitId = ad.adUnitId;
    NSString *eventName = [self unityEventNameForAd:ad
                                             suffix:@"AdLoadedEvent"
                                    allowedPrefixes:@[@"Banner", @"Mrec", @"Interstitial", @"AppOpen", @"Rewarded"]
                                           callback:@"didLoadAd"];
    if (eventName) {
        [self forwardAdEventWithName:eventName ad:ad adUnitId:adUnitId];
    }

    [self.loadingAdTypes removeObjectForKey:adUnitId];
}

// SDK 2.0.0: didFailToLoadAd now includes the failing ad unit ID directly
- (void)didFailToLoadAd:(NSString *)adUnitId error:(CLXError *)error {
    NSString *eventName = nil;

    NSNumber *adTypeNumber = self.loadingAdTypes[adUnitId];
    if (adTypeNumber) {
        switch ((AdLoadType)adTypeNumber.integerValue) {
            case AdLoadTypeBanner:
                eventName = @"OnBannerAdLoadFailedEvent";
                break;
            case AdLoadTypeMrec:
                eventName = @"OnMrecAdLoadFailedEvent";
                break;
            case AdLoadTypeInterstitial:
                eventName = @"OnInterstitialAdLoadFailedEvent";
                break;
            case AdLoadTypeAppOpen:
                eventName = @"OnAppOpenAdLoadFailedEvent";
                break;
            case AdLoadTypeRewarded:
                eventName = @"OnRewardedAdLoadFailedEvent";
                break;
        }
    }

    /*
     * loadingAdTypes only tracks Unity-initiated loads and is cleared on the first result, so
     * a native-initiated reload (a banner refresh failing after a successful first load) misses
     * it. The tracked-ad dictionaries still know the ad, so route by them, as the CLXAd-based
     * callbacks do. This callback has no CLXAd, so there is no format fallback beyond that.
     */
    if (!eventName && adUnitId) {
        for (NSString *prefix in @[@"Banner", @"Mrec", @"Interstitial", @"AppOpen", @"Rewarded"]) {
            if ([self trackedAdsForPrefix:prefix][adUnitId]) {
                eventName = [NSString stringWithFormat:@"On%@AdLoadFailedEvent", prefix];
                [self log:@"didFailToLoadAd: no pending load type for adUnitId=%@, routing by tracked %@ ad", adUnitId, prefix];
                break;
            }
        }
    }

    if (!eventName) {
        /* The C# side has no handler for an un-prefixed load failure; forwarding one would only trip its unhandled-event error. */
        [self log:@"didFailToLoadAd dropped: no pending load type and no tracked ad for adUnitId=%@ (code=%ld %@)",
                  adUnitId, (long)error.code, error.localizedDescription];
    } else {
        [self forwardEventWithArgs:@{
            @"name": eventName,
            @"adUnitId": adUnitId ?: @"",
            @"errorCode": @(error.code),
            @"errorMessage": error.localizedDescription ?: @""
        }];
    }

    if (adUnitId) {
        [self.loadingAdTypes removeObjectForKey:adUnitId];
    }
}

// CLXFullscreenAdDelegate - only called for interstitial/rewarded ads
// Note: Banners/MRECs don't have display lifecycle callbacks in iOS SDK 2.0.0
- (void)didDisplayAd:(CLXAd *)ad {
    NSString *eventName = [self unityEventNameForAd:ad
                                             suffix:@"AdDisplayedEvent"
                                    allowedPrefixes:@[@"Interstitial", @"AppOpen", @"Rewarded"]
                                           callback:@"didDisplayAd"];
    if (eventName) {
        [self forwardAdEventWithName:eventName ad:ad adUnitId:ad.adUnitId];
    }
}

// CLXFullscreenAdDelegate - only called for interstitial/rewarded ads
// Note: Banners/MRECs don't have display lifecycle callbacks in iOS SDK 2.0.0
- (void)didFailToDisplayAd:(CLXAd *)ad error:(CLXError *)error {
    NSString *eventName = [self unityEventNameForAd:ad
                                             suffix:@"AdDisplayFailedEvent"
                                    allowedPrefixes:@[@"Interstitial", @"AppOpen", @"Rewarded"]
                                           callback:@"didFailToDisplayAd"];
    if (eventName) {
        [self forwardAdEventWithName:eventName ad:ad adUnitId:ad.adUnitId error:error];
    }
}

// CLXFullscreenAdDelegate - only called for interstitial/rewarded ads
// Note: Banners/MRECs don't have display lifecycle callbacks in iOS SDK 2.0.0
- (void)didHideAd:(CLXAd *)ad {
    NSString *eventName = [self unityEventNameForAd:ad
                                             suffix:@"AdHiddenEvent"
                                    allowedPrefixes:@[@"Interstitial", @"AppOpen", @"Rewarded"]
                                           callback:@"didHideAd"];
    if (eventName) {
        [self forwardAdEventWithName:eventName ad:ad adUnitId:ad.adUnitId];
    }
}

- (void)didClickAd:(CLXAd *)ad {
    NSString *eventName = [self unityEventNameForAd:ad
                                             suffix:@"AdClickedEvent"
                                    allowedPrefixes:@[@"Banner", @"Mrec", @"Interstitial", @"AppOpen", @"Rewarded"]
                                           callback:@"didClickAd"];
    if (eventName) {
        [self forwardAdEventWithName:eventName ad:ad adUnitId:ad.adUnitId];
    }
}

// SDK 2.0.0: didRecordImpressionForAd removed - impressions now tracked via didPayRevenueForAd
// CLXAdRevenueDelegate implementation
- (void)didPayRevenueForAd:(CLXAd *)ad {
    NSString *eventName = [self unityEventNameForAd:ad
                                             suffix:@"AdRevenuePaidEvent"
                                    allowedPrefixes:@[@"Banner", @"Mrec", @"Interstitial", @"AppOpen", @"Rewarded"]
                                           callback:@"didPayRevenueForAd"];
    if (eventName) {
        [self forwardAdEventWithName:eventName ad:ad adUnitId:ad.adUnitId];
    }
}

#pragma mark - CLXRewardedDelegate

// SDK 2.0.0: Uses didRewardUserForAd:withReward: with CLXReward object
- (void)didRewardUserForAd:(CLXAd *)ad withReward:(CLXReward *)reward {
    NSString *adUnitId = ad.adUnitId;
    
    NSInteger rewardAmount = reward.amount;
    NSString *rewardLabel = reward.label ?: @"Reward";
    
    [self forwardRewardEventWithName:@"OnRewardedAdRewardedEvent" ad:ad adUnitId:adUnitId rewardAmount:rewardAmount rewardLabel:rewardLabel];
}

#pragma mark - CLXBannerDelegate

- (void)didExpandAd:(CLXAd *)ad {
    NSString *eventName = [self unityEventNameForAd:ad
                                             suffix:@"AdExpandedEvent"
                                    allowedPrefixes:@[@"Banner", @"Mrec"]
                                           callback:@"didExpandAd"];
    if (eventName) {
        [self forwardAdEventWithName:eventName ad:ad adUnitId:ad.adUnitId];
    }
}

- (void)didCollapseAd:(CLXAd *)ad {
    NSString *eventName = [self unityEventNameForAd:ad
                                             suffix:@"AdCollapsedEvent"
                                    allowedPrefixes:@[@"Banner", @"Mrec"]
                                           callback:@"didCollapseAd"];
    if (eventName) {
        [self forwardAdEventWithName:eventName ad:ad adUnitId:ad.adUnitId];
    }
}

#pragma mark - Event Routing

/*
 * Resolves the Unity event name for a delegate callback as On<prefix><suffix>.
 * The tracked-ad dictionaries are authoritative: each allowed prefix's dictionary is
 * probed by the ad unit id in array order, preserving the historical per-callback
 * routing. When no dictionary tracks the ad, the ad's own format routes it instead so
 * the event is not lost; that fallback is logged, because a miss means the ad unit
 * string is not one this manager created. Returns nil, after logging the drop, when
 * the format is not routable for this callback either.
 */
- (nullable NSString *)unityEventNameForAd:(CLXAd *)ad
                                    suffix:(NSString *)suffix
                           allowedPrefixes:(NSArray<NSString *> *)allowedPrefixes
                                  callback:(NSString *)callback {
    if (!ad) {
        /* A nil ad would read adFormat as 0 (Banner) and fabricate a banner event. */
        [self log:@"%@ dropped: callback delivered a nil ad", callback];
        return nil;
    }

    NSString *adUnitId = ad.adUnitId;
    if (adUnitId) {
        for (NSString *prefix in allowedPrefixes) {
            if ([self trackedAdsForPrefix:prefix][adUnitId]) {
                return [NSString stringWithFormat:@"On%@%@", prefix, suffix];
            }
        }
    }

    NSString *formatPrefix = [self eventPrefixFromAdFormat:ad.adFormat];
    if (formatPrefix && [allowedPrefixes containsObject:formatPrefix]) {
        [self log:@"%@: no tracked ad for adUnitId=%@, routing by ad format %@ (tracked: banners=%@ mrecs=%@ interstitials=%@ appOpens=%@ rewarded=%@)",
                  callback, adUnitId, formatPrefix,
                  self.banners.allKeys, self.mrecs.allKeys, self.interstitials.allKeys,
                  self.appOpens.allKeys, self.rewardedAds.allKeys];
        return [NSString stringWithFormat:@"On%@%@", formatPrefix, suffix];
    }

    [self log:@"%@ dropped: no tracked ad and no routable format for adUnitId=%@ format=%@",
              callback, adUnitId, [self stringFromAdFormat:ad.adFormat]];
    return nil;
}

- (nullable NSDictionary *)trackedAdsForPrefix:(NSString *)prefix {
    if ([prefix isEqualToString:@"Banner"]) return self.banners;
    if ([prefix isEqualToString:@"Mrec"]) return self.mrecs;
    if ([prefix isEqualToString:@"Interstitial"]) return self.interstitials;
    if ([prefix isEqualToString:@"AppOpen"]) return self.appOpens;
    if ([prefix isEqualToString:@"Rewarded"]) return self.rewardedAds;
    return nil;
}

/*
 * Routing counterpart of stringFromAdFormat:. Kept separate because that method's
 * default case reports @"Interstitial", which must never route an unknown or
 * non-routable format (such as Native) into interstitial events.
 */
- (nullable NSString *)eventPrefixFromAdFormat:(CLXAdFormat)adFormat {
    switch (adFormat) {
        case CLXAdFormatBanner: return @"Banner";
        case CLXAdFormatMREC: return @"Mrec";
        case CLXAdFormatInterstitial: return @"Interstitial";
        case CLXAdFormatAppOpen: return @"AppOpen";
        case CLXAdFormatRewarded: return @"Rewarded";
        default: return nil;
    }
}

#pragma mark - Event Forwarding

- (void)forwardAdEventWithName:(NSString *)name ad:(CLXAd *)ad adUnitId:(NSString *)adUnitId {
    NSMutableDictionary *args = [NSMutableDictionary dictionary];
    args[@"name"] = name;
    args[@"adUnitId"] = adUnitId ?: @"";
    [self addAdFieldsToArgs:args ad:ad];

    [self forwardEventWithArgs:args];
}

- (void)forwardAdEventWithName:(NSString *)name ad:(CLXAd *)ad adUnitId:(NSString *)adUnitId error:(CLXError *)error {
    NSMutableDictionary *args = [NSMutableDictionary dictionary];
    args[@"name"] = name;
    args[@"adUnitId"] = adUnitId ?: @"";
    [self addAdFieldsToArgs:args ad:ad];

    if (error) {
        args[@"errorCode"] = @(error.code);
        args[@"errorMessage"] = error.localizedDescription ?: @"";
    }

    [self forwardEventWithArgs:args];
}

/*
 * Adds the common ad-derived fields shared by multiple Unity callback payloads.
 * The caller owns event-specific fields such as `name` and `adUnitId`; this helper
 * appends the rest of the ad metadata without changing those caller-provided values.
 * Current fields added here are: `adFormat`, `networkName`, `placement`,
 * `networkPlacement`, and `revenue`.
 */
- (void)addAdFieldsToArgs:(NSMutableDictionary *)args ad:(CLXAd *)ad {
    if (!ad) {
        return;
    }

    args[@"adFormat"] = [self stringFromAdFormat:ad.adFormat];
    args[@"networkName"] = ad.networkName;

    // Optional fields - only add if non-nil
    if (ad.placement) {
        args[@"placement"] = ad.placement;
    }
    if (ad.networkPlacement) {
        args[@"networkPlacement"] = ad.networkPlacement;
    }

    args[@"revenue"] = ad.revenue ?: @0;
}

- (NSString *)stringFromAdFormat:(CLXAdFormat)adFormat {
    switch (adFormat) {
        case CLXAdFormatBanner: return @"Banner";
        case CLXAdFormatMREC: return @"Mrec";
        case CLXAdFormatInterstitial: return @"Interstitial";
        case CLXAdFormatAppOpen: return @"AppOpen";
        case CLXAdFormatRewarded: return @"Rewarded";
        case CLXAdFormatNative: return @"Native";
        default: return @"Interstitial";
    }
}

- (void)forwardRewardEventWithName:(NSString *)name ad:(CLXAd *)ad adUnitId:(NSString *)adUnitId rewardAmount:(NSInteger)rewardAmount rewardLabel:(NSString *)rewardLabel {
    NSMutableDictionary *args = [NSMutableDictionary dictionary];
    args[@"name"] = name;
    args[@"adUnitId"] = adUnitId ?: @"";
    [self addAdFieldsToArgs:args ad:ad];

    // Add reward info
    args[@"rewardAmount"] = @(rewardAmount);
    args[@"rewardLabel"] = rewardLabel ?: @"Reward";

    [self forwardEventWithArgs:args];
}

- (void)forwardEventWithArgs:(NSDictionary *)args {
    // Capture into a local so the block uses the callback that passed this check, not a later read of the static.
    CLXUnityBackgroundCallback callback = _backgroundCallback;
    if (!callback) {
        return;
    }

    // Snapshot so a caller mutating its NSMutableDictionary after this call cannot race the queue thread.
    NSDictionary *snapshot = [args copy];
    [self.backgroundCallbackEventsQueue addOperationWithBlock:^{
        NSString *json = [CLXUnityAdManager serializeParameters:snapshot];
        callback(json.UTF8String);
    }];
}

#pragma mark - Arbiter

- (void)runArbiterWithCallId:(NSString *)callId bidsJson:(NSString *)bidsJson {
    clx_unity_dispatch_on_main_thread(^{
        NSData *data = [bidsJson dataUsingEncoding:NSUTF8StringEncoding];
        if (!data) {
            [self forwardArbiterNoneResultWithCallId:callId];
            return;
        }

        NSError *parseError;
        id parsed = [NSJSONSerialization JSONObjectWithData:data options:0 error:&parseError];
        if (parseError || ![parsed isKindOfClass:[NSArray class]]) {
            [self log:@"arbiter: failed to parse bidsJson: %@", parseError];
            [self forwardArbiterNoneResultWithCallId:callId];
            return;
        }

        NSArray *bidsArray = (NSArray *)parsed;
        NSMutableArray<CLXArbiterBid *> *bids = [NSMutableArray array];
        for (id bidValue in bidsArray) {
            if (![bidValue isKindOfClass:[NSDictionary class]]) continue;
            CLXArbiterBid *bid = [self arbiterBidFromDictionary:(NSDictionary *)bidValue];
            if (bid) [bids addObject:bid];
        }

        if (bids.count == 0) {
            [self forwardArbiterNoneResultWithCallId:callId];
            return;
        }

        CLXArbiterConfiguration *config = [CLXArbiterConfiguration configurationWithBids:[bids copy]];
        [[CloudXCore shared] arbiterWithConfiguration:config completion:^(CLXArbiterResult *result) {
            NSMutableDictionary *args = [NSMutableDictionary dictionary];
            args[@"name"]     = @"OnArbiterCompletedEvent";
            args[@"callId"]   = callId;
            args[@"id"]       = result.identifier ?: @"";
            args[@"platform"] = result.platform.name ?: @"NONE";
            args[@"platformName"] = result.platformName ?: result.platform.name ?: @"NONE";
            if (result.bidId) args[@"bidId"] = result.bidId;
            if (result.extras.count > 0) args[@"extras"] = result.extras;
            [self forwardEventWithArgs:args];
        }];
    });
}

- (CLXArbiterBid *)arbiterBidFromDictionary:(NSDictionary *)dictionary {
    NSString *platform = [[self stringValueFromDictionary:dictionary key:@"platform"] uppercaseString] ?: @"";

    if ([platform isEqualToString:@"CLOUDX"]) {
        NSString *adUnitId = [self stringValueFromDictionary:dictionary key:@"adUnitId"];
        NSString *networkName = [self stringValueFromDictionary:dictionary key:@"networkName"];
        NSString *adFormatStr = [self stringValueFromDictionary:dictionary key:@"adFormat"];
        CLXAdFormat adFormat;
        if (adUnitId.length == 0 || networkName.length == 0
            || ![self adFormatFromString:adFormatStr result:&adFormat]) {
            [self log:@"arbiter: dropping CLOUDX bid — missing adUnitId, networkName, or adFormat"];
            return nil;
        }
        NSNumber *revenue = [self numberValueFromDictionary:dictionary key:@"revenue"] ?: @0;
        NSDictionary *adValues = [self stringMapFromDictionaryValue:dictionary[@"adValues"]];
        CLXAd *ad = [[CLXAd alloc] initWithAdUnitName:nil
                                              adUnitId:adUnitId
                                           networkName:networkName
                                      networkPlacement:[self optionalStringValueFromDictionary:dictionary key:@"networkPlacement"]
                                               revenue:revenue
                                              adFormat:adFormat
                                             placement:[self optionalStringValueFromDictionary:dictionary key:@"placement"]
                                              adValues:adValues];
        return [CLXArbiterBid cloudXBidWithAd:ad];
    }

    if ([platform isEqualToString:@"LEVELPLAY"]) {
        NSString *networkName = [self stringValueFromDictionary:dictionary key:@"networkName"];
        NSNumber *revenue = [self numberValueFromDictionary:dictionary key:@"revenue"];
        NSString *precision = [self stringValueFromDictionary:dictionary key:@"precision"];
        if (networkName.length == 0 || !revenue || precision.length == 0) {
            [self log:@"arbiter: dropping LEVELPLAY bid — missing networkName, revenue, or precision"];
            return nil;
        }
        return [CLXArbiterBid levelPlayBidWithNetworkName:networkName
                                                  revenue:revenue.doubleValue
                                                precision:precision
                                                   extras:[self stringMapFromDictionaryValue:dictionary[@"extras"]]];
    }

    if ([platform isEqualToString:@"PUBMATIC"]) {
        NSNumber *price = [self numberValueFromDictionary:dictionary key:@"price"];
        if (!price) {
            [self log:@"arbiter: dropping PUBMATIC bid — missing price"];
            return nil;
        }
        return [CLXArbiterBid pubMaticBidWithPrice:price.doubleValue
                                       partnerName:[self optionalStringValueFromDictionary:dictionary key:@"partnerName"]
                                            extras:[self stringMapFromDictionaryValue:dictionary[@"extras"]]];
    }

    if ([platform isEqualToString:@"CUSTOM"]) {
        NSString *platformName = [self stringValueFromDictionary:dictionary key:@"platformName"];
        NSNumber *revenue = [self numberValueFromDictionary:dictionary key:@"revenuePerImpressionUSD"];
        NSString *precision = [self stringValueFromDictionary:dictionary key:@"precision"];
        if (platformName.length == 0 || !revenue || precision.length == 0) {
            [self log:@"arbiter: dropping CUSTOM bid — missing platformName, revenuePerImpressionUSD, or precision"];
            return nil;
        }
        NSString *networkName = [self stringValueFromDictionary:dictionary key:@"networkName"] ?: @"";
        return [CLXArbiterBid customBidWithPlatformName:platformName
                                           networkName:networkName
                               revenuePerImpressionUSD:revenue.doubleValue
                                             precision:[CLXArbiterPrecision precisionWithName:precision]
                                                extras:[self stringMapFromDictionaryValue:dictionary[@"extras"]]];
    }

    if ([platform isEqualToString:@"ADMOB"]) {
        // Blank-checked rather than length-checked so a whitespace-only id is dropped here.
        // The native factory does not throw on iOS, so an all-space id would otherwise
        // serialize and lose server-side. Values are passed through untrimmed when valid.
        NSString *adUnitId = [self stringValueFromDictionary:dictionary key:@"adUnitId"];
        if (![self isNonBlankString:adUnitId]) {
            [self log:@"arbiter: dropping ADMOB bid — missing adUnitId"];
            return nil;
        }
        NSString *networkName = [self stringValueFromDictionary:dictionary key:@"networkName"];
        if (![self isNonBlankString:networkName]) networkName = @"admob";
        return [CLXArbiterBid adMobBidWithAdUnitId:adUnitId
                                       networkName:networkName
                     manualRevenuePerImpressionUSD:[self revenueNumberValueFromDictionary:dictionary key:@"manualRevenuePerImpressionUSD"]
                                            extras:[self stringMapFromDictionaryValue:dictionary[@"extras"]]];
    }

    if ([platform isEqualToString:@"GAM"]) {
        // Same blank-check rationale as the ADMOB branch above.
        NSString *adUnitId = [self stringValueFromDictionary:dictionary key:@"adUnitId"];
        if (![self isNonBlankString:adUnitId]) {
            [self log:@"arbiter: dropping GAM bid — missing adUnitId"];
            return nil;
        }
        NSString *networkName = [self stringValueFromDictionary:dictionary key:@"networkName"];
        if (![self isNonBlankString:networkName]) networkName = @"gam";
        return [CLXArbiterBid gamBidWithAdUnitId:adUnitId
                                     networkName:networkName
                   manualRevenuePerImpressionUSD:[self revenueNumberValueFromDictionary:dictionary key:@"manualRevenuePerImpressionUSD"]
                                          extras:[self stringMapFromDictionaryValue:dictionary[@"extras"]]];
    }

    [self log:@"arbiter: dropping bid with unknown platform: %@", platform];
    return nil;
}

- (void)forwardArbiterNoneResultWithCallId:(NSString *)callId {
    NSMutableDictionary *args = [NSMutableDictionary dictionary];
    args[@"name"] = @"OnArbiterCompletedEvent";
    args[@"callId"] = callId;
    args[@"id"] = @"";
    args[@"platform"] = @"NONE";
    args[@"platformName"] = @"NONE";
    [self forwardEventWithArgs:args];
}

- (NSString *)stringValueFromDictionary:(NSDictionary *)dictionary key:(NSString *)key {
    id value = dictionary[key];
    return [value isKindOfClass:[NSString class]] ? (NSString *)value : nil;
}

- (NSString *)optionalStringValueFromDictionary:(NSDictionary *)dictionary key:(NSString *)key {
    id value = dictionary[key];
    if (![value isKindOfClass:[NSString class]]) return nil;
    NSString *str = (NSString *)value;
    return str.length > 0 ? str : nil;
}

- (NSNumber *)numberValueFromDictionary:(NSDictionary *)dictionary key:(NSString *)key {
    id value = dictionary[key];
    return [value isKindOfClass:[NSNumber class]] ? (NSNumber *)value : nil;
}

- (NSNumber *)revenueNumberValueFromDictionary:(NSDictionary *)dictionary key:(NSString *)key {
    NSNumber *value = [self numberValueFromDictionary:dictionary key:key];
    if (!value) return nil;
    if (CFGetTypeID((__bridge CFTypeRef)value) == CFBooleanGetTypeID()) return nil;
    return value;
}

- (BOOL)isNonBlankString:(NSString *)value {
    if (!value) return NO;
    return [value stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceAndNewlineCharacterSet]].length > 0;
}

- (NSString *)nonBlankRevenueStringValueFromDictionary:(NSDictionary *)dictionary key:(NSString *)key {
    NSString *value = [self stringValueFromDictionary:dictionary key:key];
    if (!value) return nil;
    NSString *trimmed = [value stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceAndNewlineCharacterSet]];
    return trimmed.length > 0
        ? trimmed
        : nil;
}

- (CLXRevenueData *)revenueDataFromDictionary:(NSDictionary *)dictionary {
    NSString *platform = [self stringValueFromDictionary:dictionary key:@"platform"];
    CLXRevenuePlatform nativePlatform = [self revenuePlatformFromString:platform];
    if (!nativePlatform) {
        [self log:@"reportRevenueData: missing platform"];
        return nil;
    }

    NSNumber *revenue = [self revenueNumberValueFromDictionary:dictionary key:@"revenue"];
    if (!revenue) {
        [self log:@"reportRevenueData: missing revenue"];
        return nil;
    }
    if (!isfinite(revenue.doubleValue)) {
        [self log:@"reportRevenueData: non-finite revenue"];
        return nil;
    }

    NSString *adFormat = [self nonBlankRevenueStringValueFromDictionary:dictionary key:@"adFormat"];
    if (!adFormat) {
        [self log:@"reportRevenueData: missing adFormat"];
        return nil;
    }
    [self log:@"reportRevenueData: parsed platform=%@, revenue=%@, adFormat=%@", nativePlatform, revenue, adFormat];

    NSString *precisionString = [self nonBlankRevenueStringValueFromDictionary:dictionary key:@"precision"];
    CLXRevenuePrecision *precision = [self revenuePrecisionFromString:precisionString];

    return [CLXRevenueData revenueDataWithPlatform:nativePlatform
                                           revenue:revenue.doubleValue
                                          adFormat:adFormat
                                      builderBlock:^(CLXRevenueDataBuilder *builder) {
        builder.currencyCode = [self nonBlankRevenueStringValueFromDictionary:dictionary key:@"currencyCode"];
        builder.precision = precision;
        builder.networkName = [self nonBlankRevenueStringValueFromDictionary:dictionary key:@"networkName"];
        builder.adUnitId = [self nonBlankRevenueStringValueFromDictionary:dictionary key:@"adUnitId"];
        builder.thirdPartyAdPlacementId = [self nonBlankRevenueStringValueFromDictionary:dictionary key:@"thirdPartyAdPlacementId"];
        builder.creativeId = [self nonBlankRevenueStringValueFromDictionary:dictionary key:@"creativeId"];
        builder.networkPlacement = [self nonBlankRevenueStringValueFromDictionary:dictionary key:@"networkPlacement"];
        builder.countryCode = [self nonBlankRevenueStringValueFromDictionary:dictionary key:@"countryCode"];
        builder.userSegment = [self nonBlankRevenueStringValueFromDictionary:dictionary key:@"userSegment"];
    }];
}

- (CLXRevenuePlatform)revenuePlatformFromString:(NSString *)string {
    if (!string) return nil;
    NSString *trimmed = [string stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceAndNewlineCharacterSet]];
    if (trimmed.length == 0) return nil;
    if ([trimmed caseInsensitiveCompare:CLXRevenuePlatformAdMob] == NSOrderedSame) return CLXRevenuePlatformAdMob;
    if ([trimmed caseInsensitiveCompare:CLXRevenuePlatformGAM] == NSOrderedSame) return CLXRevenuePlatformGAM;
    if ([trimmed caseInsensitiveCompare:CLXRevenuePlatformInMobi] == NSOrderedSame) return CLXRevenuePlatformInMobi;
    if ([trimmed caseInsensitiveCompare:CLXRevenuePlatformTopOn] == NSOrderedSame) return CLXRevenuePlatformTopOn;
    return trimmed;
}

- (CLXRevenuePrecision *)revenuePrecisionFromString:(NSString *)string {
    if (!string) return nil;
    NSString *lower = [string lowercaseString];
    if ([lower isEqualToString:@"exact"]) return CLXRevenuePrecision.exact;
    if ([lower isEqualToString:@"estimated"]) return CLXRevenuePrecision.estimated;
    if ([lower isEqualToString:@"publisher_defined"]) return CLXRevenuePrecision.publisherDefined;
    if ([lower isEqualToString:@"undefined"]) return CLXRevenuePrecision.undefined;

    [self log:@"reportRevenueData: ignoring unknown precision=%@", string];
    return nil;
}

/*
 * Converts a JSON-parsed value to a flat NSString→NSString dictionary.
 *
 * Coercion rules (matches Android ArbiterBidJsonParser.parseStringMap behaviour):
 *   NSString   → used as-is
 *   NSNumber   → stringValue (e.g. 1.23 → "1.23")
 *   NSDictionary / NSArray → JSON-serialized via NSJSONSerialization
 *   anything else → dropped
 *
 * Blank keys and empty-string values are also dropped. Returns an empty
 * dictionary when value is nil or not an NSDictionary.
 */
- (NSDictionary<NSString *, NSString *> *)stringMapFromDictionaryValue:(id)value {
    if (![value isKindOfClass:[NSDictionary class]]) return @{};
    NSDictionary *dict = (NSDictionary *)value;
    NSMutableDictionary *result = [NSMutableDictionary dictionary];
    for (id rawKey in dict) {
        if (![rawKey isKindOfClass:[NSString class]]) continue;
        NSString *key = (NSString *)rawKey;
        if (key.length == 0) continue;

        id v = dict[key];
        NSString *stringValue = nil;
        if ([v isKindOfClass:[NSString class]]) {
            stringValue = (NSString *)v;
        } else if ([v isKindOfClass:[NSNumber class]]) {
            stringValue = [(NSNumber *)v stringValue];
        } else if ([v isKindOfClass:[NSDictionary class]] || [v isKindOfClass:[NSArray class]]) {
            NSData *serialized = [NSJSONSerialization dataWithJSONObject:v options:0 error:nil];
            stringValue = serialized
                ? [[NSString alloc] initWithData:serialized encoding:NSUTF8StringEncoding]
                : [v description];
        }
        if (stringValue.length > 0) result[key] = stringValue;
    }
    return [result copy];
}

- (BOOL)adFormatFromString:(NSString *)string result:(CLXAdFormat *)outFormat {
    NSString *upper = [string uppercaseString];
    if ([upper isEqualToString:@"BANNER"])        { *outFormat = CLXAdFormatBanner;       return YES; }
    if ([upper isEqualToString:@"MREC"])          { *outFormat = CLXAdFormatMREC;         return YES; }
    if ([upper isEqualToString:@"INTERSTITIAL"])  { *outFormat = CLXAdFormatInterstitial; return YES; }
    if ([upper isEqualToString:@"APP_OPEN"] || [upper isEqualToString:@"APPOPEN"]) { *outFormat = CLXAdFormatAppOpen; return YES; }
    if ([upper isEqualToString:@"REWARDED"])      { *outFormat = CLXAdFormatRewarded;     return YES; }
    if ([upper isEqualToString:@"NATIVE"])        { *outFormat = CLXAdFormatNative;       return YES; }
    return NO;
}

#pragma mark - Utilities

+ (NSString *)serializeParameters:(NSDictionary<NSString *, id> *)dict {
    NSError *error;
    NSData *data = [NSJSONSerialization dataWithJSONObject:dict options:0 error:&error];
    if (error) {
        NSLog(@"[%@] JSON serialization error: %@", TAG, error);
        return @"{}";
    }
    return [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
}

- (UIViewController *)unityViewController {
    UIViewController *vc = UnityGetGLViewController();
    if (!vc) {
        vc = UnityGetMainWindow().rootViewController;
    }
    if (!vc) {
        vc = [UIApplication sharedApplication].keyWindow.rootViewController;
    }
    return vc;
}

- (void)positionAdView:(UIView *)adView position:(NSString *)position {
    adView.translatesAutoresizingMaskIntoConstraints = NO;
    
    UIViewController *viewController = [self unityViewController];
    UIView *superview = viewController.view;
    UILayoutGuide *safeArea = superview.safeAreaLayoutGuide;
    
    // Remove existing constraints
    [adView removeConstraints:adView.constraints];
    
    NSMutableArray *constraints = [NSMutableArray array];
    
    /*
     * Horizontal positioning. Absolute left/right anchors: position names promise physical
     * screen edges, so they must not mirror under an RTL locale.
     */
    if ([position containsString:@"Left"]) {
        [constraints addObject:[adView.leftAnchor constraintEqualToAnchor:safeArea.leftAnchor]];
    } else if ([position containsString:@"Right"]) {
        [constraints addObject:[adView.rightAnchor constraintEqualToAnchor:safeArea.rightAnchor]];
    } else {
        // Center horizontally (TopCenter, BottomCenter, Centered, or any default)
        [constraints addObject:[adView.centerXAnchor constraintEqualToAnchor:safeArea.centerXAnchor]];
    }
    
    // Vertical positioning
    if ([position containsString:@"Top"]) {
        [constraints addObject:[adView.topAnchor constraintEqualToAnchor:safeArea.topAnchor]];
    } else if ([position containsString:@"Bottom"]) {
        [constraints addObject:[adView.bottomAnchor constraintEqualToAnchor:safeArea.bottomAnchor]];
    } else if ([position containsString:@"Center"] || [position isEqualToString:@"Centered"]) {
        [constraints addObject:[adView.centerYAnchor constraintEqualToAnchor:safeArea.centerYAnchor]];
    } else {
        // Default to bottom center
        [constraints addObject:[adView.bottomAnchor constraintEqualToAnchor:safeArea.bottomAnchor]];
    }
    
    [NSLayoutConstraint activateConstraints:constraints];
}

/*
 * Positions a banner as a vertical strip flush against the left or right safe-area edge.
 *
 * Auto Layout resolves the untransformed frame and the rotation transform spins the view
 * about its center at render time, so pinning the center half a strip-width (banner
 * height / 2) from the target edge lands the rotated strip exactly flush on that edge.
 * The banner's intrinsicContentSize (320x50) provides the frame size.
 *
 * The target edge is the safe area's, not the raw screen edge: in landscape the notch /
 * Dynamic Island of a portrait phone sits vertically centered on a short edge - exactly
 * where the vertical banner goes - so anchoring to safeAreaLayoutGuide insets the strip
 * past the cutout and keeps the creative fully visible. On edges without a cutout the
 * safe-area edge equals the screen edge and the strip stays flush. Auto Layout re-solves
 * the constraints automatically whenever the safe area changes (e.g. rotation).
 */
- (void)positionVerticalAdView:(UIView *)adView verticalPosition:(NSString *)verticalPosition {
    adView.translatesAutoresizingMaskIntoConstraints = NO;

    UIViewController *viewController = [self unityViewController];
    UIView *superview = viewController.view;
    UILayoutGuide *safeArea = superview.safeAreaLayoutGuide;

    // Remove existing constraints
    [adView removeConstraints:adView.constraints];

    CGFloat const halfStripWidth = 25.0; // standard banner height 50pt / 2
    NSString *position = verticalPosition.lowercaseString;
    NSMutableArray *constraints = [NSMutableArray array];
    [constraints addObject:[adView.centerYAnchor constraintEqualToAnchor:safeArea.centerYAnchor]];

    /*
     * Absolute left/right anchors, not leading/trailing: the position names promise physical
     * screen edges and the rotation direction is chosen per physical edge - RTL-mirrored
     * anchors would land the strip on the opposite edge with the creative facing outward.
     */
    if ([position isEqualToString:@"right"]) {
        [constraints addObject:[adView.centerXAnchor constraintEqualToAnchor:safeArea.rightAnchor
                                                                     constant:-halfStripWidth]];
        adView.transform = CGAffineTransformMakeRotation(-M_PI_2);
    } else {
        if (![position isEqualToString:@"left"]) {
            [self log:@"positionVerticalAdView called with unknown verticalPosition: %@. Falling back to Left.", verticalPosition];
        }
        [constraints addObject:[adView.centerXAnchor constraintEqualToAnchor:safeArea.leftAnchor
                                                                     constant:halfStripWidth]];
        adView.transform = CGAffineTransformMakeRotation(M_PI_2);
    }

    [NSLayoutConstraint activateConstraints:constraints];
}

/// Returns the banner for a placement and logs a warning when the caller expected one to exist.
- (nullable CLXBannerAdView *)requireBannerForPlacement:(NSString *)placement action:(NSString *)action {
    CLXBannerAdView *banner = self.banners[placement];
    if (!banner) {
        [self log:@"%@ called but banner not found for placement: %@", action, placement];
    }

    return banner;
}

/// Returns the MREC for a placement and logs a warning when the caller expected one to exist.
- (nullable CLXBannerAdView *)requireMrecForPlacement:(NSString *)placement action:(NSString *)action {
    CLXBannerAdView *mrec = self.mrecs[placement];
    if (!mrec) {
        [self log:@"%@ called but MREC not found for placement: %@", action, placement];
    }

    return mrec;
}

- (void)log:(NSString *)format, ... {
    va_list args;
    va_start(args, format);
    NSString *message = [[NSString alloc] initWithFormat:format arguments:args];
    va_end(args);
    
    NSLog(@"[%@] %@", TAG, message);
}

@end
