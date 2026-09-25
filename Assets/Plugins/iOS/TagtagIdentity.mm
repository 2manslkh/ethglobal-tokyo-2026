#import <AuthenticationServices/AuthenticationServices.h>
#import <CommonCrypto/CommonDigest.h>
#import <Security/Security.h>
#import <UIKit/UIKit.h>
#include <cstdlib>
#include <cstring>
#include "Unity/UnityInterface.h"

static NSString *TagtagRandomString() {
    uint8_t bytes[32];
    if (SecRandomCopyBytes(kSecRandomDefault, sizeof(bytes), bytes) != errSecSuccess) return nil;
    static const char alphabet[] = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
    NSMutableString *value = [NSMutableString stringWithCapacity:32];
    for (int i = 0; i < 32; i++) [value appendFormat:@"%c", alphabet[bytes[i] & 63]];
    return value;
}

static NSString *TagtagSha256Base64Url(NSString *input) {
    NSData *data = [input dataUsingEncoding:NSUTF8StringEncoding];
    uint8_t digest[CC_SHA256_DIGEST_LENGTH];
    CC_SHA256(data.bytes, (CC_LONG)data.length, digest);
    NSString *encoded = [[NSData dataWithBytes:digest length:sizeof(digest)] base64EncodedStringWithOptions:0];
    return [[[encoded stringByReplacingOccurrencesOfString:@"+" withString:@"-"]
             stringByReplacingOccurrencesOfString:@"/" withString:@"_"]
             stringByTrimmingCharactersInSet:[NSCharacterSet characterSetWithCharactersInString:@"="]];
}

static NSString *TagtagSha256Hex(NSString *input) {
    NSData *data = [input dataUsingEncoding:NSUTF8StringEncoding];
    uint8_t digest[CC_SHA256_DIGEST_LENGTH];
    CC_SHA256(data.bytes, (CC_LONG)data.length, digest);
    NSMutableString *hex = [NSMutableString stringWithCapacity:64];
    for (int i = 0; i < CC_SHA256_DIGEST_LENGTH; i++) [hex appendFormat:@"%02x", digest[i]];
    return hex;
}

static NSString *TagtagFormEscape(NSString *value) {
    NSMutableCharacterSet *safe = [NSCharacterSet URLQueryAllowedCharacterSet].mutableCopy;
    [safe removeCharactersInString:@"+&=?#/%"];
    return [value stringByAddingPercentEncodingWithAllowedCharacters:safe];
}

@interface TagtagAuthBridge : NSObject <ASAuthorizationControllerDelegate, ASAuthorizationControllerPresentationContextProviding, ASWebAuthenticationPresentationContextProviding>
@property(nonatomic, strong) ASAuthorizationController *appleController;
@property(nonatomic, strong) ASWebAuthenticationSession *googleSession;
@property(nonatomic, copy) NSString *nonce;
@property(nonatomic, copy) NSString *codeVerifier;
@property(nonatomic, copy) NSString *clientId;
@property(nonatomic, copy) NSString *redirectUri;
@property(nonatomic, copy) NSString *result;
@property(nonatomic) BOOL busy;
@property(nonatomic) NSUInteger generation;
@end

