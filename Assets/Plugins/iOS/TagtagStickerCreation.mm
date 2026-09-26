#import <UIKit/UIKit.h>
#import <PhotosUI/PhotosUI.h>
#import <UniformTypeIdentifiers/UniformTypeIdentifiers.h>
#import <Vision/Vision.h>
#import <CoreImage/CoreImage.h>
#import <AVFoundation/AVFoundation.h>

extern "C" void UnitySendMessage(const char *, const char *, const char *);
extern "C" int32_t TagtagStickerCreationAIAvailable(void);
extern "C" void TagtagStickerCreationAIOpen(void *);
extern "C" void TagtagStickerCreationAICancel(void);

static const NSUInteger TagtagMaximumPNGBytes = 5 * 1024 * 1024;
static const CGFloat TagtagMaximumEdge = 1024;

@class TagtagStickerEditor;
static TagtagStickerEditor *activeEditor;

static void TagtagSendResult(NSString *receiver, NSString *callback, NSString *status, NSString *error) {
    if (!receiver.length || !callback.length) return;
    NSDictionary *payload = @{@"status": status, @"path": @"", @"name": @"", @"kind": @"",
                              @"width": @0, @"height": @0, @"error": error ?: @""};
    NSData *json = [NSJSONSerialization dataWithJSONObject:payload options:0 error:nil];
    NSString *message = [[NSString alloc] initWithData:json encoding:NSUTF8StringEncoding];
    UnitySendMessage(receiver.UTF8String, callback.UTF8String, message.UTF8String);
}

static UIViewController *TagtagTopController(void) {
    UIWindow *window = nil;
    for (UIScene *scene in UIApplication.sharedApplication.connectedScenes) {
        if (![scene isKindOfClass:UIWindowScene.class] || scene.activationState != UISceneActivationStateForegroundActive) continue;
        for (UIWindow *candidate in ((UIWindowScene *)scene).windows) {
            if (candidate.isKeyWindow) { window = candidate; break; }
        }
        if (window) break;
    }
    UIViewController *controller = window.rootViewController;
    while (controller.presentedViewController) controller = controller.presentedViewController;
    return controller;
}

static UIImage *TagtagRender(UIImage *source, CGSize size, BOOL opaque) {
    UIGraphicsImageRendererFormat *format = [UIGraphicsImageRendererFormat defaultFormat];
    format.scale = 1;
    format.opaque = opaque;
    UIGraphicsImageRenderer *renderer = [[UIGraphicsImageRenderer alloc] initWithSize:size format:format];
    return [renderer imageWithActions:^(UIGraphicsImageRendererContext *context) {
        [source drawInRect:CGRectMake(0, 0, size.width, size.height)];
    }];
}

static UIImage *TagtagNormalize(UIImage *image) {
    if (!image || image.size.width < 1 || image.size.height < 1) return nil;
    CGFloat ratio = MIN(1, 2048.0 / MAX(image.size.width, image.size.height));
    CGSize size = CGSizeMake(MAX(1, round(image.size.width * ratio)), MAX(1, round(image.size.height * ratio)));
    return TagtagRender(image, size, NO); // Baking pixels removes EXIF orientation and source metadata.
}

