#import <MapKit/MapKit.h>
#import <UIKit/UIKit.h>
#include <cmath>
#include <cstdlib>
#include <cstring>
#include "Unity/UnityInterface.h"

@interface TagtagPin : NSObject <MKAnnotation>
@property(nonatomic, copy) NSString *stickerId;
@property(nonatomic, copy) NSString *presetId;
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

static UIImage *TagtagPinImage(NSString *label, BOOL cluster) {
    CGSize size = cluster ? CGSizeMake(50, 54) : CGSizeMake(42, 48);
    UIGraphicsImageRenderer *renderer = [[UIGraphicsImageRenderer alloc] initWithSize:size];
    return [renderer imageWithActions:^(UIGraphicsImageRendererContext *context) {
        CGContextRef ctx = context.CGContext;
        CGContextSetShadowWithColor(ctx, CGSizeMake(0, 2), 3, [UIColor colorWithWhite:0 alpha:.25].CGColor);
        UIBezierPath *shape = [UIBezierPath bezierPath];
        CGFloat w = size.width;
        [shape moveToPoint:CGPointMake(w / 2, size.height - 2)];
        [shape addCurveToPoint:CGPointMake(6, 25) controlPoint1:CGPointMake(w / 2 - 9, 39) controlPoint2:CGPointMake(5, 38)];
        [shape addCurveToPoint:CGPointMake(w - 6, 25) controlPoint1:CGPointMake(5, -3) controlPoint2:CGPointMake(w - 5, -3)];
        [shape addCurveToPoint:CGPointMake(w / 2, size.height - 2) controlPoint1:CGPointMake(w - 5, 38) controlPoint2:CGPointMake(w / 2 + 9, 39)];
        [UIColor.whiteColor setFill]; [shape fill];
        CGContextSetShadowWithColor(ctx, CGSizeZero, 0, nil);
        UIBezierPath *inner = [UIBezierPath bezierPathWithOvalInRect:CGRectMake(7, 5, w - 14, w - 14)];
        [[UIColor colorWithRed:1 green:.83 blue:.22 alpha:1] setFill]; [inner fill];
        NSDictionary *style = @{NSFontAttributeName: [UIFont boldSystemFontOfSize:cluster ? 16 : 13],
                                NSForegroundColorAttributeName: [UIColor colorWithWhite:.08 alpha:1]};
        CGSize textSize = [label sizeWithAttributes:style];
        [label drawAtPoint:CGPointMake((w - textSize.width) / 2, 18 - textSize.height / 2) withAttributes:style];
    }];
}

@implementation TagtagMapDelegate
- (MKAnnotationView *)mapView:(MKMapView *)mapView viewForAnnotation:(id<MKAnnotation>)annotation {
    if ([annotation isKindOfClass:MKUserLocation.class]) return nil;
    BOOL cluster = [annotation isKindOfClass:MKClusterAnnotation.class];
    NSString *reuse = cluster ? @"tagtag-cluster" : @"tagtag-sticker";
    MKAnnotationView *view = [mapView dequeueReusableAnnotationViewWithIdentifier:reuse];
    if (!view) view = [[MKAnnotationView alloc] initWithAnnotation:annotation reuseIdentifier:reuse];
    view.annotation = annotation;
    view.canShowCallout = NO;
    view.clusteringIdentifier = cluster ? nil : @"tagtag-nearby";
    NSString *label = cluster ? [NSString stringWithFormat:@"%lu", (unsigned long)((MKClusterAnnotation *)annotation).memberAnnotations.count] : @"✦";
    view.image = TagtagPinImage(label, cluster);
    view.centerOffset = CGPointMake(0, -view.image.size.height / 2);
    view.accessibilityLabel = cluster ? [NSString stringWithFormat:@"%@ stickers", label] : annotation.title;
    return view;
}
- (void)mapView:(MKMapView *)mapView didSelectAnnotationView:(MKAnnotationView *)view {
    if ([view.annotation isKindOfClass:MKClusterAnnotation.class]) {
        MKClusterAnnotation *cluster = (MKClusterAnnotation *)view.annotation;
        [mapView showAnnotations:cluster.memberAnnotations animated:YES];
    } else if ([view.annotation isKindOfClass:TagtagPin.class]) {
        self.selection = ((TagtagPin *)view.annotation).stickerId;
    }
    [mapView deselectAnnotation:view.annotation animated:NO];
}
@end

