#import <UIKit/UIKit.h>
#include "../../Assets/Plugins/iOS/TagtagMap.mm"

static UIView *testHost;
extern "C" UIView *UnityGetGLView() { return testHost; }

static void Record(BOOL passed, NSString *message) {
    NSDictionary *result = @{@"passed": @(passed), @"message": message};
    NSString *path = [NSHomeDirectory() stringByAppendingPathComponent:@"Documents/result.json"];
    [[NSJSONSerialization dataWithJSONObject:result options:0 error:nil] writeToFile:path atomically:YES];
}
static void Check(BOOL condition, NSString *message) {
    if (!condition) @throw [NSException exceptionWithName:@"MapOverlapFailure" reason:message userInfo:nil];
}
static void ShowPins(NSUInteger count) {
    NSMutableArray *items = [NSMutableArray new];
    for (NSUInteger index = 0; index < count; index++)
        [items addObject:@{@"id": [NSString stringWithFormat:@"sticker-%02lu", (unsigned long)index],
            @"place": @"Same place", @"teaser": [NSString stringWithFormat:@"Clue for sticker %lu", (unsigned long)index],
            @"presetId": @"taggi-1", @"latitude": @35.68, @"longitude": @139.76}];
    NSData *data = [NSJSONSerialization dataWithJSONObject:@{@"items":items} options:0 error:nil];
    NSString *json = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
    CGSize size = testHost.bounds.size;
    Check(TagtagMapShow(0, 0, size.width, size.height, size.width, size.height, 1, 35.68, 139.76, json.UTF8String) == 1,
        @"Native map must mount in a real UIKit window.");
}
static void TapCluster() {
    MKClusterAnnotation *cluster = [[MKClusterAnnotation alloc] initWithMemberAnnotations:tagtagPins.allValues];
    MKAnnotationView *view = [tagtagDelegate mapView:tagtagMap viewForAnnotation:cluster];
    [tagtagDelegate mapView:tagtagMap didSelectAnnotationView:view];
}
static UITableViewController *Picker() {
    UIViewController *presented = testHost.window.rootViewController.presentedViewController;
    Check([presented isKindOfClass:UINavigationController.class],
        @"Tapping coincident stickers must open a chooser; zoom alone cannot separate them.");
    UIViewController *content = ((UINavigationController *)presented).topViewController;
    Check([content isKindOfClass:UITableViewController.class], @"Every clustered sticker must have a selectable row.");
    return (UITableViewController *)content;
}
static void ExpectSelection(NSString *expected) {
    char *value = TagtagMapPoll();
    NSString *actual = value ? @(value) : nil;
    TagtagMapFree(value);
    Check(expected ? [expected isEqualToString:actual] : actual == nil, @"Picker must return only the selected sticker ID.");
    Check(TagtagMapPoll() == nullptr, @"Selection must be consumed exactly once.");
}
static void SelectRow(UITableViewController *picker, NSInteger row) {
    NSIndexPath *index = [NSIndexPath indexPathForRow:row inSection:0];
    [picker.tableView.delegate tableView:picker.tableView didSelectRowAtIndexPath:index];
}
static UITableViewController *stalePicker;
static void RunStep(NSUInteger stage, NSUInteger attempts = 0) {
    @try {
        // UIKit completes presentation/dismissal across run-loop turns. Wait for that
        // observable state instead of assuming a fixed animation or simulator speed.
        UIViewController *presented = testHost.window.rootViewController.presentedViewController;
        BOOL settled = stage == 0 || (stage % 2 == 1 ?
            presented != nil && !presented.isBeingPresented && !presented.isBeingDismissed : presented == nil);
        if (!settled && attempts < 100) {
            dispatch_after(dispatch_time(DISPATCH_TIME_NOW, 50 * NSEC_PER_MSEC), dispatch_get_main_queue(), ^{ RunStep(stage, attempts + 1); });
            return;
        }
        Check(settled, @"Cluster chooser did not reach the expected open/closed state.");
        switch (stage) {
            case 0: ShowPins(2); TapCluster(); break;
            case 1: {
                UITableViewController *picker = Picker();
                Check([picker.tableView numberOfRowsInSection:0] == 2, @"Both identical-coordinate stickers must be listed.");
                UITableViewCell *cell = [picker.tableView.dataSource tableView:picker.tableView cellForRowAtIndexPath:[NSIndexPath indexPathForRow:1 inSection:0]];
                Check([cell.detailTextLabel.text isEqualToString:@"Clue for sticker 1"], @"Rows must distinguish stickers with their teaser.");
                SelectRow(picker, 0); break;
            }
            case 2: ExpectSelection(@"sticker-00"); TapCluster(); break;
            case 3: SelectRow(Picker(), 1); break;
            case 4: ExpectSelection(@"sticker-01"); TapCluster(); break;
            case 5: {
                UITableViewController *picker = Picker();
                UIBarButtonItem *close = picker.navigationItem.rightBarButtonItem;
                Check(close != nil, @"The chooser needs a Close control.");
                [UIApplication.sharedApplication sendAction:close.action to:close.target from:close forEvent:nil];
                break;
            }
            case 6:
                Check(testHost.window.rootViewController.presentedViewController == nil, @"Close must dismiss the chooser.");
                ExpectSelection(nil); TapCluster(); break;
            case 7: stalePicker = Picker(); TagtagMapHide(); break;
            case 8:
                Check(testHost.window.rootViewController.presentedViewController == nil, @"Leaving Explore must dismiss its chooser.");
                SelectRow(stalePicker, 0); stalePicker = nil;
                ExpectSelection(nil); ShowPins(25); TapCluster(); break;
            case 9: {
                UITableViewController *picker = Picker();
                Check([picker.tableView numberOfRowsInSection:0] == 25, @"Dense clusters must keep all members reachable.");
                [picker.tableView scrollToRowAtIndexPath:[NSIndexPath indexPathForRow:24 inSection:0] atScrollPosition:UITableViewScrollPositionBottom animated:NO];
                SelectRow(picker, 24); break;
            }
            case 10: ExpectSelection(@"sticker-24"); TapCluster(); break;
            case 11: ShowPins(1); break;
            case 12:
                Check(testHost.window.rootViewController.presentedViewController == nil, @"Changed pins must not leave a stale chooser open.");
                ExpectSelection(nil);
                [tagtagDelegate mapView:tagtagMap didSelectAnnotationView:
                    [tagtagDelegate mapView:tagtagMap viewForAnnotation:tagtagPins[@"sticker-00"]]];
                ExpectSelection(@"sticker-00");
                TagtagMapDispose();
                Record(YES, @"Coincident pin selection, repeated selection, close, map hiding, dense groups and refresh passed.");
                return;
        }
        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, 250 * NSEC_PER_MSEC), dispatch_get_main_queue(), ^{ RunStep(stage + 1); });
    } @catch (NSException *exception) { Record(NO, [NSString stringWithFormat:@"Stage %lu: %@", (unsigned long)stage, exception.reason]); }
}

@interface MapOverlapApp : UIResponder<UIApplicationDelegate>
@property(nonatomic, strong) UIWindow *window;
@end
@implementation MapOverlapApp
- (BOOL)application:(UIApplication *)application didFinishLaunchingWithOptions:(NSDictionary *)options {
    self.window = [[UIWindow alloc] initWithFrame:UIScreen.mainScreen.bounds];
    self.window.rootViewController = [UIViewController new];
    [self.window makeKeyAndVisible];
    testHost = self.window.rootViewController.view;
    TagtagMapSetReducedMotion(true);
    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, 250 * NSEC_PER_MSEC), dispatch_get_main_queue(), ^{
        if ([NSProcessInfo.processInfo.arguments containsObject:@"--review"]) {
            ShowPins(2); TapCluster(); Record(YES, @"Review ready");
        } else RunStep(0);
    });
    return YES;
}
@end
int main(int argc, char **argv) {
    @autoreleasepool { return UIApplicationMain(argc, argv, nil, NSStringFromClass(MapOverlapApp.class)); }
}
