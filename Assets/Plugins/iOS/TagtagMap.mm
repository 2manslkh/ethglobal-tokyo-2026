#import <MapKit/MapKit.h>
#import <UIKit/UIKit.h>
#include <cmath>
#include <cstdlib>
#include <cstring>
#include "Unity/UnityInterface.h"

@interface TagtagPin : NSObject <MKAnnotation>
@property(nonatomic, copy) NSString *stickerId;
@property(nonatomic, copy) NSString *presetId;
@property(nonatomic, copy) NSString *thumbnailUrl;
@property(nonatomic, copy) NSString *title;
@property(nonatomic) CLLocationCoordinate2D coordinate;
@end
@implementation TagtagPin
@end

@interface TagtagMapDelegate : NSObject <MKMapViewDelegate>
@property(nonatomic, strong) NSString *selection;
@end

static MKMapView *tagtagMap;
static TagtagMapDelegate *tagtagDelegate;
static NSMutableDictionary<NSString *, TagtagPin *> *tagtagPins;
static NSCache<NSString *, UIImage *> *tagtagThumbnailCache;
static NSMutableDictionary<NSString *, NSMutableArray *> *tagtagThumbnailRequests;
static BOOL tagtagFirstFixSeen;
static BOOL tagtagInitialLocationPresent;
static CLLocationCoordinate2D tagtagInitialLocation;
static BOOL tagtagUserMoved;
static BOOL tagtagExplicitTarget;
static BOOL tagtagProgrammaticRegionChange;
static int tagtagLoadingStatus;

static BOOL tagtagReducedMotion;

static BOOL TagtagHasActiveGesture(UIView *view) {
    for (UIGestureRecognizer *gesture in view.gestureRecognizers)
        if (gesture.state == UIGestureRecognizerStateBegan || gesture.state == UIGestureRecognizerStateChanged) return YES;
    for (UIView *subview in view.subviews)
        if (TagtagHasActiveGesture(subview)) return YES;
    return NO;
}

static void TagtagLoadThumbnail(NSURL *url, void (^completion)(UIImage *)) {
    if (!tagtagThumbnailCache) {
        tagtagThumbnailCache = [NSCache new];
        tagtagThumbnailCache.countLimit = 96;
        tagtagThumbnailCache.totalCostLimit = 24 * 1024 * 1024;
    }
    if (!tagtagThumbnailRequests) tagtagThumbnailRequests = [NSMutableDictionary new];
    NSString *key = url.absoluteString;
    UIImage *cached = [tagtagThumbnailCache objectForKey:key];
    if (cached) { completion(cached); return; }
    NSMutableArray *waiting = tagtagThumbnailRequests[key];
    if (waiting) { [waiting addObject:[completion copy]]; return; }
    tagtagThumbnailRequests[key] = [NSMutableArray arrayWithObject:[completion copy]];
    NSURLRequest *request = [NSURLRequest requestWithURL:url cachePolicy:NSURLRequestUseProtocolCachePolicy timeoutInterval:20];
    [[[NSURLSession sharedSession] dataTaskWithRequest:request completionHandler:^(NSData *data, NSURLResponse *response, NSError *error) {
        UIImage *art = nil;
        if (!error && data.length > 0 && data.length <= 5 * 1024 * 1024 &&
            [response isKindOfClass:NSHTTPURLResponse.class] && ((NSHTTPURLResponse *)response).statusCode == 200) {
            art = [UIImage imageWithData:data];
            if (art.size.width > 1024 || art.size.height > 1024) art = nil;
        }
        dispatch_async(dispatch_get_main_queue(), ^{
            if (art) [tagtagThumbnailCache setObject:art forKey:key cost:(NSUInteger)(art.size.width * art.size.height * 4)];
            NSArray *callbacks = [tagtagThumbnailRequests[key] copy];
            [tagtagThumbnailRequests removeObjectForKey:key];
            for (id callback in callbacks) ((void (^)(UIImage *))callback)(art);
        });
    }] resume];
}