static UIImage *TagtagCutout(UIImage *image, NSError **error) {
    if (@available(iOS 17.0, *)) {
        VNGenerateForegroundInstanceMaskRequest *request = [VNGenerateForegroundInstanceMaskRequest new];
        VNImageRequestHandler *handler = [[VNImageRequestHandler alloc] initWithCGImage:image.CGImage options:@{}];
        if (![handler performRequests:@[request] error:error]) return nil;
        VNInstanceMaskObservation *observation = request.results.firstObject;
        if (!observation || observation.allInstances.count == 0) return nil;
        CVPixelBufferRef pixelBuffer = [observation generateScaledMaskForImageForInstances:observation.allInstances
                                                                        fromRequestHandler:handler error:error];
        if (!pixelBuffer) return nil;
        CIImage *mask = [CIImage imageWithCVPixelBuffer:pixelBuffer];
        CVPixelBufferRelease(pixelBuffer);
        CIImage *source = [CIImage imageWithCGImage:image.CGImage];
        CGRect extent = source.extent;
        CIImage *clear = [[CIImage imageWithColor:CIColor.clearColor] imageByCroppingToRect:extent];
        CIImage *white = [[CIImage imageWithColor:CIColor.whiteColor] imageByCroppingToRect:extent];
        CIImage *expanded = [mask imageByApplyingFilter:@"CIMorphologyMaximum" withInputParameters:@{@"inputRadius": @12}];
        CIImage *border = [white imageByApplyingFilter:@"CIBlendWithAlphaMask"
                                     withInputParameters:@{kCIInputBackgroundImageKey: clear, @"inputMaskImage": expanded}];
        CIImage *composite = [source imageByApplyingFilter:@"CIBlendWithAlphaMask"
                                          withInputParameters:@{kCIInputBackgroundImageKey: border, @"inputMaskImage": mask}];
        CIContext *context = [CIContext contextWithOptions:@{kCIContextWorkingColorSpace: [NSNull null]}];
        CGImageRef cgImage = [context createCGImage:composite fromRect:extent];
        if (!cgImage) return nil;
        UIImage *result = [UIImage imageWithCGImage:cgImage scale:1 orientation:UIImageOrientationUp];
        CGImageRelease(cgImage);
        return result;
    }
    return nil;
}

@interface TagtagStickerEditor : UIViewController <PHPickerViewControllerDelegate, UIDocumentPickerDelegate,
    UIImagePickerControllerDelegate, UINavigationControllerDelegate, UIScrollViewDelegate, UITextFieldDelegate>
@property(nonatomic, copy) NSString *source;
@property(nonatomic, copy) NSString *receiver;
@property(nonatomic, copy) NSString *callback;
@property(nonatomic, strong) UIImage *image;
@property(nonatomic, strong) UIScrollView *cropView;
@property(nonatomic, strong) UIImageView *imageView;
@property(nonatomic, strong) UITextField *nameField;
@property(nonatomic, strong) UITextField *captionField;
@property(nonatomic, strong) UISwitch *cutoutSwitch;
@property(nonatomic, strong) UIButton *saveButton;
@property(nonatomic, strong) UILabel *hintLabel;
@property(nonatomic) BOOL initialCropLayout;
@property(nonatomic) BOOL finished;
@property(nonatomic) BOOL busy;
@end

@implementation TagtagStickerEditor

