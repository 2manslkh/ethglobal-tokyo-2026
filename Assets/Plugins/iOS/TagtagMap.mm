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

static BOOL tagtagReducedMotion;

static UIImage *TagtagPinImage(NSString *presetId, NSUInteger count) {
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
            NSString *resource = [presets containsObject:presetId] ? presetId : @"taggi-1";
            UIImage *art = [UIImage imageNamed:[resource stringByAppendingPathExtension:@"png"]];
            [art drawInRect:CGRectMake(4, 2, 48, 48)];
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
    view.image = TagtagPinImage(cluster ? nil : ((TagtagPin *)annotation).presetId, count);
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
        tagtagMap.overrideUserInterfaceStyle = UIUserInterfaceStyleLight;
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
    if (tagtagMap.superview != host) [host addSubview:tagtagMap];
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
    // Sheets temporarily cover the map. Keep its region and annotations for return.
    tagtagMap.hidden = YES;
}

extern "C" void TagtagMapDispose() {
    tagtagDelegate.selection = nil;
    tagtagMap.delegate = nil;
    [tagtagMap removeFromSuperview];
    tagtagMap = nil;
    tagtagDelegate = nil;
}

extern "C" void TagtagMapSetReducedMotion(bool reduced) { tagtagReducedMotion = reduced; }

extern "C" void TagtagMapRecenter(double latitude, double longitude) {
    if (!tagtagMap) return;
    CLLocationCoordinate2D point = CLLocationCoordinate2DMake(latitude, longitude);
    if (CLLocationCoordinate2DIsValid(point))
        [tagtagMap setRegion:MKCoordinateRegionMakeWithDistance(point, 900, 900) animated:!(tagtagReducedMotion || UIAccessibilityIsReduceMotionEnabled())];
}

extern "C" char *TagtagMapPoll() {
    NSString *selection = tagtagDelegate.selection;
    if (!selection) return nullptr;
    tagtagDelegate.selection = nil;
    return strdup(selection.UTF8String);
}

extern "C" void TagtagMapFree(char *pointer) { free(pointer); }
