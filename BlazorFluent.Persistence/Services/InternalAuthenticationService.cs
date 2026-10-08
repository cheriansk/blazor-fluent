using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Identity;
using BlazorFluent.Core.Domain.Tenancy;
using BlazorFluent.Core.Dtos.Response;
using BlazorFluent.Core.Utilities;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Persistence.Services;

/// <summary>
/// Dedicated Zero-Trust Authentication & Session Rehydration Service.
/// Executes credential checks, HMAC integrity verification, and session validation
/// inside an isolated, transient system daemon scope so unauthenticated client circuits
/// are completely denied direct database access.
/// </summary>
public class InternalAuthenticationService : IInternalAuthenticationService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly IRootAdminService _rootAdminService;
    private readonly ILogger<InternalAuthenticationService> _logger;

    public InternalAuthenticationService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        IRootAdminService rootAdminService,
        ILogger<InternalAuthenticationService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _rootAdminService = rootAdminService;
        _logger = logger;
    }

    public async Task<Result<AuthUserResult>> VerifyLoginCredentialsAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Result<AuthUserResult>.Failure("Email address is required.");
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();

        // Execute pre-authentication checks and queries inside an isolated, private scope with explicit pre-auth context
        using var scope = _scopeFactory.CreateScope();
        var currentUser = scope.ServiceProvider.GetRequiredService<ICurrentUser>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        SystemIdentityUtility.EstablishPreAuthContext(currentUser, tenantContext, normalizedEmail, "VerifyLogin");

        var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Security Gate: Reject background system daemon identity logins
        if (normalizedEmail == "system" || normalizedEmail == "system@daemon.local" || normalizedEmail.Contains("@daemon.local"))
        {
            await auditService.LogSecurityEventAsync(
                "SecurityViolationSystemLoginAttempt",
                AuditSeverity.Critical,
                $"Illicit login attempt blocked for reserved system daemon identity '{email}'.",
                ct);

            return Result<AuthUserResult>.Failure("System daemon identities are non-interactive background workers and cannot log in.");
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail && !u.IsDeleted, ct);

        if (user is null)
        {
            await auditService.LogSecurityEventAsync(
                "LoginFailedUnprovisioned",
                AuditSeverity.Warning,
                $"Authentication rejected for unprovisioned email '{email}'.",
                ct);

            return Result<AuthUserResult>.Failure("Your account is not provisioned in this system.");
        }

        var secretKey = _configuration["Security:IntegritySecret"];
        if (string.IsNullOrWhiteSpace(secretKey) || secretKey.StartsWith("__SET_VIA_"))
        {
            throw new InvalidOperationException("Zero-Trust Security Violation: 'Security:IntegritySecret' is not configured in KeyVault, environment, or User Secrets.");
        }
        if (!user.IsValidForLogin(DateTime.UtcNow, secretKey, out var failureReason))
        {
            await auditService.LogSecurityEventAsync(
                "LoginFailedValidityGate",
                AuditSeverity.Warning,
                $"Login rejected for '{email}': {failureReason}",
                ct);

            return Result<AuthUserResult>.Failure(failureReason);
        }

        var isRootAdmin = _rootAdminService.IsRootAdmin(user.Email);
        var isHost = isRootAdmin;

        var dbTenants = await dbContext.Tenants
            .AsNoTracking()
            .Where(t => t.IsActive && t.Slug != IRootAdminService.DefaultTenantSlug)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

        var (allowedTenants, resolvedTenantId, resolvedTenantName) = ResolveUserTenants(user, isHost, dbTenants);

        return Result<AuthUserResult>.Success(new AuthUserResult(
            user,
            isRootAdmin,
            allowedTenants,
            resolvedTenantId,
            resolvedTenantName));
    }

    public async Task<Result<AuthUserResult>> VerifyAndRehydrateSessionAsync(string userId, Guid sessionId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || sessionId == Guid.Empty)
        {
            return Result<AuthUserResult>.Failure("Invalid session parameters.");
        }

        // Execute query inside an isolated, private system daemon scope with explicit pre-auth identity
        using var scope = _scopeFactory.CreateScope();
        var currentUser = scope.ServiceProvider.GetRequiredService<ICurrentUser>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        SystemIdentityUtility.EstablishPreAuthContext(currentUser, tenantContext, userId, "RehydrateSession");

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Verify session validity directly in database (ignore tenant query filters for global session primary key)
        var session = await dbContext.UserSessions
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session is null || session.IsRevoked)
        {
            _logger.LogWarning("Session rehydration rejected: session {SessionId} not found or revoked.", sessionId);
            return Result<AuthUserResult>.Failure("Your active session has expired or was revoked by an administrator.");
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id.ToString() == userId && !u.IsDeleted, ct);

        if (user is null)
        {
            _logger.LogWarning("Session rehydration rejected: user {UserId} not found or deleted.", userId);
            return Result<AuthUserResult>.Failure("User account is no longer valid or has been removed.");
        }

        var secretKey = _configuration["Security:IntegritySecret"];
        if (string.IsNullOrWhiteSpace(secretKey) || secretKey.StartsWith("__SET_VIA_"))
        {
            throw new InvalidOperationException("Zero-Trust Security Violation: 'Security:IntegritySecret' is not configured in KeyVault, environment, or User Secrets.");
        }
        if (!user.IsValidForLogin(DateTime.UtcNow, secretKey, out var failureReason))
        {
            _logger.LogWarning("Session rehydration rejected for user {Email}: {Reason}", user.Email, failureReason);
            return Result<AuthUserResult>.Failure(failureReason);
        }

        var isRootAdmin = _rootAdminService.IsRootAdmin(user.Email);
        var isHost = isRootAdmin;

        var dbTenants = await dbContext.Tenants
            .AsNoTracking()
            .Where(t => t.IsActive && t.Slug != IRootAdminService.DefaultTenantSlug)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

        var (allowedTenants, resolvedTenantId, resolvedTenantName) = ResolveUserTenants(user, isHost, dbTenants);

        return Result<AuthUserResult>.Success(new AuthUserResult(
            user,
            isRootAdmin,
            allowedTenants,
            resolvedTenantId,
            resolvedTenantName));
    }

    /// <summary>
    /// Evaluates tenant access using strict Zero-Trust Default-Deny.
    /// Only RootAdmin (Host) receives all active tenants.
    /// Company users only receive tenants matching their verified email domain or explicit DefaultTenantId.
    /// Client users are strictly constrained to their single DefaultTenantId.
    /// </summary>
    private static (List<TenantInfo> AllowedTenants, string ResolvedTenantId, string ResolvedTenantName) ResolveUserTenants(
        UserEntity user,
        bool isHost,
        List<TenantEntity> activeTenants)
    {
        List<TenantInfo> allowedTenants = [];
        string resolvedTenantId = string.Empty;
        string resolvedTenantName = isHost ? "System Root Administration" : "Organization";

        if (isHost)
        {
            allowedTenants = activeTenants.Select(t => new TenantInfo(t.Slug, t.Name, t.IsActive)).ToList();
            var defaultMatch = allowedTenants.FirstOrDefault(t => t.Id == user.DefaultTenantId) ?? allowedTenants.FirstOrDefault();
            if (defaultMatch is not null)
            {
                resolvedTenantId = defaultMatch.Id;
                resolvedTenantName = defaultMatch.Name;
            }
        }
        else if (user.UserType == UserType.CompanyUser)
        {
            // Zero Trust: Company user only receives access to tenants where their email domain matches
            // or where they have an explicit DefaultTenantId assignment. Default deny for all other tenants.
            var userEmail = (user.Email ?? string.Empty).Trim().ToLowerInvariant();

            allowedTenants = activeTenants
                .Where(t => (!string.IsNullOrWhiteSpace(user.DefaultTenantId) && string.Equals(t.Slug, user.DefaultTenantId, StringComparison.OrdinalIgnoreCase)) ||
                            t.GetInternalDomains().Any(d => userEmail.EndsWith(d.ToLowerInvariant())) ||
                            t.GetExternalDomains().Any(d => userEmail.EndsWith(d.ToLowerInvariant())))
                .Select(t => new TenantInfo(t.Slug, t.Name, t.IsActive))
                .ToList();

            var defaultMatch = allowedTenants.FirstOrDefault(t => t.Id == user.DefaultTenantId) ?? allowedTenants.FirstOrDefault();
            if (defaultMatch is not null)
            {
                resolvedTenantId = defaultMatch.Id;
                resolvedTenantName = defaultMatch.Name;
            }
        }
        else
        {
            // ClientUser: Strictly bound to their designated DefaultTenantId
            if (!string.IsNullOrWhiteSpace(user.DefaultTenantId))
            {
                var clientTenant = activeTenants.FirstOrDefault(t => string.Equals(t.Slug, user.DefaultTenantId, StringComparison.OrdinalIgnoreCase));
                if (clientTenant is not null)
                {
                    allowedTenants = [new TenantInfo(clientTenant.Slug, clientTenant.Name, clientTenant.IsActive)];
                    resolvedTenantId = clientTenant.Slug;
                    resolvedTenantName = clientTenant.Name;
                }
            }
        }

        return (allowedTenants, resolvedTenantId, resolvedTenantName);
    }
}
