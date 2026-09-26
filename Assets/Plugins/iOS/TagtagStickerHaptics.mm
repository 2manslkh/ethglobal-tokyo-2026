#import <UIKit/UIKit.h>

extern "C" void TagtagStickerSuccessHaptic(void)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (UIApplication.sharedApplication.applicationState != UIApplicationStateActive) return;
        UINotificationFeedbackGenerator *feedback = [[UINotificationFeedbackGenerator alloc] init];
        [feedback notificationOccurred:UINotificationFeedbackTypeSuccess];
    });
}
