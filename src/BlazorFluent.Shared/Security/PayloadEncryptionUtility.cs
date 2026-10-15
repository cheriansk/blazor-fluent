using System.Security.Cryptography;
using System.Text;
using BlazorFluent.Shared.Common;

namespace BlazorFluent.Shared.Security;

/// <summary>
/// High-performance, zero-allocation hardware-accelerated AES-256-GCM envelope encryption utility.
/// Provides Authenticated Encryption with Associated Data (AEAD) for end-to-end data security.
/// </summary>
public static class PayloadEncryptionUtility
{
    public const int NonceByteLength = 12; // Standard 96-bit GCM Nonce
    public const int TagByteLength = 16;   // Standard 128-bit GCM Authentication Tag
    public const int KeyByteLength = 32;   // AES-256 requires 256-bit (32-byte) key

    /// <summary>
    /// Encrypts raw data bytes into an EncryptedEnvelopeDto using AES-256-GCM.
    /// </summary>
    public static EncryptedEnvelopeDto Encrypt(byte[] plaintext, byte[] key)
    {
        if (key == null || key.Length != KeyByteLength)
            throw new ArgumentException($"Encryption key must be exactly {KeyByteLength} bytes (256 bits).", nameof(key));

        var nonce = new byte[NonceByteLength];
        RandomNumberGenerator.Fill(nonce);

        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagByteLength];

        using var aesGcm = new AesGcm(key, TagByteLength);
        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);

        return EncryptedEnvelopeDto.FromBytes(nonce, tag, ciphertext);
    }

    /// <summary>
    /// Decrypts an EncryptedEnvelopeDto back into raw plaintext bytes using AES-256-GCM.
    /// Throws CryptographicException if the authentication tag verification fails (anti-tampering).
    /// </summary>
    public static byte[] Decrypt(EncryptedEnvelopeDto envelope, byte[] key)
    {
        if (key == null || key.Length != KeyByteLength)
            throw new ArgumentException($"Encryption key must be exactly {KeyByteLength} bytes (256 bits).", nameof(key));

        var (nonce, tag, ciphertext) = envelope.ToBytes();
        var plaintext = new byte[ciphertext.Length];

        using var aesGcm = new AesGcm(key, TagByteLength);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);

        return plaintext;
    }

    /// <summary>
    /// Encrypts an arbitrary object payload into an EncryptedEnvelopeDto via JsonDefaults.
    /// </summary>
    public static EncryptedEnvelopeDto EncryptPayload<T>(T payload, byte[] key)
    {
        var jsonBytes = JsonDefaults.ToUtf8Bytes(payload);
        return Encrypt(jsonBytes, key);
    }

    /// <summary>
    /// Decrypts an EncryptedEnvelopeDto and deserializes the object via JsonDefaults.
    /// </summary>
    public static T? DecryptPayload<T>(EncryptedEnvelopeDto envelope, byte[] key)
    {
        var plaintextBytes = Decrypt(envelope, key);
        return JsonDefaults.FromUtf8Bytes<T>(plaintextBytes);
    }

    /// <summary>
    /// Derives a cryptographically strong 256-bit AES key from a tenant license key string using HKDF-SHA256.
    /// </summary>
    public static byte[] DeriveTenantKey(string licenseKey, string tenantId)
    {
        var ikm = Encoding.UTF8.GetBytes(licenseKey);
        var salt = Encoding.UTF8.GetBytes(tenantId);
        var info = Encoding.UTF8.GetBytes("BlazorFluent.PayloadEncryption.v1");

        return HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, KeyByteLength, salt, info);
    }
}