- (void)viewDidLoad {
    [super viewDidLoad];
    self.view.backgroundColor = [UIColor colorWithRed:0.97 green:0.96 blue:0.93 alpha:1];
    self.title = @"Create sticker";
    self.navigationItem.leftBarButtonItem = [[UIBarButtonItem alloc] initWithBarButtonSystemItem:UIBarButtonSystemItemCancel
                                                                                   target:self action:@selector(cancelTapped)];
    self.cropView = [UIScrollView new];
    self.cropView.delegate = self;
    self.cropView.bouncesZoom = YES;
    self.cropView.clipsToBounds = YES;
    self.cropView.backgroundColor = UIColor.darkGrayColor;
    self.cropView.accessibilityLabel = @"Drag and pinch to crop image";
    self.cropView.hidden = YES;
    [self.view addSubview:self.cropView];
    self.imageView = [UIImageView new];
    self.imageView.contentMode = UIViewContentModeScaleToFill;
    [self.cropView addSubview:self.imageView];

    self.hintLabel = [UILabel new];
    self.hintLabel.text = @"Drag and pinch to crop";
    self.hintLabel.font = [UIFont systemFontOfSize:13];
    self.hintLabel.textAlignment = NSTextAlignmentCenter;
    self.hintLabel.hidden = YES;
    [self.view addSubview:self.hintLabel];

    self.nameField = [UITextField new];
    self.nameField.borderStyle = UITextBorderStyleRoundedRect;
    self.nameField.placeholder = @"Sticker name";
    self.nameField.text = [self.source isEqualToString:@"polaroid"] ? @"My Polaroid" : @"My sticker";
    self.nameField.returnKeyType = UIReturnKeyDone;
    self.nameField.delegate = self;
    self.nameField.hidden = YES;
    [self.view addSubview:self.nameField];

    self.captionField = [UITextField new];
    self.captionField.borderStyle = UITextBorderStyleRoundedRect;
    self.captionField.placeholder = @"Caption (optional, 40 characters)";
    self.captionField.returnKeyType = UIReturnKeyDone;
    self.captionField.delegate = self;
    self.captionField.hidden = YES;
    [self.view addSubview:self.captionField];

    self.cutoutSwitch = [UISwitch new];
    self.cutoutSwitch.accessibilityLabel = @"Cut out subject with white border";
    self.cutoutSwitch.hidden = YES;
    [self.view addSubview:self.cutoutSwitch];
    UILabel *cutoutLabel = [UILabel new];
    cutoutLabel.tag = 101;
    cutoutLabel.text = @"Cut out subject + white border";
    cutoutLabel.font = [UIFont systemFontOfSize:15];
    cutoutLabel.hidden = YES;
    [self.view addSubview:cutoutLabel];

    self.saveButton = [UIButton buttonWithType:UIButtonTypeSystem];
    [self.saveButton setTitle:@"Save sticker" forState:UIControlStateNormal];
    self.saveButton.titleLabel.font = [UIFont boldSystemFontOfSize:18];
    self.saveButton.backgroundColor = [UIColor colorWithRed:1 green:0.84 blue:0.22 alpha:1];
    self.saveButton.tintColor = UIColor.blackColor;
    self.saveButton.layer.cornerRadius = 14;
    [self.saveButton addTarget:self action:@selector(saveTapped) forControlEvents:UIControlEventTouchUpInside];
    self.saveButton.hidden = YES;
    [self.view addSubview:self.saveButton];
}

- (void)viewDidAppear:(BOOL)animated {
    [super viewDidAppear:animated];
    if (self.image || self.busy || self.finished) return;
    self.busy = YES;
    if ([self.source isEqualToString:@"ai"]) {
        if (TagtagStickerCreationAIAvailable()) TagtagStickerCreationAIOpen((__bridge void *)self);
        else [self finish:@"unavailable" path:nil image:nil error:@"Image Playground is unavailable on this device."];
    } else if ([self.source isEqualToString:@"polaroid"]) {
        [self choosePolaroidSource];
    } else {
        [self chooseImportSource];
    }
}

- (void)viewDidLayoutSubviews {
    [super viewDidLayoutSubviews];
    CGRect safe = self.view.safeAreaLayoutGuide.layoutFrame;
    CGFloat margin = 22, width = safe.size.width - 2 * margin;
    CGFloat controls = [self.source isEqualToString:@"polaroid"] ? 214 : 166;
    CGFloat cropSize = MAX(100, MIN(width, safe.size.height - controls - 30));
    self.cropView.frame = CGRectMake((safe.size.width - cropSize) / 2, safe.origin.y + 10, cropSize, cropSize);
    CGFloat y = CGRectGetMaxY(self.cropView.frame) + 8;
    self.hintLabel.frame = CGRectMake(margin, y, width, 20);
    y += 28;
    self.nameField.frame = CGRectMake(margin, y, width, 38);
    y += 44;
    if ([self.source isEqualToString:@"polaroid"]) {
        self.captionField.frame = CGRectMake(margin, y, width, 38);
        y += 44;
    }
    self.cutoutSwitch.frame = CGRectMake(safe.size.width - margin - 51, y, 51, 31);
    [self.view viewWithTag:101].frame = CGRectMake(margin, y, width - 60, 31);
    self.saveButton.frame = CGRectMake(margin, CGRectGetMaxY(safe) - 54, width, 50);
    if (self.image && !self.initialCropLayout && cropSize > 0) {
        self.imageView.frame = CGRectMake(0, 0, self.image.size.width, self.image.size.height);
        self.cropView.contentSize = self.image.size;
        CGFloat zoom = MAX(cropSize / self.image.size.width, cropSize / self.image.size.height);
        self.cropView.minimumZoomScale = zoom;
        self.cropView.maximumZoomScale = MAX(zoom * 5, 1);
        self.cropView.zoomScale = zoom;
        self.cropView.contentOffset = CGPointMake((self.image.size.width * zoom - cropSize) / 2,
                                                   (self.image.size.height * zoom - cropSize) / 2);
        self.initialCropLayout = YES;
    }
}

