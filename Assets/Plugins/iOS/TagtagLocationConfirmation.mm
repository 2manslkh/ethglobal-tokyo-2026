#import <UIKit/UIKit.h>
#import <MapKit/MapKit.h>

extern "C" void UnitySendMessage(const char *, const char *, const char *);

@interface TagtagLocationPicker : UIViewController<MKMapViewDelegate>
@property(nonatomic, strong) MKMapView *map;
@property(nonatomic, strong) UILabel *message;
@property(nonatomic, strong) UIButton *confirm;
@property(nonatomic, strong) CLLocation *measured;
@property(nonatomic) double accuracy;
@property(nonatomic, copy) NSString *receiver;
@property(nonatomic, copy) NSString *requestId;
@property(nonatomic) BOOL locked;
@property(nonatomic) CLLocationCoordinate2D fixedSpot;
@property(nonatomic) BOOL finished;
@property(nonatomic) BOOL rendered;
- (void)finish:(BOOL)confirmed;
@end

static TagtagLocationPicker *activePicker;

@implementation TagtagLocationPicker
- (void)viewDidLoad {
    [super viewDidLoad];
    self.title = @"Confirm this spot";
    self.view.backgroundColor = UIColor.systemBackgroundColor;
    self.navigationItem.leftBarButtonItem = [[UIBarButtonItem alloc] initWithBarButtonSystemItem:UIBarButtonSystemItemCancel
        target:self action:@selector(cancel)];
    self.message = [UILabel new];
    self.message.text = @"GPS is approximate here. Move the map so the pin marks where you placed your sticker.";
    self.message.font = [UIFont preferredFontForTextStyle:UIFontTextStyleBody];
    self.message.adjustsFontForContentSizeCategory = YES;
    self.message.numberOfLines = 0;
    self.message.translatesAutoresizingMaskIntoConstraints = NO;
    [self.view addSubview:self.message];
    self.map = [MKMapView new];
    self.map.translatesAutoresizingMaskIntoConstraints = NO;
    self.map.delegate = self;
    self.map.rotateEnabled = NO;
    self.map.pitchEnabled = NO;
    self.map.accessibilityLabel = @"Sticker location. Move the map to position the pin.";
    [self.view addSubview:self.map];
    UIImageView *pin = [[UIImageView alloc] initWithImage:[UIImage systemImageNamed:@"mappin"]];
    pin.tintColor = UIColor.systemRedColor;
    pin.translatesAutoresizingMaskIntoConstraints = NO;
    pin.userInteractionEnabled = NO;
    pin.isAccessibilityElement = NO;
    [self.view addSubview:pin];
    pin.hidden = self.locked;
    if (self.locked) {
        MKPointAnnotation *savedPin = [MKPointAnnotation new];
        savedPin.coordinate = self.fixedSpot;
        savedPin.title = @"Your saved sticker";
        [self.map addAnnotation:savedPin];
    }
    self.confirm = [UIButton buttonWithType:UIButtonTypeSystem];
    self.confirm.translatesAutoresizingMaskIntoConstraints = NO;
    self.confirm.titleLabel.font = [UIFont preferredFontForTextStyle:UIFontTextStyleHeadline];
    self.confirm.titleLabel.adjustsFontForContentSizeCategory = YES;
    [self.confirm setTitle:@"Use this spot" forState:UIControlStateNormal];
    [self.confirm addTarget:self action:@selector(useSpot) forControlEvents:UIControlEventTouchUpInside];
    self.confirm.enabled = NO;
    [self.view addSubview:self.confirm];
    UILayoutGuide *safe = self.view.safeAreaLayoutGuide;
    [NSLayoutConstraint activateConstraints:@[
        [self.message.topAnchor constraintEqualToAnchor:safe.topAnchor constant:16],
        [self.message.leadingAnchor constraintEqualToAnchor:safe.leadingAnchor constant:20],
        [self.message.trailingAnchor constraintEqualToAnchor:safe.trailingAnchor constant:-20],
        [self.map.topAnchor constraintEqualToAnchor:self.message.bottomAnchor constant:16],
        [self.map.leadingAnchor constraintEqualToAnchor:safe.leadingAnchor],
        [self.map.trailingAnchor constraintEqualToAnchor:safe.trailingAnchor],
        [self.map.bottomAnchor constraintEqualToAnchor:self.confirm.topAnchor constant:-12],
        [self.confirm.leadingAnchor constraintEqualToAnchor:safe.leadingAnchor constant:20],
        [self.confirm.trailingAnchor constraintEqualToAnchor:safe.trailingAnchor constant:-20],
        [self.confirm.bottomAnchor constraintEqualToAnchor:safe.bottomAnchor constant:-12],
        [self.confirm.heightAnchor constraintGreaterThanOrEqualToConstant:52],
        [pin.centerXAnchor constraintEqualToAnchor:self.map.centerXAnchor],
        [pin.bottomAnchor constraintEqualToAnchor:self.map.centerYAnchor],
        [pin.widthAnchor constraintEqualToConstant:28], [pin.heightAnchor constraintEqualToConstant:40]
    ]];
    [self.map setRegion:MKCoordinateRegionMakeWithDistance(self.locked ? self.fixedSpot : self.measured.coordinate,
        MAX(600, self.accuracy * 2.5), MAX(600, self.accuracy * 2.5)) animated:NO];
    [self.map addOverlay:[MKCircle circleWithCenterCoordinate:self.measured.coordinate radius:self.accuracy]];
}
- (MKOverlayRenderer *)mapView:(MKMapView *)map rendererForOverlay:(id<MKOverlay>)overlay {
    MKCircleRenderer *circle = [[MKCircleRenderer alloc] initWithCircle:(MKCircle *)overlay];
    circle.fillColor = [UIColor.systemBlueColor colorWithAlphaComponent:0.08];
    circle.strokeColor = [UIColor.systemBlueColor colorWithAlphaComponent:0.35];
    circle.lineWidth = 1;
    return circle;
}
- (BOOL)spotInRange {
    CLLocationCoordinate2D point = self.locked ? self.fixedSpot : self.map.centerCoordinate;
    if (!CLLocationCoordinate2DIsValid(point)) return NO;
    return [self.measured distanceFromLocation:[[CLLocation alloc] initWithLatitude:point.latitude longitude:point.longitude]]
        <= self.accuracy + 100;
}
- (void)updateConfirmation {
    BOOL inRange = [self spotInRange];
    self.confirm.enabled = self.rendered && inRange;
    self.message.text = !inRange ? (self.locked ? @"Return to your saved sticker’s location to finish publishing." :
        @"Choose a spot inside the blue location area.") : self.locked ?
        @"Your sticker is saved at this pin. Confirm this spot to finish publishing." :
        @"GPS is approximate here. Move the map so the pin marks where you placed your sticker.";
}
- (void)mapView:(MKMapView *)map regionWillChangeAnimated:(BOOL)animated { self.confirm.enabled = NO; }
- (void)mapView:(MKMapView *)map regionDidChangeAnimated:(BOOL)animated { [self updateConfirmation]; }
- (void)mapViewDidFinishRenderingMap:(MKMapView *)map fullyRendered:(BOOL)fullyRendered {
    if (fullyRendered) { self.rendered = YES; [self updateConfirmation]; }
}
- (void)mapViewDidFailLoadingMap:(MKMapView *)map withError:(NSError *)error {
    self.rendered = NO;
    self.confirm.enabled = NO;
    self.message.text = @"The map couldn’t load. Check your connection, then cancel and try publishing again. Your note is saved.";
}
- (void)cancel { [self finish:NO]; }
- (void)useSpot { if (self.rendered && [self spotInRange]) [self finish:YES]; }
- (void)finish:(BOOL)confirmed {
    if (self.finished) return;
    if (self.navigationController.isBeingPresented && self.navigationController.transitionCoordinator) {
        [self.navigationController.transitionCoordinator animateAlongsideTransition:nil completion:^(id<UIViewControllerTransitionCoordinatorContext> context) {
            [self finish:confirmed];
        }];
        return;
    }
    self.finished = YES;
    CLLocationCoordinate2D point = self.locked ? self.fixedSpot : self.map.centerCoordinate;
    NSDictionary *result = @{@"requestId": self.requestId, @"status": confirmed ? @"confirmed" : @"cancelled",
        @"latitude": @(point.latitude), @"longitude": @(point.longitude)};
    NSString *json = [[NSString alloc] initWithData:[NSJSONSerialization dataWithJSONObject:result options:0 error:nil]
        encoding:NSUTF8StringEncoding];
    self.map.delegate = nil;
    [self.navigationController dismissViewControllerAnimated:YES completion:^{
        if (activePicker == self) activePicker = nil;
        UnitySendMessage(self.receiver.UTF8String, "OnLocationConfirmed", json.UTF8String);
    }];
}
@end

