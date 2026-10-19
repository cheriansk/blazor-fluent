namespace BlazorFluent.Core.Domain.Base;

/// <summary>
/// Contract for domain entities that store sensitive, confidential, or proprietary business payloads
/// with optional field-level at-rest encryption (<see cref="EncryptedPayloadBytes"/>).
/// <para>
/// <b>CRYPTOGRAPHIC SPECIFICATION:</b> Sensitive business fields are protected at rest using industry-standard,
/// NIST-approved <b>AES-256-GCM</b> authenticated encryption with 12-byte nonces and 16-byte auth tags.
/// </para>
/// <para>
/// <b>WHEN TO USE:</b>
/// <list type="bullet">
///   <item>Entities that contain high-value intellectual property, trade secrets, sensitive deliverables, or confidential notes.</item>
///   <item>When database administrators, DB backup storage, or compromised read-replicas must NOT be able to view cleartext business payloads (zero-knowledge at-rest storage).</item>
/// </list>
/// </para>
/// <para>
/// <b>WHEN NOT TO USE:</b>
/// <list type="bullet">
///   <item>Standard operational metadata or fields that need to be searched, filtered, indexed, or sorted in SQL (e.g. Status, DueDate, Title, TenantId).</item>
///   <item>High-frequency query entities that cannot tolerate the network roundtrip overhead of on-demand decryption via <c>/api/v1/transforms/decrypt-proprietary</c>.</item>
///   <item>Entities whose data is already non-sensitive, public, or adequately protected by standard PostgreSQL transparent disk encryption / TLS in transit.</item>
/// </list>
/// </para>
/// </summary>
public interface IEncryptedPayloadEntity
{

    /// <summary>
    /// Indicates whether sensitive business fields on this entity are stored encrypted inside <see cref="EncryptedPayloadBytes"/>.
    /// When <c>false</c>, fields are populated in standard cleartext database columns.
    /// </summary>
    bool IsEncryptedAtRest { get; set; }

    /// <summary>
    /// Packed AES-256-GCM ciphertext ([12-byte Nonce | 16-byte Tag | Ciphertext]) encrypted with the Vault/API key.
    /// Decrypted strictly on-demand in volatile memory via POST /api/v1/transforms/decrypt-proprietary when rendered on screen.
    /// </summary>
    byte[]? EncryptedPayloadBytes { get; set; }
}
