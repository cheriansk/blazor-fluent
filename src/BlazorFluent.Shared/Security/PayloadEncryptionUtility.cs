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

    /// <summary>
    /// Derives a tenant-isolated 256-bit AES key from the Cloud API's exclusive Vault Master Key for at-rest byte[] encryption.
    /// </summary>
    public static byte[] DeriveVaultAtRestKey(string vaultMasterKey, string tenantId)
    {
        var ikm = Encoding.UTF8.GetBytes(vaultMasterKey);
        var salt = Encoding.UTF8.GetBytes(tenantId);
        var info = Encoding.UTF8.GetBytes("BlazorFluent.ProprietaryAtRestVault.v1");

        return HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, KeyByteLength, salt, info);
    }

    /// <summary>
    /// Encrypts an object into a single packed byte[] array: [12-byte Nonce | 16-byte Tag | Ciphertext].
    /// Designed for storing proprietary encrypted payloads directly in PostgreSQL bytea columns.
    /// </summary>
    public static byte[] EncryptToPackedBytes<T>(T payload, byte[] vaultKey)
    {
        if (vaultKey == null || vaultKey.Length != KeyByteLength)
            throw new ArgumentException($"Vault key must be {KeyByteLength} bytes.", nameof(vaultKey));

        var plaintext = JsonDefaults.ToUtf8Bytes(payload);
        var nonce = new byte[NonceByteLength];
        RandomNumberGenerator.Fill(nonce);

        var tag = new byte[TagByteLength];
        var ciphertext = new byte[plaintext.Length];

        using var aesGcm = new AesGcm(vaultKey, TagByteLength);
        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);

        var packed = new byte[NonceByteLength + TagByteLength + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, packed, 0, NonceByteLength);
        Buffer.BlockCopy(tag, 0, packed, NonceByteLength, TagByteLength);
        Buffer.BlockCopy(ciphertext, 0, packed, NonceByteLength + TagByteLength, ciphertext.Length);

        return packed;
    }

    /// <summary>
    /// Decrypts a packed byte[] array ([12-byte Nonce | 16-byte Tag | Ciphertext]) back into the original object.
    /// </summary>
    public static T? DecryptFromPackedBytes<T>(byte[] packedBytes, byte[] vaultKey)
    {
        if (vaultKey == null || vaultKey.Length != KeyByteLength)
            throw new ArgumentException($"Vault key must be {KeyByteLength} bytes.", nameof(vaultKey));

        if (packedBytes == null || packedBytes.Length < NonceByteLength + TagByteLength)
            throw new CryptographicException("Packed encrypted payload is truncated or malformed.");

        var nonce = new byte[NonceByteLength];
        var tag = new byte[TagByteLength];
        var ciphertextLength = packedBytes.Length - NonceByteLength - TagByteLength;
        var ciphertext = new byte[ciphertextLength];
        var plaintext = new byte[ciphertextLength];

        Buffer.BlockCopy(packedBytes, 0, nonce, 0, NonceByteLength);
        Buffer.BlockCopy(packedBytes, NonceByteLength, tag, 0, TagByteLength);
        Buffer.BlockCopy(packedBytes, NonceByteLength + TagByteLength, ciphertext, 0, ciphertextLength);

        using var aesGcm = new AesGcm(vaultKey, TagByteLength);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);

        return JsonDefaults.FromUtf8Bytes<T>(plaintext);
    }

    /// <summary>
    /// Compresses a UTF-8 JSON string using GZip for compact network transmission.
    /// </summary>
    public static byte[] CompressGzip(string json)
    {
        var inputBytes = Encoding.UTF8.GetBytes(json);
        using var output = new MemoryStream();
        using (var gzip = new System.IO.Compression.GZipStream(output, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(inputBytes, 0, inputBytes.Length);
        }
        return output.ToArray();
    }

    /// <summary>
    /// Decompresses a GZip byte array back into a UTF-8 JSON string.
    /// </summary>
    public static string DecompressGzip(byte[] compressedBytes)
    {
        using var input = new MemoryStream(compressedBytes);
        using var gzip = new System.IO.Compression.GZipStream(input, System.IO.Compression.CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
