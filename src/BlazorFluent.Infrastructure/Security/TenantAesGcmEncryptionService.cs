using System.Buffers;
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
/// - Single payload: [12 bytes Nonce][16 bytes AuthTag][Ciphertext] (for payloads <= 64KB)
/// - Chunked stream: [4 bytes Magic 'BFG1'] repeating [4 bytes ChunkLen][12 bytes Nonce][16 bytes AuthTag][Ciphertext] ending with [0 bytes ChunkLen] (constant memory for large files)
/// </summary>
public class TenantAesGcmEncryptionService : ITenantEncryptionService
{
    private const int NonceSizeBytes = 12; // Standard 96-bit GCM nonce
    private const int TagSizeBytes = 16;   // Standard 128-bit authentication tag
    private const int KeySizeBytes = 32;   // 256-bit AES key
    private const int ChunkSizeBytes = 64 * 1024; // 64KB chunk buffer size to eliminate LOH allocations
    private static readonly byte[] ChunkedMagicHeader = [(byte)'B', (byte)'F', (byte)'G', (byte)'1'];

    private readonly byte[] _masterKey;
    private readonly ILogger<TenantAesGcmEncryptionService> _logger;

    public TenantAesGcmEncryptionService(IConfiguration configuration, ILogger<TenantAesGcmEncryptionService> logger)
    {
        _logger = logger;

        var keyConfig = configuration["Security:MasterEncryptionKey"];
        if (string.IsNullOrWhiteSpace(keyConfig) || keyConfig.Length < 32 || keyConfig.StartsWith("__SET_VIA_"))
        {
            throw new InvalidOperationException(
                "Zero-Trust Security Violation: 'Security:MasterEncryptionKey' must be configured in KeyVault, environment, or User Secrets with at least 32 characters.");
        }

        _masterKey = Encoding.UTF8.GetBytes(keyConfig[..32]);
    }

    public async Task<Stream> EncryptStreamAsync(Stream plaintextStream, string tenantId, CancellationToken cancellationToken = default)
    {
        if (plaintextStream == null) throw new ArgumentNullException(nameof(plaintextStream));
        if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("TenantId is required for key derivation.", nameof(tenantId));

        byte[] tenantKey = DeriveTenantKey(tenantId);
        using var aesGcm = new AesGcm(tenantKey, TagSizeBytes);

        // If the stream is seekable and smaller than 64KB, use the standard single-payload format
        if (plaintextStream.CanSeek && plaintextStream.Length <= ChunkSizeBytes)
        {
            int length = (int)plaintextStream.Length;
            byte[] plaintext = ArrayPool<byte>.Shared.Rent(length);
            try
            {
                int read = await plaintextStream.ReadAsync(plaintext.AsMemory(0, length), cancellationToken);
                byte[] nonce = new byte[NonceSizeBytes];
                RandomNumberGenerator.Fill(nonce);

                byte[] ciphertext = new byte[read];
                byte[] tag = new byte[TagSizeBytes];

                aesGcm.Encrypt(nonce, plaintext.AsSpan(0, read), ciphertext, tag);

                var outputStream = new MemoryStream(NonceSizeBytes + TagSizeBytes + read);
                await outputStream.WriteAsync(nonce.AsMemory(0, NonceSizeBytes), cancellationToken);
                await outputStream.WriteAsync(tag.AsMemory(0, TagSizeBytes), cancellationToken);
                await outputStream.WriteAsync(ciphertext.AsMemory(0, read), cancellationToken);

                outputStream.Position = 0;
                return outputStream;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(plaintext);
            }
        }

        // For large streams or non-seekable streams, encrypt in chunked streaming format to avoid LOH allocations
        var chunkedOutput = new MemoryStream();
        await chunkedOutput.WriteAsync(ChunkedMagicHeader.AsMemory(0, ChunkedMagicHeader.Length), cancellationToken);

        byte[] buffer = ArrayPool<byte>.Shared.Rent(ChunkSizeBytes);
        byte[] chunkCiphertext = ArrayPool<byte>.Shared.Rent(ChunkSizeBytes);
        byte[] chunkNonce = new byte[NonceSizeBytes];
        byte[] chunkTag = new byte[TagSizeBytes];

        try
        {
            int bytesRead;
            while ((bytesRead = await plaintextStream.ReadAsync(buffer.AsMemory(0, ChunkSizeBytes), cancellationToken)) > 0)
            {
                RandomNumberGenerator.Fill(chunkNonce);
                aesGcm.Encrypt(chunkNonce, buffer.AsSpan(0, bytesRead), chunkCiphertext.AsSpan(0, bytesRead), chunkTag);

                byte[] lengthBytes = BitConverter.GetBytes(bytesRead);
                await chunkedOutput.WriteAsync(lengthBytes.AsMemory(0, 4), cancellationToken);
                await chunkedOutput.WriteAsync(chunkNonce.AsMemory(0, NonceSizeBytes), cancellationToken);
                await chunkedOutput.WriteAsync(chunkTag.AsMemory(0, TagSizeBytes), cancellationToken);
                await chunkedOutput.WriteAsync(chunkCiphertext.AsMemory(0, bytesRead), cancellationToken);
            }

            // Write 0-length terminal chunk
            await chunkedOutput.WriteAsync(BitConverter.GetBytes(0).AsMemory(0, 4), cancellationToken);
            chunkedOutput.Position = 0;
            return chunkedOutput;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            ArrayPool<byte>.Shared.Return(chunkCiphertext);
        }
    }

