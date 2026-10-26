package com.gsam.habimon.chatbot;

import android.content.Context;
import android.content.SharedPreferences;
import android.app.Activity;
import android.graphics.Rect;
import android.os.Build;
import android.security.keystore.KeyGenParameterSpec;
import android.security.keystore.KeyProperties;
import android.util.Base64;
import android.view.View;
import android.view.WindowInsets;
import android.view.WindowManager;

import java.nio.charset.StandardCharsets;
import java.security.KeyStore;
import java.security.MessageDigest;
import java.util.Arrays;
import javax.crypto.Cipher;
import javax.crypto.KeyGenerator;
import javax.crypto.SecretKey;
import javax.crypto.spec.GCMParameterSpec;

/** AES/GCM protected blob storage with a per-UID key backed by Android Keystore. */
public final class ChatbotSecureStore {
    private static final String PREFS = "habimon_chat_owner_tokens_v1";
    private static final String KEY_PREFIX = "habimon_chat_owner_";
    private static final String KEYSTORE = "AndroidKeyStore";
    private static final int IV_BYTES = 12;
    private static final int GCM_TAG_BITS = 128;

    private ChatbotSecureStore() {}

    /** Requests a resizable Unity window and exposes the actual Android IME overlap in pixels. */
    public static void configureForIme(Activity activity) {
        if (activity == null) return;
        activity.getWindow().setSoftInputMode(WindowManager.LayoutParams.SOFT_INPUT_ADJUST_RESIZE);
    }

    public static int getImeBottomInset(Activity activity) {
        if (activity == null) return 0;
        View decor = activity.getWindow().getDecorView();
        if (Build.VERSION.SDK_INT >= 30) {
            WindowInsets insets = decor.getRootWindowInsets();
            if (insets == null) return 0;
            return insets.getInsets(WindowInsets.Type.ime()).bottom;
        }

        Rect visibleFrame = new Rect();
        decor.getWindowVisibleDisplayFrame(visibleFrame);
        return Math.max(0, decor.getRootView().getHeight() - visibleFrame.bottom);
    }

    public static synchronized String get(Context context, String account) {
        try {
            String keyId = safeId(account);
            String encoded = context.getApplicationContext().getSharedPreferences(PREFS, Context.MODE_PRIVATE)
                    .getString(keyId, null);
            if (encoded == null) return null;
            byte[] packed = Base64.decode(encoded, Base64.NO_WRAP);
            if (packed.length <= IV_BYTES) return null;
            byte[] iv = Arrays.copyOfRange(packed, 0, IV_BYTES);
            byte[] ciphertext = Arrays.copyOfRange(packed, IV_BYTES, packed.length);
            Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
            cipher.init(Cipher.DECRYPT_MODE, secretKey(KEY_PREFIX + keyId), new GCMParameterSpec(GCM_TAG_BITS, iv));
            return new String(cipher.doFinal(ciphertext), StandardCharsets.UTF_8);
        } catch (Exception exception) {
            throw new IllegalStateException("Unable to read the encrypted chatbot credential", exception);
        }
    }

    public static synchronized boolean put(Context context, String account, String value) {
        try {
            String keyId = safeId(account);
            Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
            cipher.init(Cipher.ENCRYPT_MODE, secretKey(KEY_PREFIX + keyId));
            byte[] iv = cipher.getIV();
            byte[] encrypted = cipher.doFinal(value.getBytes(StandardCharsets.UTF_8));
            byte[] packed = new byte[iv.length + encrypted.length];
            System.arraycopy(iv, 0, packed, 0, iv.length);
            System.arraycopy(encrypted, 0, packed, iv.length, encrypted.length);
            boolean saved = context.getApplicationContext().getSharedPreferences(PREFS, Context.MODE_PRIVATE)
                    .edit().putString(keyId, Base64.encodeToString(packed, Base64.NO_WRAP)).commit();
            Arrays.fill(packed, (byte) 0);
            Arrays.fill(encrypted, (byte) 0);
            return saved;
        } catch (Exception exception) {
            throw new IllegalStateException("Unable to store the encrypted chatbot credential", exception);
        }
    }

    public static synchronized boolean remove(Context context, String account) {
        try {
            String keyId = safeId(account);
            boolean saved = context.getApplicationContext().getSharedPreferences(PREFS, Context.MODE_PRIVATE)
                    .edit().remove(keyId).commit();
            KeyStore keyStore = KeyStore.getInstance(KEYSTORE);
            keyStore.load(null);
            if (keyStore.containsAlias(KEY_PREFIX + keyId)) keyStore.deleteEntry(KEY_PREFIX + keyId);
            return saved;
        } catch (Exception exception) {
            throw new IllegalStateException("Unable to remove the encrypted chatbot credential", exception);
        }
    }

    private static SecretKey secretKey(String alias) throws Exception {
        KeyStore keyStore = KeyStore.getInstance(KEYSTORE);
        keyStore.load(null);
        if (keyStore.containsAlias(alias)) return ((KeyStore.SecretKeyEntry) keyStore.getEntry(alias, null)).getSecretKey();
        KeyGenerator generator = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, KEYSTORE);
        generator.init(new KeyGenParameterSpec.Builder(alias, KeyProperties.PURPOSE_ENCRYPT | KeyProperties.PURPOSE_DECRYPT)
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                .setRandomizedEncryptionRequired(true)
                .build());
        return generator.generateKey();
    }

    private static String safeId(String account) throws Exception {
        if (account == null || account.isEmpty()) throw new IllegalArgumentException("Account namespace is required");
        byte[] digest = MessageDigest.getInstance("SHA-256").digest(account.getBytes(StandardCharsets.UTF_8));
        StringBuilder builder = new StringBuilder(digest.length * 2);
        for (byte item : digest) builder.append(String.format(java.util.Locale.ROOT, "%02x", item));
        Arrays.fill(digest, (byte) 0);
        return builder.toString();
    }
}
