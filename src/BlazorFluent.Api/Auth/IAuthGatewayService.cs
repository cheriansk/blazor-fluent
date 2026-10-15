using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BlazorFluent.Shared.Common;
using Microsoft.IdentityModel.Tokens;

namespace BlazorFluent.Api.Auth;

public record AuthCodeRecord(
    string Code,
    EntitlementUser User,
    DateTime ExpiresUtc,
    string ClientId
);

public record TokenResponseDto(
    string AccessToken,
    string TokenType,
    int ExpiresInSeconds,
    string TenantId,
    string UserEmail
);

public interface IAuthGatewayService
{
    Task<Result<string>> GenerateAuthorizationCodeAsync(string email, string clientId);
    Task<Result<TokenResponseDto>> ExchangeCodeForTokenAsync(string code, string clientId);
    Task<Result<EntitlementUser>> ValidateEntitlementAsync(string email);
}

public class AuthGatewayService : IAuthGatewayService
{
    private readonly IEntitlementsRepository _entitlementsRepo;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthGatewayService> _logger;
    private static readonly ConcurrentDictionary<string, AuthCodeRecord> PendingCodes = new();

    // In production, loaded from Azure Key Vault RSA key pair
    private static readonly SymmetricSecurityKey SigningKey = new(
        SHA256.HashData(Encoding.UTF8.GetBytes("BlazorFluent-Cloud-AuthGateway-MasterKey-2026-Secure!"))
    );

    public AuthGatewayService(
        IEntitlementsRepository entitlementsRepo,
        IConfiguration configuration,
        ILogger<AuthGatewayService> logger)
    {
        _entitlementsRepo = entitlementsRepo;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Result<EntitlementUser>> ValidateEntitlementAsync(string email)
    {
        var user = await _entitlementsRepo.GetByEmailAsync(email);
        if (user == null)
        {
            return Result<EntitlementUser>.Fail($"User '{email}' is not provisioned in corporate entitlements.");
        }

        if (!user.IsValidForLogin(DateTime.UtcNow))
        {
            return Result<EntitlementUser>.Fail($"Subscription or access window for '{email}' is inactive or expired.");
        }

        return Result<EntitlementUser>.Ok(user);
    }

    public async Task<Result<string>> GenerateAuthorizationCodeAsync(string email, string clientId)
    {
        var validation = await ValidateEntitlementAsync(email);
        if (!validation.IsSuccess)
        {
            return Result<string>.Fail(validation.Errors);
        }

        var code = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24));
        var record = new AuthCodeRecord(code, validation.Data!, DateTime.UtcNow.AddSeconds(30), clientId);
        PendingCodes[code] = record;

        _logger.LogInformation("Generated single-use 30-second auth code for {Email} under client {ClientId}", email, clientId);
        return Result<string>.Ok(code);
    }

    public Task<Result<TokenResponseDto>> ExchangeCodeForTokenAsync(string code, string clientId)
    {
        if (!PendingCodes.TryRemove(code, out var record))
        {
            return Task.FromResult(Result<TokenResponseDto>.Fail("Authorization code is invalid or has already been used."));
        }

        if (DateTime.UtcNow > record.ExpiresUtc)
        {
            return Task.FromResult(Result<TokenResponseDto>.Fail("Authorization code has expired."));
        }

        if (!string.Equals(record.ClientId, clientId, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(Result<TokenResponseDto>.Fail("Client mismatch for authorization code."));
        }

        var tokenHandler = new JwtSecurityTokenHandler();
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, record.User.Email),
            new(JwtRegisteredClaimNames.Email, record.User.Email),
            new("tenant_id", record.User.TenantId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        foreach (var role in record.User.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(8),
            Issuer = _configuration["Auth:Issuer"] ?? "https://auth.company.com",
            Audience = _configuration["Auth:Audience"] ?? "https://api.company.com",
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        var response = new TokenResponseDto(
            AccessToken: tokenString,
            TokenType: "Bearer",
            ExpiresInSeconds: 28800,
            TenantId: record.User.TenantId,
            UserEmail: record.User.Email
        );

        return Task.FromResult(Result<TokenResponseDto>.Ok(response));
    }
}