- (UIView *)viewForZoomingInScrollView:(UIScrollView *)scrollView { return self.imageView; }

- (void)chooseImportSource {
    UIAlertController *sheet = [UIAlertController alertControllerWithTitle:@"Choose an image" message:nil preferredStyle:UIAlertControllerStyleActionSheet];
    [sheet addAction:[UIAlertAction actionWithTitle:@"Photos" style:UIAlertActionStyleDefault handler:^(UIAlertAction *action) { [self openPhotos]; }]];
    [sheet addAction:[UIAlertAction actionWithTitle:@"Files" style:UIAlertActionStyleDefault handler:^(UIAlertAction *action) { [self openFiles]; }]];
    [sheet addAction:[UIAlertAction actionWithTitle:@"Cancel" style:UIAlertActionStyleCancel handler:^(UIAlertAction *action) { [self cancelTapped]; }]];
    [self presentViewController:sheet animated:YES completion:nil];
}

- (void)choosePolaroidSource {
    UIAlertController *sheet = [UIAlertController alertControllerWithTitle:@"Polaroid photo" message:nil preferredStyle:UIAlertControllerStyleActionSheet];
    if ([UIImagePickerController isSourceTypeAvailable:UIImagePickerControllerSourceTypeCamera]) {
        [sheet addAction:[UIAlertAction actionWithTitle:@"Rear camera" style:UIAlertActionStyleDefault handler:^(UIAlertAction *action) { [self openCamera:UIImagePickerControllerCameraDeviceRear]; }]];
        if ([UIImagePickerController isCameraDeviceAvailable:UIImagePickerControllerCameraDeviceFront])
            [sheet addAction:[UIAlertAction actionWithTitle:@"Front camera" style:UIAlertActionStyleDefault handler:^(UIAlertAction *action) { [self openCamera:UIImagePickerControllerCameraDeviceFront]; }]];
    }
    [sheet addAction:[UIAlertAction actionWithTitle:@"Photos" style:UIAlertActionStyleDefault handler:^(UIAlertAction *action) { [self openPhotos]; }]];
    [sheet addAction:[UIAlertAction actionWithTitle:@"Files" style:UIAlertActionStyleDefault handler:^(UIAlertAction *action) { [self openFiles]; }]];
    [sheet addAction:[UIAlertAction actionWithTitle:@"Cancel" style:UIAlertActionStyleCancel handler:^(UIAlertAction *action) { [self cancelTapped]; }]];
    [self presentViewController:sheet animated:YES completion:nil];
}

- (void)openPhotos {
    PHPickerConfiguration *configuration = [[PHPickerConfiguration alloc] initWithPhotoLibrary:PHPhotoLibrary.sharedPhotoLibrary];
    configuration.filter = PHPickerFilter.imagesFilter;
    configuration.selectionLimit = 1;
    PHPickerViewController *picker = [[PHPickerViewController alloc] initWithConfiguration:configuration];
    picker.delegate = self;
    [self presentViewController:picker animated:YES completion:nil];
}

