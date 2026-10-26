using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using BlazorEnterprise.Core.Security;
using BlazorEnterprise.Infrastructure.Security;
using BlazorEnterprise.Shared.Common;
using BlazorEnterprise.Shared.Contracts.Engine.Client;
using BlazorEnterprise.Shared.Security;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BlazorEnterprise.Core.Tests.Security;

public class ZeroTrustTenancyIsolationTests
{
    [Fact]
    public void GenerateSecureLicenseKey_ReturnsCryptographicallySecure256BitBase64Key()
    {
        var key1 = OpaqueIdUtility.GenerateSecureLicenseKey();
        var key2 = OpaqueIdUtility.GenerateSecureLicenseKey();

        key1.Should().NotBeNullOrWhiteSpace();
        key2.Should().NotBeNullOrWhiteSpace();
        key1.Should().NotBe(key2);

        var bytes1 = Convert.FromBase64String(key1);
        var bytes2 = Convert.FromBase64String(key2);

        bytes1.Length.Should().Be(32, "256 bits is exactly 32 bytes");
        bytes2.Length.Should().Be(32, "256 bits is exactly 32 bytes");
    }

    [Fact]
    public void GenerateTenantRefId_ReturnsValidSlugWithPrefix()
    {
        var tid1 = OpaqueIdUtility.GenerateTenantRefId();
        var tid2 = OpaqueIdUtility.GenerateTenantRefId();

        tid1.Should().StartWith("tnt_");
        tid2.Should().StartWith("tnt_");
        tid1.Should().NotBe(tid2, "Random suffix guarantees uniqueness");
    }

    [Fact]
    public void MaskGuid_DeterministicAndSaltSensitive()
    {
        var entityId = Guid.NewGuid();
        var secretA = "SecretSalt-TenantA-2026";
        var secretB = "SecretSalt-TenantB-2026";

        var opaqueA1 = OpaqueIdUtility.MaskGuid(entityId, secretA);
        var opaqueA2 = OpaqueIdUtility.MaskGuid(entityId, secretA);
        var opaqueB = OpaqueIdUtility.MaskGuid(entityId, secretB);

        opaqueA1.Should().Be(opaqueA2, "Identical GUID and salt must produce deterministic opaque reference");
        opaqueA1.Should().NotBe(opaqueB, "Different salts must produce distinct opaque references to prevent correlation across tenants");
        opaqueA1.Should().StartWith("ref_");
    }

    [Fact]
    public void TenantTokenService_IssuesEphemeralS2SToken_WithMaskedOpaqueClaims()
    {
        var tenantId = "tnt_omega_9876";
        var correlationId = "corr-" + Guid.NewGuid();
        var licenseKey = OpaqueIdUtility.GenerateSecureLicenseKey();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"Tenants:{tenantId}:LicenseKey"] = licenseKey
            })
            .Build();

        var correlationContext = new CorrelationContext { CorrelationId = correlationId };
        var tokenService = new TenantTokenService(config, correlationContext);
        var token = tokenService.GenerateS2SToken(tenantId, "BatchWorker");

        token.Should().NotBeNullOrWhiteSpace();

        // Parse token and verify payload claims
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Issuer.Should().Be("blazorenterprise-worker");
        jwt.Audiences.Should().Contain("https://api.blazorenterprise.com");

        // Verify opaque claims
        jwt.Claims.Should().Contain(c => c.Type == "tid" && c.Value == tenantId);
        jwt.Claims.Should().Contain(c => c.Type == "tenant_id" && c.Value == tenantId);
        jwt.Claims.Should().Contain(c => c.Type == "cid" && c.Value == correlationId);
        jwt.Claims.Should().Contain(c => c.Type == "act" && c.Value == "BatchWorker");

        // Verify zero PII leakage
        jwt.Claims.Should().NotContain(c => c.Type == "email");
        jwt.Claims.Should().NotContain(c => c.Type == "name");
        jwt.Claims.Should().NotContain(c => c.Type == "unique_name");

        // Verify ephemeral 5-minute lifespan
        var lifespan = jwt.ValidTo - jwt.ValidFrom;
        lifespan.TotalMinutes.Should().BeApproximately(5, 0.1);
    }

    [Fact]
    public void TenantTokenService_ThrowsInvalidOperationException_WhenTenantKeyMissing()
    {
        var config = new ConfigurationBuilder().Build(); // No tenant keys configured
        var correlationContext = new CorrelationContext { CorrelationId = "corr-1" };
        var tokenService = new TenantTokenService(config, correlationContext);

        var act = () => tokenService.GenerateS2SToken("unconfigured_tenant", "BatchWorker");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Zero-Trust Security Violation*");
    }

    [Fact]
    public void CrossTenantIsolation_PayloadEncryption_DifferentTenantsCannotDecrypt()
    {
        var tenantA = "tnt_alpha";
        var tenantB = "tnt_beta";

        var licenseA = OpaqueIdUtility.GenerateSecureLicenseKey();
        var licenseB = OpaqueIdUtility.GenerateSecureLicenseKey();

        var keyA = PayloadEncryptionUtility.DeriveTenantKey(licenseA, tenantA);
        var keyB = PayloadEncryptionUtility.DeriveTenantKey(licenseB, tenantB);

        var secretPayload = Encoding.UTF8.GetBytes("Tenant A proprietary confidential data");
        var envelopeA = PayloadEncryptionUtility.Encrypt(secretPayload, keyA);

        // Decrypting with Tenant A's key succeeds
        var decryptedBytes = PayloadEncryptionUtility.Decrypt(envelopeA, keyA);
        Encoding.UTF8.GetString(decryptedBytes).Should().Be("Tenant A proprietary confidential data");

        // Decrypting with Tenant B's key fails cryptographically
        var act = () => PayloadEncryptionUtility.Decrypt(envelopeA, keyB);
        act.Should().Throw<CryptographicException>();
    }
}
