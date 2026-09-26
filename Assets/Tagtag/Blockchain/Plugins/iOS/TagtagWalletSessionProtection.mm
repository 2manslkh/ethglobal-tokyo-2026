#import <Foundation/Foundation.h>

extern "C" int TagtagPrepareProtectedWalletSessionFile(const char *directoryUtf8, const char *fileUtf8)
{
    @autoreleasepool
    {
        if (directoryUtf8 == nullptr || fileUtf8 == nullptr) return 0;

        NSString *directory = [NSString stringWithUTF8String:directoryUtf8];
        NSString *file = [NSString stringWithUTF8String:fileUtf8];
        if (directory == nil || file == nil) return 0;

        NSFileManager *manager = [NSFileManager defaultManager];
        NSDictionary *directoryAttributes = @{
            NSFileProtectionKey: NSFileProtectionComplete,
            NSFilePosixPermissions: @(0700)
        };
        NSError *error = nil;
        if (![manager createDirectoryAtPath:directory
                 withIntermediateDirectories:YES
                                  attributes:directoryAttributes
                                       error:&error]) return 0;

        NSDictionary *fileAttributes = @{
            NSFileProtectionKey: NSFileProtectionComplete,
            NSFilePosixPermissions: @(0600)
        };
        if (![manager createFileAtPath:file contents:[NSData data] attributes:fileAttributes]) return 0;

        NSDictionary *actualAttributes = [manager attributesOfItemAtPath:file error:&error];
        if (actualAttributes == nil) return 0;
        return [actualAttributes[NSFileProtectionKey] isEqualToString:NSFileProtectionComplete] ? 1 : 0;
    }
}