- (void)picker:(PHPickerViewController *)picker didFinishPicking:(NSArray<PHPickerResult *> *)results {
    [picker dismissViewControllerAnimated:YES completion:^{
        if (results.count == 0) { [self cancelTapped]; return; }
        NSItemProvider *provider = results.firstObject.itemProvider;
        if (![provider canLoadObjectOfClass:UIImage.class]) {
            [self finish:@"error" path:nil image:nil error:@"The selected photo could not be opened."]; return;
        }
        [provider loadObjectOfClass:UIImage.class completionHandler:^(id<NSItemProviderReading> object, NSError *error) {
            dispatch_async(dispatch_get_main_queue(), ^{
                if (error || ![object isKindOfClass:UIImage.class]) [self finish:@"error" path:nil image:nil error:@"The selected photo could not be opened."];
                else [self useImage:(UIImage *)object];
            });
        }];
    }];
}

- (void)openFiles {
    UIDocumentPickerViewController *picker = [[UIDocumentPickerViewController alloc] initForOpeningContentTypes:@[UTTypeImage] asCopy:YES];
    picker.delegate = self;
    [self presentViewController:picker animated:YES completion:nil];
}

- (void)documentPickerWasCancelled:(UIDocumentPickerViewController *)controller { [self cancelTapped]; }

- (void)documentPicker:(UIDocumentPickerViewController *)controller didPickDocumentsAtURLs:(NSArray<NSURL *> *)urls {
    NSURL *url = urls.firstObject;
    BOOL scoped = [url startAccessingSecurityScopedResource];
    NSData *data = url ? [NSData dataWithContentsOfURL:url options:NSDataReadingMappedIfSafe error:nil] : nil;
    if (scoped) [url stopAccessingSecurityScopedResource];
    UIImage *image = data ? [UIImage imageWithData:data] : nil;
    [controller dismissViewControllerAnimated:YES completion:^{
        if (image) [self useImage:image];
        else [self finish:@"error" path:nil image:nil error:@"The selected file is not a readable image."];
    }];
}

- (void)openCamera:(UIImagePickerControllerCameraDevice)device {
    if (self.finished) return;
    AVAuthorizationStatus status = [AVCaptureDevice authorizationStatusForMediaType:AVMediaTypeVideo];
    if (status == AVAuthorizationStatusDenied || status == AVAuthorizationStatusRestricted) {
        [self finish:@"unavailable" path:nil image:nil error:@"Camera access is unavailable. Enable it in Settings."];
        return;
    }
    if (status == AVAuthorizationStatusNotDetermined) {
        [AVCaptureDevice requestAccessForMediaType:AVMediaTypeVideo completionHandler:^(BOOL granted) {
            dispatch_async(dispatch_get_main_queue(), ^{
                if (granted) [self openCamera:device];
                else [self finish:@"unavailable" path:nil image:nil error:@"Camera access was denied."];
            });
        }];
        return;
    }
    UIImagePickerController *picker = [UIImagePickerController new];
    picker.sourceType = UIImagePickerControllerSourceTypeCamera;
    picker.cameraDevice = device;
    picker.allowsEditing = NO;
    picker.delegate = self;
    [self presentViewController:picker animated:YES completion:nil];
}

- (void)imagePickerControllerDidCancel:(UIImagePickerController *)picker { [self cancelTapped]; }

- (void)imagePickerController:(UIImagePickerController *)picker didFinishPickingMediaWithInfo:(NSDictionary<UIImagePickerControllerInfoKey, id> *)info {
    UIImage *image = info[UIImagePickerControllerOriginalImage];
    [picker dismissViewControllerAnimated:YES completion:^{
        if (image) [self useImage:image];
        else [self finish:@"error" path:nil image:nil error:@"The camera image could not be opened."];
    }];
}

