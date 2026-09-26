#import <Foundation/Foundation.h>
#import <Security/Security.h>

static NSMutableDictionary *WalletQuery(const char *userId)
{
    if (userId == nullptr) return nil;
    NSString *account = [NSString stringWithUTF8String:userId];
    if (account.length == 0) return nil;
    return [@{
        (__bridge id)kSecClass: (__bridge id)kSecClassGenericPassword,
        (__bridge id)kSecAttrService: @"com.kenk.tagtag.phone-wallet.v1",
        (__bridge id)kSecAttrAccount: account
    } mutableCopy];
}

extern "C" char *TagtagWalletSeedLoad(const char *userId, int *status)
{
    @autoreleasepool
    {
        if (status == nullptr) return nullptr;
        *status = -1;
        NSMutableDictionary *query = WalletQuery(userId);
        if (query == nil) return nullptr;
        query[(__bridge id)kSecReturnData] = @YES;
        query[(__bridge id)kSecMatchLimit] = (__bridge id)kSecMatchLimitOne;
        CFTypeRef result = nullptr;
        OSStatus resultStatus = SecItemCopyMatching((__bridge CFDictionaryRef)query, &result);
        if (resultStatus == errSecItemNotFound) { *status = 1; return nullptr; }
        if (resultStatus != errSecSuccess || result == nullptr) return nullptr;
        NSData *data = CFBridgingRelease(result);
        char *copy = (char *)calloc(data.length + 1, 1);
        if (copy != nullptr) memcpy(copy, data.bytes, data.length);
        if (copy != nullptr) *status = 0;
        return copy;
    }
}

extern "C" bool TagtagWalletSeedSave(const char *userId, const char *phrase)
{
    @autoreleasepool
    {
        NSMutableDictionary *query = WalletQuery(userId);
        if (query == nil || phrase == nullptr) return false;
        NSData *value = [[NSString stringWithUTF8String:phrase] dataUsingEncoding:NSUTF8StringEncoding];
        if (value.length == 0) return false;
        NSDictionary *update = @{ (__bridge id)kSecValueData: value };
        OSStatus result = SecItemUpdate((__bridge CFDictionaryRef)query, (__bridge CFDictionaryRef)update);
        if (result == errSecItemNotFound)
        {
            query[(__bridge id)kSecValueData] = value;
            query[(__bridge id)kSecAttrAccessible] = (__bridge id)kSecAttrAccessibleWhenUnlockedThisDeviceOnly;
            result = SecItemAdd((__bridge CFDictionaryRef)query, nullptr);
        }
        return result == errSecSuccess;
    }
}

extern "C" void TagtagWalletSeedFree(char *pointer)
{
    if (pointer == nullptr) return;
    memset(pointer, 0, strlen(pointer));
    free(pointer);
}