extern "C" void TagtagMapShow(float x, float y, float width, float height,
                                float screenWidth, float screenHeight, double latitude, double longitude,
                                const char *pinsJson) {
    if (!(std::isfinite(x) && std::isfinite(y) && std::isfinite(width) && std::isfinite(height)) ||
        screenWidth <= 0 || screenHeight <= 0 || width <= 0 || height <= 0) return;
    UIView *host = UnityGetGLView();
    if (!host || !host.window) return;
    if (!tagtagDelegate) tagtagDelegate = [TagtagMapDelegate new];
    if (!tagtagMap) {
        tagtagMap = [[MKMapView alloc] initWithFrame:CGRectZero];
        tagtagMap.mapType = MKMapTypeStandard;
        tagtagMap.showsCompass = YES;
        tagtagMap.showsUserLocation = YES;
        tagtagMap.delegate = tagtagDelegate;
        tagtagMap.accessibilityLabel = @"Nearby stickers map";
        [host addSubview:tagtagMap];
        if (CLLocationCoordinate2DIsValid(CLLocationCoordinate2DMake(latitude, longitude))) {
            [tagtagMap setRegion:MKCoordinateRegionMakeWithDistance(CLLocationCoordinate2DMake(latitude, longitude), 900, 900) animated:NO];
        }
    }
    CGFloat sx = host.bounds.size.width / screenWidth;
    CGFloat sy = host.bounds.size.height / screenHeight;
    tagtagMap.frame = CGRectMake(x * sx, (screenHeight - y - height) * sy, width * sx, height * sy);
    tagtagMap.hidden = NO;
    if (!pinsJson) return;
    NSData *data = [@(pinsJson) dataUsingEncoding:NSUTF8StringEncoding];
    NSDictionary *payload = [NSJSONSerialization JSONObjectWithData:data options:0 error:nil];
    NSArray *items = [payload[@"items"] isKindOfClass:NSArray.class] ? payload[@"items"] : @[];
    [tagtagMap removeAnnotations:tagtagMap.annotations];
    for (NSDictionary *item in items) {
        if (![item isKindOfClass:NSDictionary.class] || ![item[@"id"] isKindOfClass:NSString.class]) continue;
        double lat = [item[@"latitude"] doubleValue], lon = [item[@"longitude"] doubleValue];
        CLLocationCoordinate2D point = CLLocationCoordinate2DMake(lat, lon);
        if (!CLLocationCoordinate2DIsValid(point)) continue;
        TagtagPin *pin = [TagtagPin new];
        pin.stickerId = item[@"id"];
        pin.presetId = item[@"presetId"];
        pin.title = [item[@"place"] isKindOfClass:NSString.class] ? item[@"place"] : @"Sticker";
        pin.coordinate = point;
        [tagtagMap addAnnotation:pin];
    }
}

extern "C" void TagtagMapHide() {
    tagtagDelegate.selection = nil;
    tagtagMap.delegate = nil;
    [tagtagMap removeFromSuperview];
    tagtagMap = nil;
}

extern "C" void TagtagMapRecenter(double latitude, double longitude) {
    if (!tagtagMap) return;
    CLLocationCoordinate2D point = CLLocationCoordinate2DMake(latitude, longitude);
    if (CLLocationCoordinate2DIsValid(point))
        [tagtagMap setRegion:MKCoordinateRegionMakeWithDistance(point, 900, 900) animated:YES];
}

extern "C" char *TagtagMapPoll() {
    NSString *selection = tagtagDelegate.selection;
    if (!selection) return nullptr;
    tagtagDelegate.selection = nil;
    return strdup(selection.UTF8String);
}

extern "C" void TagtagMapFree(char *pointer) { free(pointer); }
