using BlazorFluent.Core.Common;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Identity;
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
    private readonly IAuditService _auditService;
    private readonly ILogger<InternalAuthenticationService> _logger;

    public InternalAuthenticationService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        IRootAdminService rootAdminService,
        IAuditService auditService,
        ILogger<InternalAuthenticationService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _rootAdminService = rootAdminService;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<Result<AuthUserResult>> VerifyLoginCredentialsAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Result<AuthUserResult>.Failure("Email address is required.");
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();

        // Security Gate: Reject background system daemon identity logins
        if (normalizedEmail == "system" || normalizedEmail == "system@daemon.local" || normalizedEmail.Contains("@daemon.local"))
        {
            await _auditService.LogSecurityEventAsync(
                "SecurityViolationSystemLoginAttempt",
                AuditSeverity.Critical,
                $"Illicit login attempt blocked for reserved system daemon identity '{email}'.",
                ct);

            return Result<AuthUserResult>.Failure("System daemon identities are non-interactive background workers and cannot log in.");
        }

        // Execute query inside an isolated, private system daemon scope
        using var scope = _scopeFactory.CreateScope();
        var currentUser = scope.ServiceProvider.GetRequiredService<ICurrentUser>();
        currentUser.SetSystemDaemon("InternalAuthService:VerifyLogin");

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail && !u.IsDeleted, ct);

        if (user is null)
        {
            await _auditService.LogSecurityEventAsync(
                "LoginFailedUnprovisioned",
                AuditSeverity.Warning,
                $"Authentication rejected for unprovisioned email '{email}'.",
                ct);

            return Result<AuthUserResult>.Failure("Your account is not provisioned in this system.");
        }

        var secretKey = _configuration["Security:IntegritySecret"] ?? "BlazorFluent-Secret-Key-Change-In-Production-2026";
        if (!user.IsValidForLogin(DateTime.UtcNow, secretKey, out var failureReason))
        {
            await _auditService.LogSecurityEventAsync(
                "LoginFailedValidityGate",
                AuditSeverity.Warning,
                $"Login rejected for '{email}': {failureReason}",
                ct);

            return Result<AuthUserResult>.Failure(failureReason);
        }

        var isRootAdmin = _rootAdminService.IsRootAdmin(user.Email);
        var isHost = isRootAdmin;

        List<TenantInfo> allowedTenants = [];
        string resolvedTenantId = string.Empty;
        string resolvedTenantName = isHost ? "System Root Administration" : "Organization";

        if (isHost || user.UserType == UserType.CompanyUser)
        {
            var dbTenants = await dbContext.Tenants
                .AsNoTracking()
                .Where(t => t.IsActive && t.Slug != IRootAdminService.DefaultTenantSlug)
                .OrderBy(t => t.Name)
                .ToListAsync(ct);

            allowedTenants = dbTenants.Select(t => new TenantInfo(t.Slug, t.Name, isHost)).ToList();

            var defaultMatch = allowedTenants.FirstOrDefault(t => t.Id == user.DefaultTenantId) ?? allowedTenants.FirstOrDefault();
            if (defaultMatch is not null)
            {
                resolvedTenantId = defaultMatch.Id;
                resolvedTenantName = defaultMatch.Name;
            }
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(user.DefaultTenantId))
            {
                var clientTenant = await dbContext.Tenants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Slug == user.DefaultTenantId && t.IsActive && t.Slug != IRootAdminService.DefaultTenantSlug, ct);

                if (clientTenant is not null)
                {
                    allowedTenants = [new TenantInfo(clientTenant.Slug, clientTenant.Name, false)];
                    resolvedTenantId = clientTenant.Slug;
                    resolvedTenantName = clientTenant.Name;
                }
            }
        }

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

        // Execute query inside an isolated, private system daemon scope
        using var scope = _scopeFactory.CreateScope();
        var currentUser = scope.ServiceProvider.GetRequiredService<ICurrentUser>();
        currentUser.SetSystemDaemon("InternalAuthService:RehydrateSession");

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

        var secretKey = _configuration["Security:IntegritySecret"] ?? "BlazorFluent-Secret-Key-Change-In-Production-2026";
        if (!user.IsValidForLogin(DateTime.UtcNow, secretKey, out var failureReason))
        {
            _logger.LogWarning("Session rehydration rejected for user {Email}: {Reason}", user.Email, failureReason);
            return Result<AuthUserResult>.Failure(failureReason);
        }

        var isRootAdmin = _rootAdminService.IsRootAdmin(user.Email);
        var isHost = isRootAdmin;

        List<TenantInfo> allowedTenants = [];
        string resolvedTenantId = string.Empty;
        string resolvedTenantName = isHost ? "System Root Administration" : "Organization";

        if (isHost || user.UserType == UserType.CompanyUser)
        {
            var dbTenants = await dbContext.Tenants
                .AsNoTracking()
                .Where(t => t.IsActive && t.Slug != IRootAdminService.DefaultTenantSlug)
                .OrderBy(t => t.Name)
                .ToListAsync(ct);

            allowedTenants = dbTenants.Select(t => new TenantInfo(t.Slug, t.Name, isHost)).ToList();

            var defaultMatch = allowedTenants.FirstOrDefault(t => t.Id == user.DefaultTenantId) ?? allowedTenants.FirstOrDefault();
            if (defaultMatch is not null)
            {
                resolvedTenantId = defaultMatch.Id;
                resolvedTenantName = defaultMatch.Name;
            }
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(user.DefaultTenantId))
            {
                var clientTenant = await dbContext.Tenants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Slug == user.DefaultTenantId && t.IsActive && t.Slug != IRootAdminService.DefaultTenantSlug, ct);

                if (clientTenant is not null)
                {
                    allowedTenants = [new TenantInfo(clientTenant.Slug, clientTenant.Name, false)];
                    resolvedTenantId = clientTenant.Slug;
                    resolvedTenantName = clientTenant.Name;
                }
            }
        }

        return Result<AuthUserResult>.Success(new AuthUserResult(
            user,
            isRootAdmin,
            allowedTenants,
            resolvedTenantId,
            resolvedTenantName));
    }
}