- (void)useImage:(UIImage *)image {
    if (self.finished) return;
    self.image = TagtagNormalize(image);
    if (!self.image) { [self finish:@"error" path:nil image:nil error:@"The image has invalid dimensions."]; return; }
    self.imageView.image = self.image;
    self.initialCropLayout = NO;
    self.busy = NO;
    self.cropView.hidden = NO;
    self.hintLabel.hidden = NO;
    self.nameField.hidden = NO;
    self.captionField.hidden = ![self.source isEqualToString:@"polaroid"];
    BOOL cutoutAvailable = NO;
    if (@available(iOS 17.0, *)) cutoutAvailable = YES;
    self.cutoutSwitch.hidden = !cutoutAvailable;
    [self.view viewWithTag:101].hidden = self.cutoutSwitch.hidden;
    self.saveButton.hidden = NO;
    [self.view setNeedsLayout];
}

- (BOOL)textFieldShouldReturn:(UITextField *)textField { [textField resignFirstResponder]; return YES; }

- (BOOL)textField:(UITextField *)textField shouldChangeCharactersInRange:(NSRange)range replacementString:(NSString *)string {
    if (textField != self.captionField) return YES;
    NSString *result = [textField.text stringByReplacingCharactersInRange:range withString:string];
    __block NSUInteger count = 0;
    [result enumerateSubstringsInRange:NSMakeRange(0, result.length) options:NSStringEnumerationByComposedCharacterSequences
                           usingBlock:^(NSString *substring, NSRange substringRange, NSRange enclosingRange, BOOL *stop) { count++; }];
    return count <= 40;
}

- (UIImage *)croppedImage {
    CGFloat zoom = self.cropView.zoomScale;
    CGSize viewport = self.cropView.bounds.size;
    CGRect crop = CGRectMake(self.cropView.contentOffset.x / zoom, self.cropView.contentOffset.y / zoom,
                             viewport.width / zoom, viewport.height / zoom);
    crop = CGRectIntersection(CGRectMake(0, 0, self.image.size.width, self.image.size.height), crop);
    if (CGRectIsNull(crop) || crop.size.width < 1 || crop.size.height < 1) return nil;
    CGFloat side = MIN(crop.size.width, crop.size.height);
    crop.origin.x += (crop.size.width - side) / 2;
    crop.origin.y += (crop.size.height - side) / 2;
    crop.size = CGSizeMake(side, side);
    CGImageRef piece = CGImageCreateWithImageInRect(self.image.CGImage, crop);
    if (!piece) return nil;
    UIImage *result = [UIImage imageWithCGImage:piece scale:1 orientation:UIImageOrientationUp];
    CGImageRelease(piece);
    return result;
}

- (UIImage *)polaroidWithPhoto:(UIImage *)photo caption:(NSString *)caption {
    CGSize size = CGSizeMake(900, 1024);
    UIGraphicsImageRendererFormat *format = [UIGraphicsImageRendererFormat defaultFormat];
    format.scale = 1;
    format.opaque = NO;
    UIGraphicsImageRenderer *renderer = [[UIGraphicsImageRenderer alloc] initWithSize:size format:format];
    return [renderer imageWithActions:^(UIGraphicsImageRendererContext *context) {
        UIColor *paper = [UIColor colorWithRed:1 green:0.995 blue:0.975 alpha:1];
        [paper setFill];
        [[UIBezierPath bezierPathWithRoundedRect:CGRectMake(8, 8, 884, 1008) cornerRadius:12] fill];
        [photo drawInRect:CGRectMake(54, 54, 792, 792)];
        if (caption.length) {
            UIFont *font = [UIFont fontWithName:@"MarkerFelt-Wide" size:40] ?: [UIFont systemFontOfSize:40 weight:UIFontWeightMedium];
            NSMutableParagraphStyle *paragraph = [NSMutableParagraphStyle new];
            paragraph.alignment = NSTextAlignmentCenter;
            paragraph.lineBreakMode = NSLineBreakByTruncatingTail;
            [caption drawInRect:CGRectMake(60, 878, 780, 70) withAttributes:@{NSFontAttributeName: font,
              NSForegroundColorAttributeName: UIColor.darkGrayColor, NSParagraphStyleAttributeName: paragraph}];
        }
    }];
}