@implementation TagtagAuthBridge
- (ASPresentationAnchor)presentationAnchorForAuthorizationController:(ASAuthorizationController *)controller {
    return UnityGetMainWindow();
}
- (ASPresentationAnchor)presentationAnchorForWebAuthenticationSession:(ASWebAuthenticationSession *)session {
    return UnityGetMainWindow();
}
- (void)finish:(NSDictionary *)payload {
    NSData *data = [NSJSONSerialization dataWithJSONObject:payload options:0 error:nil];
    self.result = data ? [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding] : @"{\"error\":\"Sign-in failed.\"}";
    self.busy = NO;
    self.appleController = nil;
    self.googleSession = nil;
    self.nonce = nil;
    self.codeVerifier = nil;
}
- (void)authorizationController:(ASAuthorizationController *)controller didCompleteWithAuthorization:(ASAuthorization *)authorization {
    if (!self.busy || controller != self.appleController) return;
    if (![authorization.credential isKindOfClass:ASAuthorizationAppleIDCredential.class]) {
        [self finish:@{@"error": @"Apple did not return an identity credential."}]; return;
    }
    ASAuthorizationAppleIDCredential *credential = authorization.credential;
    NSString *token = [[NSString alloc] initWithData:credential.identityToken encoding:NSUTF8StringEncoding];
    if (!token.length || !self.nonce.length) {
        [self finish:@{@"error": @"Apple sign-in did not return a token."}]; return;
    }
    [self finish:@{@"providerId": @"apple.com", @"idToken": token, @"rawNonce": self.nonce}];
}
- (void)authorizationController:(ASAuthorizationController *)controller didCompleteWithError:(NSError *)error {
    if (controller != self.appleController) return;
    NSString *message = [error.domain isEqualToString:ASAuthorizationErrorDomain] && error.code == ASAuthorizationErrorCanceled ?
        @"Sign-in cancelled." : @"Apple sign-in did not finish. Try again.";
    [self finish:@{@"error": message}];
}
- (void)exchangeGoogleCode:(NSString *)code generation:(NSUInteger)generation {
    NSString *body = [NSString stringWithFormat:@"code=%@&client_id=%@&redirect_uri=%@&grant_type=authorization_code&code_verifier=%@",
        TagtagFormEscape(code), TagtagFormEscape(self.clientId), TagtagFormEscape(self.redirectUri), TagtagFormEscape(self.codeVerifier)];
    NSMutableURLRequest *request = [NSMutableURLRequest requestWithURL:[NSURL URLWithString:@"https://oauth2.googleapis.com/token"]];
    request.HTTPMethod = @"POST";
    [request setValue:@"application/x-www-form-urlencoded" forHTTPHeaderField:@"Content-Type"];
    request.HTTPBody = [body dataUsingEncoding:NSUTF8StringEncoding];
    [[[NSURLSession sharedSession] dataTaskWithRequest:request completionHandler:^(NSData *data, NSURLResponse *response, NSError *error) {
        dispatch_async(dispatch_get_main_queue(), ^{
            if (generation != self.generation || !self.busy) return;
            NSDictionary *json = data ? [NSJSONSerialization JSONObjectWithData:data options:0 error:nil] : nil;
            NSString *idToken = [json[@"id_token"] isKindOfClass:NSString.class] ? json[@"id_token"] : nil;
            NSString *accessToken = [json[@"access_token"] isKindOfClass:NSString.class] ? json[@"access_token"] : nil;
            if (error || !idToken.length || !accessToken.length || ![(NSHTTPURLResponse *)response statusCode] ||
                [(NSHTTPURLResponse *)response statusCode] >= 400) {
                [self finish:@{@"error": @"Google sign-in did not return a credential."}]; return;
            }
            [self finish:@{@"providerId": @"google.com", @"idToken": idToken, @"accessToken": accessToken}];
        });
    }] resume];
}
@end

static TagtagAuthBridge *tagtagAuth;

