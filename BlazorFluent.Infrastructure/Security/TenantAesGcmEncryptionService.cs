using System.Security.Cryptography;
using System.Text;
using BlazorFluent.Core.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Infrastructure.Security;

/// <summary>
/// Authenticated client-side stream encryption engine using native hardware-accelerated AES-256-GCM.
/// Derives a cryptographically isolated 256-bit key per tenant via HKDF-SHA256 from a master key.
/// Produces self-contained encrypted payloads formatted as:
/// [12 bytes Nonce][16 bytes AuthTag][Ciphertext]
/// </summary>
public class TenantAesGcmEncryptionService : ITenantEncryptionService
{
    private const int NonceSizeBytes = 12; // Standard 96-bit GCM nonce
    private const int TagSizeBytes = 16;   // Standard 128-bit authentication tag
    private const int KeySizeBytes = 32;   // 256-bit AES key

    private readonly byte[] _masterKey;
    private readonly ILogger<TenantAesGcmEncryptionService> _logger;

    public TenantAesGcmEncryptionService(IConfiguration configuration, ILogger<TenantAesGcmEncryptionService> logger)
    {
        _logger = logger;

        var keyConfig = configuration["Security:MasterEncryptionKey"];
        if (!string.IsNullOrWhiteSpace(keyConfig) && keyConfig.Length >= 32)
        {
            _masterKey = Encoding.UTF8.GetBytes(keyConfig[..32]);
        }
        else
        {
            // Deterministic default development master key (32 bytes)
            _masterKey = SHA256.HashData(Encoding.UTF8.GetBytes("BlazorFluent-Dev-Master-Encryption-Secret-Key-2026"));
        }
    }

    public async Task<Stream> EncryptStreamAsync(Stream plaintextStream, string tenantId, CancellationToken cancellationToken = default)
    {
        if (plaintextStream == null) throw new ArgumentNullException(nameof(plaintextStream));
        if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("TenantId is required for key derivation.", nameof(tenantId));

        byte[] tenantKey = DeriveTenantKey(tenantId);

        // Read all plaintext bytes into memory
        using var memoryStream = new MemoryStream();
        await plaintextStream.CopyToAsync(memoryStream, cancellationToken);
        byte[] plaintext = memoryStream.ToArray();

        byte[] nonce = new byte[NonceSizeBytes];
        RandomNumberGenerator.Fill(nonce);

        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[TagSizeBytes];

        using (var aesGcm = new AesGcm(tenantKey, TagSizeBytes))
        {
            aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);
        }

        // Pack format: [12-byte Nonce][16-byte AuthTag][Ciphertext]
        var outputStream = new MemoryStream(NonceSizeBytes + TagSizeBytes + ciphertext.Length);
        await outputStream.WriteAsync(nonce, 0, NonceSizeBytes, cancellationToken);
        await outputStream.WriteAsync(tag, 0, TagSizeBytes, cancellationToken);
        await outputStream.WriteAsync(ciphertext, 0, ciphertext.Length, cancellationToken);

        outputStream.Position = 0;
        return outputStream;
    }

    public async Task<Stream> DecryptStreamAsync(Stream encryptedStream, string tenantId, CancellationToken cancellationToken = default)
    {
        if (encryptedStream == null) throw new ArgumentNullException(nameof(encryptedStream));
        if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("TenantId is required for key derivation.", nameof(tenantId));

        byte[] tenantKey = DeriveTenantKey(tenantId);

        using var memoryStream = new MemoryStream();
        await encryptedStream.CopyToAsync(memoryStream, cancellationToken);
        byte[] payload = memoryStream.ToArray();

        if (payload.Length < NonceSizeBytes + TagSizeBytes)
        {
            throw new CryptographicException("Encrypted payload is corrupted or smaller than header size.");
        }

        byte[] nonce = payload.AsSpan(0, NonceSizeBytes).ToArray();
        byte[] tag = payload.AsSpan(NonceSizeBytes, TagSizeBytes).ToArray();
        byte[] ciphertext = payload.AsSpan(NonceSizeBytes + TagSizeBytes).ToArray();

        byte[] plaintext = new byte[ciphertext.Length];

        using (var aesGcm = new AesGcm(tenantKey, TagSizeBytes))
        {
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);
        }

        var outputStream = new MemoryStream(plaintext);
        return outputStream;
    }

    public async Task<string> ComputeSha256ChecksumAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));

        long originalPosition = stream.CanSeek ? stream.Position : 0;
        try
        {
            if (stream.CanSeek) stream.Position = 0;
            var hash = await SHA256.HashDataAsync(stream, cancellationToken);
            return Convert.ToHexStringLower(hash);
        }
        finally
        {
            if (stream.CanSeek) stream.Position = originalPosition;
        }
    }

    private byte[] DeriveTenantKey(string tenantId)
    {
        // HKDF-SHA256: Derives a 32-byte (256-bit) cryptographically isolated key per tenant
        byte[] info = Encoding.UTF8.GetBytes($"tenant:{tenantId}:file-encryption-v1");
        byte[] salt = SHA256.HashData(Encoding.UTF8.GetBytes($"salt:{tenantId}"));

        return HKDF.DeriveKey(HashAlgorithmName.SHA256, _masterKey, KeySizeBytes, salt, info);
    }
}