- (void)saveTapped {
    if (self.busy || self.finished) return;
    [self.view endEditing:YES];
    UIImage *cropped = [self croppedImage];
    if (!cropped) { [self showError:@"Could not crop this image."]; return; }
    BOOL cutout = self.cutoutSwitch.isOn && !self.cutoutSwitch.hidden;
    NSString *caption = [self.captionField.text copy] ?: @"";
    self.busy = YES;
    self.saveButton.enabled = NO;
    [self.saveButton setTitle:@"Saving…" forState:UIControlStateNormal];
    dispatch_async(dispatch_get_global_queue(QOS_CLASS_USER_INITIATED, 0), ^{
        UIImage *art = TagtagRender(cropped, CGSizeMake(792, 792), NO);
        NSError *error = nil;
        if (cutout) art = TagtagCutout(art, &error);
        if (art && [self.source isEqualToString:@"polaroid"]) art = [self polaroidWithPhoto:art caption:caption];
        NSData *png = nil;
        CGSize finalSize = art.size;
        for (NSUInteger attempt = 0; art && attempt < 8; attempt++) {
            CGFloat scale = MIN(1, TagtagMaximumEdge / MAX(art.size.width, art.size.height));
            if (scale < 1) art = TagtagRender(art, CGSizeMake(floor(art.size.width * scale), floor(art.size.height * scale)), NO);
            png = UIImagePNGRepresentation(art);
            finalSize = art.size;
            if (png.length > 0 && png.length <= TagtagMaximumPNGBytes) break;
            CGFloat shrink = 0.8;
            art = TagtagRender(art, CGSizeMake(MAX(1, floor(art.size.width * shrink)), MAX(1, floor(art.size.height * shrink))), NO);
        }
        NSString *path = nil;
        if (png.length > 0 && png.length <= TagtagMaximumPNGBytes && finalSize.width <= 1024 && finalSize.height <= 1024) {
            NSString *directory = [NSSearchPathForDirectoriesInDomains(NSApplicationSupportDirectory, NSUserDomainMask, YES).firstObject
                                   stringByAppendingPathComponent:@"StickerCreations"];
            [[NSFileManager defaultManager] createDirectoryAtPath:directory withIntermediateDirectories:YES attributes:nil error:&error];
            path = [directory stringByAppendingPathComponent:[NSUUID.UUID.UUIDString stringByAppendingPathExtension:@"png"]];
            if (![png writeToFile:path options:NSDataWritingAtomic error:&error]) path = nil;
        }
        NSString *completedPath = path;
        CGSize completedSize = finalSize;
        dispatch_async(dispatch_get_main_queue(), ^{
            if (self.finished) {
                if (completedPath) [[NSFileManager defaultManager] removeItemAtPath:completedPath error:nil];
                return;
            }
            if (completedPath) [self finish:@"success" path:completedPath image:art error:nil dimensions:completedSize];
            else {
                self.busy = NO;
                self.saveButton.enabled = YES;
                [self.saveButton setTitle:@"Save sticker" forState:UIControlStateNormal];
                [self showError:error.localizedDescription ?: @"Could not save a PNG under 5 MiB. Try another crop."];
            }
        });
    });
}

- (void)showError:(NSString *)message {
    UIAlertController *alert = [UIAlertController alertControllerWithTitle:@"Sticker not saved" message:message preferredStyle:UIAlertControllerStyleAlert];
    [alert addAction:[UIAlertAction actionWithTitle:@"OK" style:UIAlertActionStyleDefault handler:nil]];
    [self presentViewController:alert animated:YES completion:nil];
}

- (void)cancelTapped { [self finish:@"cancelled" path:nil image:nil error:nil]; }

- (void)finish:(NSString *)status path:(NSString *)path image:(UIImage *)image error:(NSString *)error {
    [self finish:status path:path image:image error:error dimensions:CGSizeZero];
}

