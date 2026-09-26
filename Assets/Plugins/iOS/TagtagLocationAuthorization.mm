#import <CoreLocation/CoreLocation.h>

// Keep these values in sync with Tagtag.Services.LocationAuthorization.
static CLLocationManager *tagtagAuthorizationManager;

extern "C" int TagtagLocationAuthorizationStatus()
{
    if (![CLLocationManager locationServicesEnabled]) return 1; // Denied device-wide.

    CLAuthorizationStatus status = [CLLocationManager authorizationStatus];
    switch (status)
    {
        case kCLAuthorizationStatusDenied:
        case kCLAuthorizationStatusRestricted:
            return 1;
        case kCLAuthorizationStatusNotDetermined:
            return 0;
        case kCLAuthorizationStatusAuthorizedAlways:
        case kCLAuthorizationStatusAuthorizedWhenInUse:
            if (@available(iOS 14.0, *))
            {
                if (!tagtagAuthorizationManager) tagtagAuthorizationManager = [[CLLocationManager alloc] init];
                return tagtagAuthorizationManager.accuracyAuthorization == CLAccuracyAuthorizationReducedAccuracy ? 2 : 3;
            }
            return 3;
    }
    return 0;
}