static UIImage *TagtagPinImage(NSString *presetId, NSUInteger count, UIImage *customArt) {
    const BOOL cluster = count > 0;
    CGSize size = cluster ? CGSizeMake(50, 50) : CGSizeMake(56, 60);
    UIGraphicsImageRenderer *renderer = [[UIGraphicsImageRenderer alloc] initWithSize:size];
    return [renderer imageWithActions:^(UIGraphicsImageRendererContext *context) {
        CGContextRef ctx = context.CGContext;
        UIColor *ink = [UIColor colorWithRed:32.0/255 green:32.0/255 blue:30.0/255 alpha:1];
        CGContextSetShadowWithColor(ctx, CGSizeMake(0, 2), 3, [UIColor colorWithWhite:0 alpha:.18].CGColor);
        if (cluster) {
            UIBezierPath *paper = [UIBezierPath bezierPathWithRoundedRect:CGRectMake(3, 2, 44, 44) cornerRadius:16];
            [[UIColor colorWithRed:1 green:254.0/255 blue:250.0/255 alpha:1] setFill];
            [paper fill];
            CGContextSetShadowWithColor(ctx, CGSizeZero, 0, nil);
            NSString *label = [NSString stringWithFormat:@"%lu", (unsigned long)count];
            UIFont *font = [UIFont fontWithName:@"InstrumentSans-SemiBold" size:17] ?: [UIFont systemFontOfSize:17 weight:UIFontWeightSemibold];
            NSDictionary *style = @{NSFontAttributeName: font, NSForegroundColorAttributeName: ink};
            CGSize textSize = [label sizeWithAttributes:style];
            [label drawAtPoint:CGPointMake((size.width - textSize.width) / 2, (46 - textSize.height) / 2) withAttributes:style];
        } else {
            NSSet *presets = [NSSet setWithArray:@[@"taggi-1", @"taggi-2", @"taggi-3", @"taggi-4"]];
            NSString *resource = [presets containsObject:presetId] ? presetId : nil;
            UIImage *art = customArt ?: (resource ? [UIImage imageNamed:[resource stringByAppendingPathExtension:@"png"]] : nil);
            if (art) {
                CGFloat scale = MIN(48 / art.size.width, 48 / art.size.height);
                CGSize fitted = CGSizeMake(art.size.width * scale, art.size.height * scale);
                [art drawInRect:CGRectMake(4 + (48 - fitted.width) / 2, 2 + (48 - fitted.height) / 2, fitted.width, fitted.height)];
            } else {
                [[UIColor colorWithWhite:1 alpha:1] setFill];
                [[UIBezierPath bezierPathWithRoundedRect:CGRectMake(7, 5, 42, 42) cornerRadius:10] fill];
                [[UIImage systemImageNamed:@"photo"] drawInRect:CGRectMake(16, 14, 24, 24)];
            }
        }
    }];
}

@interface TagtagAnnotationView : MKAnnotationView
@end
@implementation TagtagAnnotationView
- (void)setSelected:(BOOL)selected animated:(BOOL)animated {
    [super setSelected:selected animated:animated];
    self.backgroundColor = selected ? [UIColor colorWithRed:1 green:225.0/255 blue:90.0/255 alpha:1] : UIColor.clearColor;
    self.layer.cornerRadius = 14;
    self.accessibilityTraits = UIAccessibilityTraitButton | (selected ? UIAccessibilityTraitSelected : 0);
}
@end

@implementation TagtagMapDelegate
- (MKAnnotationView *)mapView:(MKMapView *)mapView viewForAnnotation:(id<MKAnnotation>)annotation {
    if ([annotation isKindOfClass:MKUserLocation.class]) return nil;
    BOOL cluster = [annotation isKindOfClass:MKClusterAnnotation.class];
    NSString *reuse = cluster ? @"tagtag-cluster" : @"tagtag-sticker";
    MKAnnotationView *view = [mapView dequeueReusableAnnotationViewWithIdentifier:reuse];
    if (!view) view = [[TagtagAnnotationView alloc] initWithAnnotation:annotation reuseIdentifier:reuse];
    view.annotation = annotation;
    view.canShowCallout = NO;
    view.clusteringIdentifier = cluster ? nil : @"tagtag-nearby";
    NSUInteger count = cluster ? ((MKClusterAnnotation *)annotation).memberAnnotations.count : 0;
    view.image = TagtagPinImage(cluster ? nil : ((TagtagPin *)annotation).presetId, count, nil);
    if (!cluster) {
        TagtagPin *pin = (TagtagPin *)annotation;
        NSURL *url = pin.thumbnailUrl.length ? [NSURL URLWithString:pin.thumbnailUrl] : nil;
        if ([url.scheme isEqualToString:@"https"]) {
            __weak MKAnnotationView *weakView = view;
            TagtagLoadThumbnail(url, ^(UIImage *art) {
                if (art && weakView.annotation == pin) {
                    weakView.image = TagtagPinImage(nil, 0, art);
                    weakView.centerOffset = CGPointMake(0, -weakView.image.size.height / 2);
                }
            });
        }
    }
    view.centerOffset = CGPointMake(0, -view.image.size.height / 2);
    view.accessibilityLabel = cluster ? [NSString stringWithFormat:@"%lu stickers", (unsigned long)count] : annotation.title;
    return view;
}
- (void)mapView:(MKMapView *)mapView didSelectAnnotationView:(MKAnnotationView *)view {
    if ([view.annotation isKindOfClass:MKClusterAnnotation.class]) {
        MKClusterAnnotation *cluster = (MKClusterAnnotation *)view.annotation;
        [mapView showAnnotations:cluster.memberAnnotations animated:!(tagtagReducedMotion || UIAccessibilityIsReduceMotionEnabled())];
        [mapView deselectAnnotation:view.annotation animated:NO];
    } else if ([view.annotation isKindOfClass:TagtagPin.class]) {
        self.selection = ((TagtagPin *)view.annotation).stickerId;
    }
}
- (void)mapView:(MKMapView *)mapView regionDidChangeAnimated:(BOOL)animated {
    if (tagtagProgrammaticRegionChange) tagtagProgrammaticRegionChange = NO;
    if (!tagtagFirstFixSeen && TagtagHasActiveGesture(mapView)) tagtagUserMoved = YES;
}
- (void)mapView:(MKMapView *)mapView regionWillChangeAnimated:(BOOL)animated {
    if (!tagtagFirstFixSeen && TagtagHasActiveGesture(mapView)) tagtagUserMoved = YES;
}
- (void)mapViewWillStartLoadingMap:(MKMapView *)mapView { tagtagLoadingStatus = 1; }
- (void)mapViewDidFinishLoadingMap:(MKMapView *)mapView { tagtagLoadingStatus = 0; }
- (void)mapViewDidFailLoadingMap:(MKMapView *)mapView withError:(NSError *)error { tagtagLoadingStatus = 2; }
@end