- (void)finish:(NSString *)status path:(NSString *)path image:(UIImage *)image error:(NSString *)error dimensions:(CGSize)dimensions {
    if (self.finished) return;
    self.finished = YES;
    if ([self.source isEqualToString:@"ai"]) TagtagStickerCreationAICancel();
    NSString *name = [self.nameField.text stringByTrimmingCharactersInSet:NSCharacterSet.whitespaceAndNewlineCharacterSet];
    if (!name.length) name = [self.source isEqualToString:@"polaroid"] ? @"My Polaroid" : @"My sticker";
    NSString *kind = [self.source isEqualToString:@"polaroid"] ? @"polaroid" : ([self.source isEqualToString:@"ai"] ? @"ai" : @"image");
    NSDictionary *payload = @{@"status": status ?: @"error", @"path": path ?: @"", @"name": name,
                              @"kind": kind, @"width": @((NSInteger)dimensions.width), @"height": @((NSInteger)dimensions.height),
                              @"error": error ?: @""};
    NSData *json = [NSJSONSerialization dataWithJSONObject:payload options:0 error:nil];
    NSString *message = [[NSString alloc] initWithData:json encoding:NSUTF8StringEncoding] ?: @"{\"status\":\"error\"}";
    [self.navigationController.presentingViewController dismissViewControllerAnimated:YES completion:^{
        UnitySendMessage(self.receiver.UTF8String, self.callback.UTF8String, message.UTF8String);
        if (activeEditor == self) activeEditor = nil;
    }];
}
@end

extern "C" void TagtagStickerCreationAIFinished(const char *path) {
    dispatch_async(dispatch_get_main_queue(), ^{
        if (!activeEditor || activeEditor.finished) return;
        UIImage *image = path ? [UIImage imageWithContentsOfFile:@(path)] : nil;
        if (image) [activeEditor useImage:image];
        else [activeEditor finish:@"error" path:nil image:nil error:@"Image Playground did not return a readable image."];
    });
}

extern "C" void TagtagStickerCreationAICancelled(void) {
    dispatch_async(dispatch_get_main_queue(), ^{
        [activeEditor finish:@"cancelled" path:nil image:nil error:nil];
    });
}

extern "C" int TagtagStickerCreationAvailable(void) {
    int flags = 1;
    if ([UIImagePickerController isSourceTypeAvailable:UIImagePickerControllerSourceTypeCamera]) flags |= 2;
    if (TagtagStickerCreationAIAvailable()) flags |= 4;
    if (@available(iOS 17.0, *)) flags |= 8;
    return flags;
}

extern "C" void TagtagStickerCreationOpen(const char *source, const char *receiver, const char *callback) {
    NSString *sourceValue = source ? @(source) : @"";
    NSString *receiverValue = receiver ? @(receiver) : @"";
    NSString *callbackValue = callback ? @(callback) : @"";
    dispatch_async(dispatch_get_main_queue(), ^{
        if (!receiverValue.length || !callbackValue.length) return;
        if (activeEditor) {
            TagtagSendResult(receiverValue, callbackValue, @"error", @"A sticker editor is already open.");
            return;
        }
        if (![@[@"import", @"ai", @"polaroid"] containsObject:sourceValue]) {
            TagtagSendResult(receiverValue, callbackValue, @"unavailable", @"Unknown sticker source.");
            return;
        }
        UIViewController *presenter = TagtagTopController();
        if (!presenter) {
            TagtagSendResult(receiverValue, callbackValue, @"unavailable", @"The editor cannot be presented right now.");
            return;
        }
        TagtagStickerEditor *editor = [TagtagStickerEditor new];
        editor.source = sourceValue;
        editor.receiver = receiverValue;
        editor.callback = callbackValue;
        UINavigationController *navigation = [[UINavigationController alloc] initWithRootViewController:editor];
        navigation.modalPresentationStyle = UIModalPresentationFullScreen;
        activeEditor = editor;
        [presenter presentViewController:navigation animated:YES completion:nil];
    });
}

extern "C" void TagtagStickerCreationCancel(void) {
    dispatch_async(dispatch_get_main_queue(), ^{ [activeEditor cancelTapped]; });
}
