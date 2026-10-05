namespace BlazorFluent.Core.Security;

/// <summary>
/// Provides client-side stream encryption and decryption using authenticated AES-256-GCM.
/// Derives a cryptographically isolated 256-bit key per tenant via HKDF-SHA256 from a master key.
/// Ensures zero-knowledge confidentiality for file payloads stored in Azure Blob Storage.
/// </summary>
public interface ITenantEncryptionService
{
    /// <summary>
    /// Encrypts plaintext data stream with a tenant-derived AES-256-GCM key.
    /// Emits a stream containing [12-byte Nonce][16-byte AuthTag][Ciphertext].
    /// </summary>
    Task<Stream> EncryptStreamAsync(Stream plaintextStream, string tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrypts ciphertext stream containing [12-byte Nonce][16-byte AuthTag][Ciphertext]
    /// using the tenant's derived AES-256-GCM key and verifies authenticity.
    /// </summary>
    Task<Stream> DecryptStreamAsync(Stream encryptedStream, string tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes the SHA-256 hex digest of a stream for tamper-detection and audit logging.
    /// </summary>
    Task<string> ComputeSha256ChecksumAsync(Stream stream, CancellationToken cancellationToken = default);
}
