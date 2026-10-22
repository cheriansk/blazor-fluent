using System.Security.Cryptography;
using System.Text;

namespace BlazorFluent.Core.Security;

/// <summary>
/// Cryptographic utility generating high-entropy license keys and non-correlatable opaque identifiers.
/// Adheres to GDPR Data Minimization and OWASP ASVS 4.3 Indirect Reference standard to prevent IDOR and enumeration.
/// </summary>
public static class OpaqueIdUtility
{
    /// <summary>
    /// Generates a cryptographically secure 256-bit (32-byte) Base64 license key using RandomNumberGenerator.
    /// </summary>
    public static string GenerateSecureLicenseKey()
    {
        var keyBytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(keyBytes);
    }

    /// <summary>
    /// Generates a non-descriptive, opaque external reference ID for a tenant.
    /// Avoids exposing internal database PKs or company names on the wire.
    /// </summary>
    public static string GenerateTenantRefId()
    {
        return $"tnt_{RandomNumberGenerator.GetHexString(12).ToLowerInvariant()}";
    }

    /// <summary>
    /// Computes a one-way salted opaque reference handle for a user GUID or entity ID.
    /// External observers inspecting tokens see only an opaque hash string that matches no database table directly.
    /// </summary>
    public static string MaskGuid(Guid id, string saltOrKey)
    {
        var keyBytes = Encoding.UTF8.GetBytes(saltOrKey);
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(id.ToByteArray());
        return "ref_" + Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
}