extern "C" bool TagtagIdentityBegin(const char *provider, const char *googleClientId, const char *googleReversedClientId) {
    if (!NSThread.isMainThread) return false;
    if (!tagtagAuth) tagtagAuth = [TagtagAuthBridge new];
    if (tagtagAuth.busy) return false;
    tagtagAuth.busy = YES;
    tagtagAuth.result = nil;
    tagtagAuth.generation++;
    NSString *name = provider ? @(provider) : @"";
    if ([name isEqualToString:@"apple"]) {
        tagtagAuth.nonce = TagtagRandomString();
        if (!tagtagAuth.nonce) { [tagtagAuth finish:@{@"error": @"Secure sign-in could not start."}]; return true; }
        ASAuthorizationAppleIDRequest *request = [[ASAuthorizationAppleIDProvider new] createRequest];
        request.nonce = TagtagSha256Hex(tagtagAuth.nonce);
        request.requestedScopes = @[ASAuthorizationScopeFullName, ASAuthorizationScopeEmail];
        tagtagAuth.appleController = [[ASAuthorizationController alloc] initWithAuthorizationRequests:@[request]];
        tagtagAuth.appleController.delegate = tagtagAuth;
        tagtagAuth.appleController.presentationContextProvider = tagtagAuth;
        [tagtagAuth.appleController performRequests];
        return true;
    }
    if ([name isEqualToString:@"google"]) {
        NSString *clientId = googleClientId ? @(googleClientId) : @"";
        NSString *scheme = googleReversedClientId ? @(googleReversedClientId) : @"";
        if (!clientId.length || !scheme.length) {
            [tagtagAuth finish:@{@"error": @"Google sign-in is not configured."}]; return true;
        }
        tagtagAuth.clientId = clientId;
        tagtagAuth.redirectUri = [scheme stringByAppendingString:@":/oauth2redirect"];
        tagtagAuth.codeVerifier = TagtagRandomString();
        NSString *state = TagtagRandomString();
        if (!tagtagAuth.codeVerifier || !state) {
            [tagtagAuth finish:@{@"error": @"Secure sign-in could not start."}]; return true;
        }
        NSString *url = [NSString stringWithFormat:@"https://accounts.google.com/o/oauth2/v2/auth?client_id=%@&redirect_uri=%@&response_type=code&scope=openid%%20email%%20profile&code_challenge=%@&code_challenge_method=S256&state=%@",
            TagtagFormEscape(clientId), TagtagFormEscape(tagtagAuth.redirectUri),
            TagtagFormEscape(TagtagSha256Base64Url(tagtagAuth.codeVerifier)), TagtagFormEscape(state)];
        NSUInteger generation = tagtagAuth.generation;
        tagtagAuth.googleSession = [[ASWebAuthenticationSession alloc] initWithURL:[NSURL URLWithString:url]
            callbackURLScheme:scheme completionHandler:^(NSURL *callback, NSError *error) {
            if (generation != tagtagAuth.generation || !tagtagAuth.busy) return;
            if (error || !callback) {
                NSString *message = error.code == ASWebAuthenticationSessionErrorCodeCanceledLogin ?
                    @"Sign-in cancelled." : @"Google sign-in did not finish. Try again.";
                [tagtagAuth finish:@{@"error": message}]; return;
            }
            NSURLComponents *components = [NSURLComponents componentsWithURL:callback resolvingAgainstBaseURL:NO];
            NSString *code = nil, *returnedState = nil;
            for (NSURLQueryItem *item in components.queryItems) {
                if ([item.name isEqualToString:@"code"]) code = item.value;
                if ([item.name isEqualToString:@"state"]) returnedState = item.value;
            }
            if (!code.length || ![returnedState isEqualToString:state]) {
                [tagtagAuth finish:@{@"error": @"Google sign-in response was invalid."}]; return;
            }
            [tagtagAuth exchangeGoogleCode:code generation:generation];
        }];
        tagtagAuth.googleSession.presentationContextProvider = tagtagAuth;
        [tagtagAuth.googleSession start];
        return true;
    }
    [tagtagAuth finish:@{@"error": @"Choose Apple or Google."}];
    return true;
}

extern "C" char *TagtagIdentityPoll() {
    if (!tagtagAuth.result) return nullptr;
    char *copy = strdup(tagtagAuth.result.UTF8String);
    tagtagAuth.result = nil;
    return copy;
}

extern "C" void TagtagIdentityFree(char *pointer) { free(pointer); }

extern "C" void TagtagIdentityCancel() {
    tagtagAuth.generation++;
    [tagtagAuth.googleSession cancel];
    [tagtagAuth.appleController cancel];
    tagtagAuth.googleSession = nil;
    tagtagAuth.appleController = nil;
    tagtagAuth.busy = NO;
    tagtagAuth.result = nil;
}

static NSMutableDictionary *TagtagKeychainQuery() {
    NSString *service = [NSBundle.mainBundle.bundleIdentifier stringByAppendingString:@".session"];
    return [@{(__bridge id)kSecClass: (__bridge id)kSecClassGenericPassword,
              (__bridge id)kSecAttrService: service,
              (__bridge id)kSecAttrAccount: @"current"} mutableCopy];
}

extern "C" bool TagtagSessionStore(const char *value) {
    if (!value) return false;
    NSMutableDictionary *query = TagtagKeychainQuery();
    NSData *data = [@(value) dataUsingEncoding:NSUTF8StringEncoding];
    SecItemDelete((__bridge CFDictionaryRef)query);
    query[(__bridge id)kSecValueData] = data;
    query[(__bridge id)kSecAttrAccessible] = (__bridge id)kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly;
    return SecItemAdd((__bridge CFDictionaryRef)query, NULL) == errSecSuccess;
}

extern "C" char *TagtagSessionLoad() {
    NSMutableDictionary *query = TagtagKeychainQuery();
    query[(__bridge id)kSecReturnData] = @YES;
    query[(__bridge id)kSecMatchLimit] = (__bridge id)kSecMatchLimitOne;
    CFTypeRef item = NULL;
    if (SecItemCopyMatching((__bridge CFDictionaryRef)query, &item) != errSecSuccess || !item) return nullptr;
    NSString *value = [[NSString alloc] initWithData:(__bridge_transfer NSData *)item encoding:NSUTF8StringEncoding];
    return value ? strdup(value.UTF8String) : nullptr;
}

extern "C" void TagtagSessionClear() { SecItemDelete((__bridge CFDictionaryRef)TagtagKeychainQuery()); }
