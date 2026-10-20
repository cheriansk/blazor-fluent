namespace BlazorFluent.Core.Domain.Base;

/// <summary>
/// Marks a domain entity as cryptographically sealed with an HMAC-SHA256 signature against direct database tampering.
/// Protects sensitive identity, tenant membership, and hierarchical RBAC role properties.
/// </summary>
public interface IHmacSignableEntity
{
    /// <summary>
    /// Base64-encoded HMAC-SHA256 signature computed over critical security and role properties.
    /// </summary>
    string? RowSignature { get; set; }

    /// <summary>
    /// Computes the expected HMAC-SHA256 signature for this entity given the application secret key.
    /// </summary>
    string ComputeIntegritySignature(string secretKey);

    /// <summary>
    /// Validates whether the row's stored signature matches the expected HMAC in constant time.
    /// </summary>
    bool VerifyIntegritySignature(string secretKey);
}
