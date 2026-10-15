using System.Security.Cryptography;
using System.Text;
using BlazorFluent.Shared.Common;
using BlazorFluent.Shared.Security;
using FluentAssertions;
using Xunit;

namespace BlazorFluent.Core.Tests.Security;

public class PayloadEncryptionUtilityTests
{
    private readonly byte[] _validKey = SHA256.HashData(Encoding.UTF8.GetBytes("Test-Secret-Key-32-Bytes-Length!"));

    [Fact]
    public void EncryptAndDecrypt_RoundTrip_PreservesPlaintext()
    {
        var originalText = "Sensitive financial and customer payload 12345";
        var plaintextBytes = Encoding.UTF8.GetBytes(originalText);

        var envelope = PayloadEncryptionUtility.Encrypt(plaintextBytes, _validKey);

        envelope.Should().NotBeNull();
        envelope.NonceBase64.Should().NotBeNullOrWhiteSpace();
        envelope.TagBase64.Should().NotBeNullOrWhiteSpace();
        envelope.CiphertextBase64.Should().NotBeNullOrWhiteSpace();

        var decryptedBytes = PayloadEncryptionUtility.Decrypt(envelope, _validKey);
        var decryptedText = Encoding.UTF8.GetString(decryptedBytes);

        decryptedText.Should().Be(originalText);
    }

    [Fact]
    public void Decrypt_WithTamperedCiphertext_ThrowsCryptographicException()
    {
        var plaintextBytes = Encoding.UTF8.GetBytes("Data to protect");
        var envelope = PayloadEncryptionUtility.Encrypt(plaintextBytes, _validKey);

        var rawCiphertext = Convert.FromBase64String(envelope.CiphertextBase64);
        rawCiphertext[0] ^= 0xFF; // Tamper single byte

        var tamperedEnvelope = envelope with { CiphertextBase64 = Convert.ToBase64String(rawCiphertext) };

        var act = () => PayloadEncryptionUtility.Decrypt(tamperedEnvelope, _validKey);
        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Decrypt_WithTamperedTag_ThrowsCryptographicException()
    {
        var plaintextBytes = Encoding.UTF8.GetBytes("Data to protect");
        var envelope = PayloadEncryptionUtility.Encrypt(plaintextBytes, _validKey);

        var rawTag = Convert.FromBase64String(envelope.TagBase64);
        rawTag[0] ^= 0xFF; // Tamper auth tag

        var tamperedEnvelope = envelope with { TagBase64 = Convert.ToBase64String(rawTag) };

        var act = () => PayloadEncryptionUtility.Decrypt(tamperedEnvelope, _validKey);
        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void DeriveTenantKey_ReturnsIsolatedKeysPerTenant()
    {
        var licenseKey = "MY-COMMERCIAL-LICENSE-KEY";

        var keyTenantA = PayloadEncryptionUtility.DeriveTenantKey(licenseKey, "Tenant-Alpha");
        var keyTenantB = PayloadEncryptionUtility.DeriveTenantKey(licenseKey, "Tenant-Beta");
        var keyTenantARepeat = PayloadEncryptionUtility.DeriveTenantKey(licenseKey, "Tenant-Alpha");

        keyTenantA.Length.Should().Be(PayloadEncryptionUtility.KeyByteLength);
        keyTenantB.Length.Should().Be(PayloadEncryptionUtility.KeyByteLength);

        keyTenantA.Should().NotEqual(keyTenantB);
        keyTenantA.Should().Equal(keyTenantARepeat);
    }

    private record TestPayload(int Id, string Name, DateTime CreatedAt);

    [Fact]
    public void EncryptPayload_And_DecryptPayload_PreservesTypedObject()
    {
        var payload = new TestPayload(101, "RollupCalculation", new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc));

        var envelope = PayloadEncryptionUtility.EncryptPayload(payload, _validKey);
        var decrypted = PayloadEncryptionUtility.DecryptPayload<TestPayload>(envelope, _validKey);

        decrypted.Should().NotBeNull();
        decrypted!.Id.Should().Be(payload.Id);
        decrypted.Name.Should().Be(payload.Name);
        decrypted.CreatedAt.Should().Be(payload.CreatedAt);
    }
}