extern "C" int TagtagMapShow(float x, float y, float width, float height,
                                float screenWidth, float screenHeight, int hasLocation, double latitude, double longitude,
                                const char *pinsJson) {
    if (!(std::isfinite(x) && std::isfinite(y) && std::isfinite(width) && std::isfinite(height)) ||
        screenWidth <= 0 || screenHeight <= 0 || width <= 0 || height <= 0) return 0;
    UIView *host = UnityGetGLView();
    if (!host || !host.window) return 0;
    if (!tagtagDelegate) tagtagDelegate = [TagtagMapDelegate new];
    if (!tagtagPins) tagtagPins = [NSMutableDictionary new];
    if (!tagtagMap) {
        tagtagLoadingStatus = 1;
        tagtagMap = [[MKMapView alloc] initWithFrame:CGRectZero];
        tagtagMap.mapType = MKMapTypeStandard;
        tagtagMap.overrideUserInterfaceStyle = UIUserInterfaceStyleLight;
        tagtagMap.showsCompass = YES;
        tagtagMap.showsUserLocation = YES;
        tagtagMap.delegate = tagtagDelegate;
        tagtagMap.accessibilityLabel = @"Nearby stickers map";
        [host addSubview:tagtagMap];
        if (hasLocation && CLLocationCoordinate2DIsValid(CLLocationCoordinate2DMake(latitude, longitude))) {
            tagtagInitialLocationPresent = YES;
            tagtagInitialLocation = CLLocationCoordinate2DMake(latitude, longitude);
            tagtagProgrammaticRegionChange = YES;
            [tagtagMap setRegion:MKCoordinateRegionMakeWithDistance(CLLocationCoordinate2DMake(latitude, longitude), 900, 900) animated:NO];
        } else {
            tagtagProgrammaticRegionChange = YES;
            [tagtagMap setRegion:MKCoordinateRegionMake(CLLocationCoordinate2DMake(0, 0), MKCoordinateSpanMake(150, 330)) animated:NO];
        }
    } else if (hasLocation && !tagtagFirstFixSeen && CLLocationCoordinate2DIsValid(CLLocationCoordinate2DMake(latitude, longitude)) &&
               (!tagtagInitialLocationPresent || tagtagInitialLocation.latitude != latitude || tagtagInitialLocation.longitude != longitude)) {
        tagtagFirstFixSeen = YES;
        if (!tagtagUserMoved && !tagtagExplicitTarget) {
            tagtagProgrammaticRegionChange = YES;
            [tagtagMap setRegion:MKCoordinateRegionMakeWithDistance(CLLocationCoordinate2DMake(latitude, longitude), 900, 900) animated:NO];
        }
    }
    CGFloat sx = host.bounds.size.width / screenWidth;
    CGFloat sy = host.bounds.size.height / screenHeight;
    tagtagMap.frame = CGRectMake(x * sx, (screenHeight - y - height) * sy, width * sx, height * sy);
    if (tagtagMap.superview != host) [host addSubview:tagtagMap];
    tagtagMap.hidden = NO;
    if (!pinsJson) return 1;
    NSData *data = [@(pinsJson) dataUsingEncoding:NSUTF8StringEncoding];
    NSDictionary *payload = [NSJSONSerialization JSONObjectWithData:data options:0 error:nil];
    NSArray *items = [payload[@"items"] isKindOfClass:NSArray.class] ? payload[@"items"] : @[];
    NSMutableSet<NSString *> *seen = [NSMutableSet new];
    for (NSDictionary *item in items) {
        if (![item isKindOfClass:NSDictionary.class] || ![item[@"id"] isKindOfClass:NSString.class]) continue;
        NSString *stickerId = item[@"id"];
        if (!stickerId.length || [seen containsObject:stickerId]) continue;
        double lat = [item[@"latitude"] doubleValue], lon = [item[@"longitude"] doubleValue];
        CLLocationCoordinate2D point = CLLocationCoordinate2DMake(lat, lon);
        if (!CLLocationCoordinate2DIsValid(point)) continue;
        [seen addObject:stickerId];
        NSString *preset = [item[@"presetId"] isKindOfClass:NSString.class] ? item[@"presetId"] : nil;
        NSString *thumbnail = [item[@"thumbnailUrl"] isKindOfClass:NSString.class] ? item[@"thumbnailUrl"] : nil;
        NSString *title = [item[@"place"] isKindOfClass:NSString.class] ? item[@"place"] : @"Sticker";
        TagtagPin *old = tagtagPins[stickerId];
        if (old && old.coordinate.latitude == lat && old.coordinate.longitude == lon &&
            [(old.presetId ?: @"") isEqualToString:(preset ?: @"")] &&
            [(old.thumbnailUrl ?: @"") isEqualToString:(thumbnail ?: @"")] &&
            [(old.title ?: @"") isEqualToString:title]) continue;
        if (old) [tagtagMap removeAnnotation:old];
        TagtagPin *pin = [TagtagPin new];
        pin.stickerId = stickerId;
        pin.presetId = preset;
        pin.thumbnailUrl = thumbnail;
        pin.title = title;
        pin.coordinate = point;
        tagtagPins[stickerId] = pin;
        [tagtagMap addAnnotation:pin];
    }
    for (NSString *stickerId in [tagtagPins.allKeys copy]) {
        if ([seen containsObject:stickerId]) continue;
        [tagtagMap removeAnnotation:tagtagPins[stickerId]];
        [tagtagPins removeObjectForKey:stickerId];
    }
    return 1;
}

