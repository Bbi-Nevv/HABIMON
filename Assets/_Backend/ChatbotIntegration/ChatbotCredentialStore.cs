using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// Stores an opaque HABIMON chat owner token separately for each authenticated Firebase UID.
/// The server still treats this token as an installation bearer secret, not as a Firebase identity.
/// </summary>
public static class ChatbotCredentialStore
{
    private const string ServiceName = "com.gsam.habimon.chat-owner";

    public static string GetOrCreate(string firebaseUid)
    {
        if (string.IsNullOrWhiteSpace(firebaseUid))
            throw new ArgumentException("A signed-in Firebase UID is required.", nameof(firebaseUid));

        string account = Sha256Hex(firebaseUid);
        string existing = Read(account);
        if (IsValid(existing))
            return existing;
        if (existing != null)
            throw new InvalidOperationException("The stored chatbot credential is invalid; it was not replaced.");

        // 256 random bits. Never derive this value from the UID, PIN, password or device ID.
        byte[] bytes = new byte[32];
        using (RandomNumberGenerator random = RandomNumberGenerator.Create())
            random.GetBytes(bytes);
        string created = Base64Url(bytes);
        Array.Clear(bytes, 0, bytes.Length);

        // A native persistent secret store is mandatory on supported mobile targets. If the
        // platform bridge is missing or locked, fail closed instead of silently changing owner.
        Write(account, created);
        string readBack = Read(account);
        if (!string.Equals(created, readBack, StringComparison.Ordinal))
            throw new InvalidOperationException("The chatbot credential could not be persisted securely.");
        return created;
    }

    private static bool IsValid(string value)
    {
        return !string.IsNullOrWhiteSpace(value) && value.Length >= 40 && value.Length <= 64;
    }

    private static string Read(string account)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using (AndroidJavaClass store = new AndroidJavaClass("com.gsam.habimon.chatbot.ChatbotSecureStore"))
        using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            return store.CallStatic<string>("get", activity, account);
#elif UNITY_IOS && !UNITY_EDITOR
        IntPtr value;
        int status = HabimonKeychainGet(ServiceName, account, out value);
        if (status == -25300) // errSecItemNotFound
            return null;
        if (status != 0 || value == IntPtr.Zero)
            throw new InvalidOperationException("iOS Keychain could not read the chatbot credential (" + status + ").");
        try { return Marshal.PtrToStringAnsi(value); }
        finally { HabimonKeychainFree(value); }
#elif UNITY_EDITOR
        return ChatbotEditorCredentialStore.Get(ServiceName, account);
#else
        throw new PlatformNotSupportedException("Chatbot credentials require Android Keystore or iOS Keychain.");
#endif
    }

    private static void Write(string account, string value)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using (AndroidJavaClass store = new AndroidJavaClass("com.gsam.habimon.chatbot.ChatbotSecureStore"))
        using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
        {
            if (!store.CallStatic<bool>("put", activity, account, value))
                throw new InvalidOperationException("Android Keystore could not persist the chatbot credential.");
        }
#elif UNITY_IOS && !UNITY_EDITOR
        if (HabimonKeychainSet(ServiceName, account, value) != 0)
            throw new InvalidOperationException("iOS Keychain could not persist the chatbot credential.");
#elif UNITY_EDITOR
        ChatbotEditorCredentialStore.Set(ServiceName, account, value);
#else
        throw new PlatformNotSupportedException("Chatbot credentials require Android Keystore or iOS Keychain.");
#endif
    }

    private static string Sha256Hex(string value)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
            var result = new StringBuilder(hash.Length * 2);
            foreach (byte item in hash)
                result.Append(item.ToString("x2"));
            return result.ToString();
        }
    }

    private static string Base64Url(byte[] value)
    {
        return Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern int HabimonKeychainSet(string service, string account, string value);
    [DllImport("__Internal")] private static extern int HabimonKeychainGet(string service, string account, out IntPtr value);
    [DllImport("__Internal")] private static extern void HabimonKeychainFree(IntPtr value);
#endif
}

#if UNITY_EDITOR
/// <summary>Editor-only local credentials. This type is excluded from Players.</summary>
internal static class ChatbotEditorCredentialStore
{
    private static readonly System.Collections.Generic.Dictionary<string, string> Values =
        new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);

    public static string Get(string service, string account)
    {
        Values.TryGetValue(service + ":" + account, out string value);
        return value;
    }

    public static void Set(string service, string account, string value)
    {
        Values[service + ":" + account] = value;
    }
}
#endif
