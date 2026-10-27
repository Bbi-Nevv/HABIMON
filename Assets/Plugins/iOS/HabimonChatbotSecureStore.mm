#import <Foundation/Foundation.h>
#import <Security/Security.h>
#include <stdlib.h>
#include <string.h>

static NSString *HabimonString(const char *value) {
    if (value == NULL) return nil;
    return [NSString stringWithUTF8String:value];
}

extern "C" int HabimonKeychainSet(const char *serviceValue, const char *accountValue, const char *secretValue) {
    @autoreleasepool {
        NSString *service = HabimonString(serviceValue);
        NSString *account = HabimonString(accountValue);
        NSString *secret = HabimonString(secretValue);
        if (service == nil || account == nil || secret == nil) return (int)errSecParam;

        NSData *data = [secret dataUsingEncoding:NSUTF8StringEncoding];
        NSDictionary *query = @{
            (__bridge id)kSecClass: (__bridge id)kSecClassGenericPassword,
            (__bridge id)kSecAttrService: service,
            (__bridge id)kSecAttrAccount: account
        };
        NSDictionary *attributes = @{
            (__bridge id)kSecValueData: data,
            (__bridge id)kSecAttrAccessible: (__bridge id)kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly
        };

        OSStatus status = SecItemUpdate((__bridge CFDictionaryRef)query, (__bridge CFDictionaryRef)attributes);
        if (status == errSecItemNotFound) {
            NSMutableDictionary *item = [query mutableCopy];
            [item addEntriesFromDictionary:attributes];
            status = SecItemAdd((__bridge CFDictionaryRef)item, NULL);
        }
        return (int)status;
    }
}

extern "C" int HabimonKeychainGet(const char *serviceValue, const char *accountValue, char **output) {
    @autoreleasepool {
        if (output == NULL) return (int)errSecParam;
        *output = NULL;
        NSString *service = HabimonString(serviceValue);
        NSString *account = HabimonString(accountValue);
        if (service == nil || account == nil) return (int)errSecParam;

        NSDictionary *query = @{
            (__bridge id)kSecClass: (__bridge id)kSecClassGenericPassword,
            (__bridge id)kSecAttrService: service,
            (__bridge id)kSecAttrAccount: account,
            (__bridge id)kSecReturnData: @YES,
            (__bridge id)kSecMatchLimit: (__bridge id)kSecMatchLimitOne
        };
        CFTypeRef result = NULL;
        OSStatus status = SecItemCopyMatching((__bridge CFDictionaryRef)query, &result);
        if (status != errSecSuccess || result == NULL) return (int)status;

        NSData *data = CFBridgingRelease(result);
        NSString *value = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
        if (value == nil) return (int)errSecDecode;
        const char *utf8 = [value UTF8String];
        size_t length = strlen(utf8);
        char *copy = (char *)malloc(length + 1);
        if (copy == NULL) return (int)errSecAllocate;
        memcpy(copy, utf8, length + 1);
        *output = copy;
        return (int)errSecSuccess;
    }
}

extern "C" void HabimonKeychainFree(char *value) { free(value); }

extern "C" int HabimonKeychainRemove(const char *serviceValue, const char *accountValue) {
    @autoreleasepool {
        NSString *service = HabimonString(serviceValue);
        NSString *account = HabimonString(accountValue);
        if (service == nil || account == nil) return (int)errSecParam;
        NSDictionary *query = @{
            (__bridge id)kSecClass: (__bridge id)kSecClassGenericPassword,
            (__bridge id)kSecAttrService: service,
            (__bridge id)kSecAttrAccount: account
        };
        OSStatus status = SecItemDelete((__bridge CFDictionaryRef)query);
        return status == errSecItemNotFound ? (int)errSecSuccess : (int)status;
    }
}
