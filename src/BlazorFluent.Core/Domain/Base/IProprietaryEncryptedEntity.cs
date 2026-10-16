namespace BlazorFluent.Core.Domain.Base;

/// <summary>
/// Contract for domain entities that support traceability back to a canonical staged row (SourceRowId)
/// and optional API-keyed AES-256-GCM at-rest byte encryption (EncryptedPayloadBytes).
/// </summary>
public interface IProprietaryEncryptedEntity
{
    /// <summary>
    /// Deterministic UUID v7 linking this domain entity back to the exact row inside staging.StagedImports.PayloadJson.
    /// </summary>
    Guid? SourceRowId { get; set; }

    /// <summary>
    /// Indicates whether sensitive business fields on this entity are stored encrypted inside EncryptedPayloadBytes.
    /// </summary>
    bool IsEncryptedAtRest { get; set; }

    /// <summary>
    /// Packed AES-256-GCM ciphertext ([12-byte Nonce | 16-byte Tag | Ciphertext]) encrypted with the Cloud API's VaultMasterKey.
    /// Decrypted on-demand in memory via POST /api/v1/transforms/decrypt-proprietary when displayed on screen.
    /// </summary>
    byte[]? EncryptedPayloadBytes { get; set; }
}
