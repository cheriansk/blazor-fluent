using System.Security.Cryptography;
using System.Text;
using BlazorFluent.Core.Contracts;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Infrastructure.Security;

/// <summary>
/// Cryptographic HMAC-SHA256 signature validator for incoming HTTP webhooks.
/// Protects system-to-system integration endpoints against payload tampering and timing attacks.
/// </summary>
public class HmacWebhookSignatureValidator : IWebhookSignatureValidator
{
    private readonly ILogger<HmacWebhookSignatureValidator> _logger;

    public HmacWebhookSignatureValidator(ILogger<HmacWebhookSignatureValidator> logger)
    {
        _logger = logger;
    }

    public bool ValidateHmacSha256(string payloadBody, string headerSignature, string secretKey)
    {
        if (string.IsNullOrWhiteSpace(payloadBody) || string.IsNullOrWhiteSpace(headerSignature) || string.IsNullOrWhiteSpace(secretKey))
        {
            _logger.LogWarning("Webhook signature verification failed: Missing payload body, signature header, or secret key.");
            return false;
        }

        try
        {
            // Normalize header signature (strip prefixes like sha256=)
            var cleanSignature = headerSignature.Trim();
            if (cleanSignature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
            {
                cleanSignature = cleanSignature[7..];
            }

            var secretBytes = Encoding.UTF8.GetBytes(secretKey);
            var payloadBytes = Encoding.UTF8.GetBytes(payloadBody);

            using var hmac = new HMACSHA256(secretBytes);
            var hashBytes = hmac.ComputeHash(payloadBytes);
            var computedHexSignature = Convert.ToHexStringLower(hashBytes);

            // Constant-time byte comparison prevents timing side-channel vulnerabilities
            var isMatch = CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computedHexSignature),
                Encoding.UTF8.GetBytes(cleanSignature.ToLowerInvariant()));

            if (!isMatch)
            {
                _logger.LogWarning("Webhook HMAC-SHA256 signature mismatch detected! Hostile payload modification or secret key mismatch.");
            }

            return isMatch;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred during webhook HMAC-SHA256 signature validation.");
            return false;
        }
    }
}
