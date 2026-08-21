//
//  CLXDemoAttPlugin.m
//  CloudX Unity demo app - App Tracking Transparency bridge
//
//  Demo-app-only native code. The CloudX SDK deliberately never prompts for
//  ATT: CLXAdTrackingService reads ATTrackingManager.trackingAuthorizationStatus
//  and treats notDetermined exactly like denied (nil IDFA, dnt = 1). Choosing
//  when to ask is the app's job, so the prompt lives here in the demo and must
//  never move into the CloudXSdk package.
//

#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <AppTrackingTransparency/AppTrackingTransparency.h>

/*
 * Status values handed back to C#. The iOS 14+ values match
 * ATTrackingManagerAuthorizationStatus exactly (notDetermined 0, restricted 1,
 * denied 2, authorized 3). -1 is a CloudX sentinel meaning the framework is
 * unavailable on this OS version, so C# can distinguish "cannot ask" from
 * "asked and got notDetermined".
 */
static const int kCLXDemoAttStatusUnavailable = -1;

typedef void (*CLXDemoAttCallback)(int status);

#ifdef __cplusplus
extern "C" {
#endif

/*
 * requestTrackingAuthorization only presents the alert when UIApplication is in
 * the foreground *active* state; called any earlier it silently returns the
 * current status. Unity's Application.isFocused is not a proxy for this - it can
 * report focus while UIApplication is still inactive during launch, which
 * produces an immediate notDetermined and no prompt.
 */
bool _CLXDemoAttIsActive(void)
{
    __block BOOL isActive = NO;

    if ([NSThread isMainThread])
    {
        return UIApplication.sharedApplication.applicationState == UIApplicationStateActive;
    }

    dispatch_sync(dispatch_get_main_queue(), ^{
        isActive = UIApplication.sharedApplication.applicationState == UIApplicationStateActive;
    });

    return isActive;
}

int _CLXDemoAttStatus(void)
{
    if (@available(iOS 14, *))
    {
        return (int)ATTrackingManager.trackingAuthorizationStatus;
    }

    return kCLXDemoAttStatusUnavailable;
}

void _CLXDemoAttRequest(CLXDemoAttCallback callback)
{
    if (callback == NULL)
    {
        return;
    }

    if (@available(iOS 14, *))
    {
        /*
         * requestTrackingAuthorizationWithCompletionHandler only presents the
         * alert while the app is active, and it invokes the handler on an
         * arbitrary queue. The C# side already waits for foreground before
         * calling this; hopping to main keeps the Unity-side latch single
         * threaded.
         */
        [ATTrackingManager requestTrackingAuthorizationWithCompletionHandler:^(ATTrackingManagerAuthorizationStatus status) {
            dispatch_async(dispatch_get_main_queue(), ^{
                callback((int)status);
            });
        }];
        return;
    }

    /*
     * Pre-iOS 14 has no ATT. Answer immediately so the caller's coroutine can
     * never hang waiting on a callback that would never arrive.
     */
    callback(kCLXDemoAttStatusUnavailable);
}

#ifdef __cplusplus
}
#endif