extern "C" void TagtagConfirmLocationOpen(double latitude, double longitude, float accuracy, bool locked, double pinLatitude, double pinLongitude, const char *receiver, const char *requestId) {
    NSString *target = [NSString stringWithUTF8String:receiver];
    NSString *identifier = [NSString stringWithUTF8String:requestId];
    dispatch_async(dispatch_get_main_queue(), ^{
        UIViewController *presenter = nil;
        for (UIScene *scene in UIApplication.sharedApplication.connectedScenes) {
            if (![scene isKindOfClass:UIWindowScene.class] || scene.activationState != UISceneActivationStateForegroundActive) continue;
            for (UIWindow *window in ((UIWindowScene *)scene).windows)
                if (window.isKeyWindow) presenter = window.rootViewController;
        }
        while (presenter.presentedViewController) presenter = presenter.presentedViewController;
        if (!presenter || activePicker) {
            NSString *json = [[NSString alloc] initWithData:[NSJSONSerialization dataWithJSONObject:
                @{@"requestId": identifier, @"status": @"cancelled"} options:0 error:nil] encoding:NSUTF8StringEncoding];
            UnitySendMessage(target.UTF8String, "OnLocationConfirmed", json.UTF8String);
            return;
        }
        activePicker = [TagtagLocationPicker new];
        activePicker.receiver = target;
        activePicker.requestId = identifier;
        activePicker.measured = [[CLLocation alloc] initWithLatitude:latitude longitude:longitude];
        activePicker.accuracy = accuracy;
        activePicker.locked = locked;
        activePicker.fixedSpot = CLLocationCoordinate2DMake(pinLatitude, pinLongitude);
        UINavigationController *navigation = [[UINavigationController alloc] initWithRootViewController:activePicker];
        navigation.modalPresentationStyle = UIModalPresentationFullScreen;
        [presenter presentViewController:navigation animated:YES completion:nil];
    });
}
extern "C" void TagtagConfirmLocationCancel() {
    dispatch_async(dispatch_get_main_queue(), ^{ [activePicker finish:NO]; });
}
