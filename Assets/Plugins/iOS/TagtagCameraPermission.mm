#import <AVFoundation/AVFoundation.h>

// Unity's AVCapture trampoline can compile out permission handling when neither
// WebCamTexture nor Microphone is used. ARKit still needs the video permission.
extern "C" int TagtagCameraAuthorizationStatus()
{
    switch ([AVCaptureDevice authorizationStatusForMediaType:AVMediaTypeVideo])
    {
        case AVAuthorizationStatusNotDetermined: return 0;
        case AVAuthorizationStatusRestricted: return 1;
        case AVAuthorizationStatusDenied: return 2;
        case AVAuthorizationStatusAuthorized: return 3;
    }
    return -1;
}

extern "C" void TagtagCameraRequestAccess()
{
    if ([AVCaptureDevice authorizationStatusForMediaType:AVMediaTypeVideo] != AVAuthorizationStatusNotDetermined)
        return;
    [AVCaptureDevice requestAccessForMediaType:AVMediaTypeVideo completionHandler:^(BOOL granted) {
        // C# polls the authoritative status. This completion keeps the native request alive.
        (void)granted;
    }];
}