extern "C" void TagtagMapHide() {
    tagtagDelegate.selection = nil;
    // Sheets temporarily cover the map. Keep its region and annotations for return.
    tagtagMap.hidden = YES;
}

extern "C" void TagtagMapDispose() {
    tagtagDelegate.selection = nil;
    tagtagMap.delegate = nil;
    [tagtagMap removeFromSuperview];
    tagtagMap = nil;
    tagtagDelegate = nil;
    tagtagPins = nil;
    tagtagFirstFixSeen = NO;
    tagtagInitialLocationPresent = NO;
    tagtagUserMoved = NO;
    tagtagExplicitTarget = NO;
    tagtagProgrammaticRegionChange = NO;
    tagtagLoadingStatus = 0;
}

extern "C" void TagtagMapSetReducedMotion(bool reduced) { tagtagReducedMotion = reduced; }

extern "C" int TagtagMapLoadingStatus() { return tagtagLoadingStatus; }

extern "C" void TagtagMapRetry() {
    if (!tagtagMap) return;
    tagtagLoadingStatus = 1;
    // A map-type round trip asks MapKit to issue fresh tile requests while retaining the region.
    tagtagMap.mapType = MKMapTypeSatellite;
    tagtagMap.mapType = MKMapTypeStandard;
}

extern "C" void TagtagMapRecenter(double latitude, double longitude) {
    if (!tagtagMap) return;
    CLLocationCoordinate2D point = CLLocationCoordinate2DMake(latitude, longitude);
    if (CLLocationCoordinate2DIsValid(point)) {
        tagtagExplicitTarget = YES;
        tagtagProgrammaticRegionChange = YES;
        [tagtagMap setRegion:MKCoordinateRegionMakeWithDistance(point, 900, 900) animated:!(tagtagReducedMotion || UIAccessibilityIsReduceMotionEnabled())];
    }
}

extern "C" char *TagtagMapPoll() {
    NSString *selection = tagtagDelegate.selection;
    if (!selection) return nullptr;
    tagtagDelegate.selection = nil;
    return strdup(selection.UTF8String);
}

extern "C" void TagtagMapFree(char *pointer) { free(pointer); }
