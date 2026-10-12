namespace BlazorFluent.Core.Contracts;

/// <summary>
/// Cryptographic signature validator for incoming HTTP webhooks (e.g. Stripe, GitHub, Azure AD callbacks).
/// </summary>
public interface IWebhookSignatureValidator
{
    /// <summary>
    /// Validates an incoming HTTP webhook request payload against an expected HMAC-SHA256 signature header.
    /// Uses constant-time comparison (CryptographicOperations.FixedTimeEquals) to prevent timing side-channel attacks.
    /// </summary>
    /// <param name="payloadBody">The raw UTF-8 string body of the incoming webhook HTTP request.</param>
    /// <param name="headerSignature">The signature header string received in the request (e.g., X-Hub-Signature-256 or Stripe-Signature).</param>
    /// <param name="secretKey">The shared secret key configured for the webhook provider.</param>
    /// <returns>True if the computed HMAC-SHA256 signature matches the header signature; otherwise false.</returns>
    bool ValidateHmacSha256(string payloadBody, string headerSignature, string secretKey);
}