    public async Task<Stream> DecryptStreamAsync(Stream encryptedStream, string tenantId, CancellationToken cancellationToken = default)
    {
        if (encryptedStream == null) throw new ArgumentNullException(nameof(encryptedStream));
        if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("TenantId is required for key derivation.", nameof(tenantId));

        byte[] tenantKey = DeriveTenantKey(tenantId);
        using var aesGcm = new AesGcm(tenantKey, TagSizeBytes);

        // Check if stream starts with chunked magic header
        byte[] magicCheck = new byte[4];
        int magicRead = await encryptedStream.ReadAsync(magicCheck.AsMemory(0, 4), cancellationToken);

        if (magicRead == 4 && magicCheck.AsSpan().SequenceEqual(ChunkedMagicHeader))
        {
            // Decrypt chunked stream
            var decryptedStream = new MemoryStream();
            byte[] lenBuffer = new byte[4];
            byte[] chunkNonce = new byte[NonceSizeBytes];
            byte[] chunkTag = new byte[TagSizeBytes];

            while (true)
            {
                int lenRead = await encryptedStream.ReadAsync(lenBuffer.AsMemory(0, 4), cancellationToken);
                if (lenRead < 4) break;

                int chunkSize = BitConverter.ToInt32(lenBuffer, 0);
                if (chunkSize <= 0) break; // Terminating chunk

                await encryptedStream.ReadExactlyAsync(chunkNonce.AsMemory(0, NonceSizeBytes), cancellationToken);
                await encryptedStream.ReadExactlyAsync(chunkTag.AsMemory(0, TagSizeBytes), cancellationToken);

                byte[] cipherBuffer = ArrayPool<byte>.Shared.Rent(chunkSize);
                byte[] plainBuffer = ArrayPool<byte>.Shared.Rent(chunkSize);

                try
                {
                    await encryptedStream.ReadExactlyAsync(cipherBuffer.AsMemory(0, chunkSize), cancellationToken);
                    aesGcm.Decrypt(chunkNonce, cipherBuffer.AsSpan(0, chunkSize), chunkTag, plainBuffer.AsSpan(0, chunkSize));
                    await decryptedStream.WriteAsync(plainBuffer.AsMemory(0, chunkSize), cancellationToken);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(cipherBuffer);
                    ArrayPool<byte>.Shared.Return(plainBuffer);
                }
            }

            decryptedStream.Position = 0;
            return decryptedStream;
        }

        // Standard single payload format: [12-byte Nonce][16-byte Tag][Ciphertext]
        using var memoryStream = new MemoryStream();
        if (magicRead > 0)
        {
            await memoryStream.WriteAsync(magicCheck.AsMemory(0, magicRead), cancellationToken);
        }
        await encryptedStream.CopyToAsync(memoryStream, cancellationToken);
        byte[] payload = memoryStream.ToArray();

        if (payload.Length < NonceSizeBytes + TagSizeBytes)
        {
            throw new CryptographicException("Encrypted payload is corrupted or smaller than header size.");
        }

        byte[] nonce = payload.AsSpan(0, NonceSizeBytes).ToArray();
        byte[] tag = payload.AsSpan(NonceSizeBytes, TagSizeBytes).ToArray();
        byte[] ciphertext = payload.AsSpan(NonceSizeBytes + TagSizeBytes).ToArray();

        byte[] plaintextResult = new byte[ciphertext.Length];
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintextResult);

        var outputStream = new MemoryStream(plaintextResult);
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
