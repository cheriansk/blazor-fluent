using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Shared.DataListTypes;
using BlazorEnterprise.Core.Domain.Identity;
using BlazorEnterprise.Core.Domain.Tenancy;
using BlazorEnterprise.Core.Dtos.Response;
using BlazorEnterprise.Core.Utilities;
using BlazorEnterprise.Persistence.Context;
using BlazorEnterprise.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TenantInfo = BlazorEnterprise.Core.Contracts.TenantInfo;

namespace BlazorEnterprise.Persistence.Services;

/// <summary>
/// Dedicated Zero-Trust Authentication & Session Rehydration Service.
/// Executes credential checks, HMAC integrity verification, and session validation
/// inside an isolated, transient system daemon scope so unauthenticated client circuits
/// are completely denied direct database access.
/// </summary>
public class InternalAuthenticationService : IInternalAuthenticationService
{
    private static readonly Func<AppDbContext, Guid, Task<UserEntity?>> GetUserByIdCompiled =
        EF.CompileAsyncQuery((AppDbContext ctx, Guid id) =>
            ctx.Users.AsNoTracking().FirstOrDefault(u => u.Id == id && !u.IsDeleted));

    private static readonly Func<AppDbContext, string, Task<UserEntity?>> GetUserByEmailCompiled =
        EF.CompileAsyncQuery((AppDbContext ctx, string email) =>
            ctx.Users.AsNoTracking().FirstOrDefault(u => u.Email.ToLower() == email && !u.IsDeleted));

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly IRootAdminService _rootAdminService;
    private readonly ISystemIdentity _systemIdentity;
    private readonly ILogger<InternalAuthenticationService> _logger;

    public InternalAuthenticationService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        IRootAdminService rootAdminService,
        ISystemIdentity systemIdentity,
        ILogger<InternalAuthenticationService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _rootAdminService = rootAdminService;
        _systemIdentity = systemIdentity;
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
        SystemIdentityUtility.EstablishPreAuthContext(currentUser, tenantContext, _systemIdentity, normalizedEmail, "VerifyLogin");

        var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Security Gate: Reject background system daemon identity logins
        if (_systemIdentity.IsSystemDaemon(normalizedEmail))
        {
            await auditService.LogSecurityEventAsync(
                "SecurityViolationSystemLoginAttempt",
                AuditSeverityEnum.Critical,
                $"Illicit login attempt blocked for reserved system daemon identity '{email}'.",
                ct);

            return Result<AuthUserResult>.Failure("System daemon identities are non-interactive background workers and cannot log in.");
        }

        var user = await GetUserByEmailCompiled(dbContext, normalizedEmail);

        if (user is null)
        {
            await auditService.LogSecurityEventAsync(
                "LoginFailedUnprovisioned",
                AuditSeverityEnum.Critical,
                $"Authentication rejected for unprovisioned email '{email}'.",
                ct);

            return Result<AuthUserResult>.Failure("Your account is not provisioned in this system.");
        }

        if (user.UserKind != UserKindEnum.Human)
        {
            await auditService.LogSecurityEventAsync(
                "SecurityViolationSystemLoginAttempt",
                AuditSeverityEnum.Critical,
                $"Illicit login attempt blocked for non-human account kind '{user.UserKind}' ('{email}').",
                ct);

            return Result<AuthUserResult>.Failure("System daemon and bot accounts are non-interactive identities and cannot log in.");
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
                AuditSeverityEnum.Critical,
                $"Login rejected for '{email}': {failureReason}",
                ct);

            return Result<AuthUserResult>.Failure(failureReason);
        }


        var isRootAdmin = _rootAdminService.IsRootAdmin(user.Email);
        var isHost = isRootAdmin;
        var dbTenants = await dbContext.Tenants
            .AsNoTracking()
            .Where(t => t.IsActive && t.Slug != _rootAdminService.RootTenantSlug)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

        var rootTenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Slug == _rootAdminService.RootTenantSlug, ct);

        var nowUtc = DateTime.UtcNow;
        var userMemberships = await dbContext.TenantUsers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(tu => tu.UserEntityId == user.Id && tu.IsActive && !tu.IsDeleted)
            .Include(tu => tu.Tenant)
            .ToListAsync(ct);

        var (allowedTenants, resolvedTenantId, resolvedTenantName, resolvedTenantSlug) = ResolveUserTenants(user, isHost, dbTenants, rootTenant, userMemberships);

        return Result<AuthUserResult>.Success(new AuthUserResult(
            user,
            isRootAdmin,
            allowedTenants,
            resolvedTenantId,
            resolvedTenantName,
            resolvedTenantSlug));
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
        SystemIdentityUtility.EstablishPreAuthContext(currentUser, tenantContext, _systemIdentity, userId, "RehydrateSession");

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();

        // ARCHITECTURAL ZERO-TRUST EXEMPTION:
        // Primary-key GUID session lookup executes during initial pre-auth handshake before tenant identity is resolved.
        // Bypassing tenant filters here is strictly audited and scoped to the exact unique session GUID primary key.
        var session = await dbContext.UserSessions
            .AsNoTracking()
            .Where(s => s.Id == sessionId)
            .FirstOrDefaultWithAuditedBypassAsync(
                auditService,
                "Primary-key GUID session lookup during pre-auth handshake before tenant identity is resolved.",
                ct);

        if (session is null || session.IsRevoked)
        {
            _logger.LogWarning("Session rehydration rejected: session {SessionId} not found or revoked.", sessionId);
            return Result<AuthUserResult>.Failure("Your active session has expired or was revoked by an administrator.");
        }

        var now = DateTime.UtcNow;

        // Hard Limit: 8-Hour Absolute Maximum Session Lifetime
        if (session.StartedAtUtc.AddHours(8) < now)
        {
            await dbContext.UserSessions
                .Where(s => s.Id == sessionId)
                .ExecuteUpdateWithAuditedBypassAsync(
                    auditService,
                    "Pre-auth revocation: session exceeded absolute 8-hour maximum lifetime.",
                    s => s
                        .SetProperty(x => x.IsRevoked, true)
                        .SetProperty(x => x.RevokedAtUtc, now)
                        .SetProperty(x => x.RevokedBy, "System:MaxSessionLifetimeExceeded"),
                    ct);

            _logger.LogWarning("Session rehydration rejected: session {SessionId} exceeded absolute 8-hour maximum lifetime.", sessionId);
            return Result<AuthUserResult>.Failure("Your session has reached the maximum 8-hour limit. Please sign in again.");
        }

        // Hard Limit: 30-Minute Idle Inactivity Timeout
        if (session.LastActivityAtUtc.AddMinutes(30) < now)
        {
            await dbContext.UserSessions
                .Where(s => s.Id == sessionId)
                .ExecuteUpdateWithAuditedBypassAsync(
                    auditService,
                    "Pre-auth revocation: session exceeded 30-minute idle threshold.",
                    s => s
                        .SetProperty(x => x.IsRevoked, true)
                        .SetProperty(x => x.RevokedAtUtc, now)
                        .SetProperty(x => x.RevokedBy, "System:IdleTimeoutExceeded"),
                    ct);

            _logger.LogWarning("Session rehydration rejected: session {SessionId} exceeded 30-minute idle threshold.", sessionId);
            return Result<AuthUserResult>.Failure("Your session has expired due to 30 minutes of inactivity. Please sign in again.");
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id.ToString() == userId && !u.IsDeleted, ct);

        if (user is null)
        {
            _logger.LogCritical("SECURITY VIOLATION: Session rehydration rejected: user {UserId} not found or deleted.", userId);
            return Result<AuthUserResult>.Failure("User account is no longer valid or has been removed.");
        }

        var secretKey = _configuration["Security:IntegritySecret"];
        if (string.IsNullOrWhiteSpace(secretKey) || secretKey.StartsWith("__SET_VIA_"))
        {
            throw new InvalidOperationException("Zero-Trust Security Violation: 'Security:IntegritySecret' is not configured in KeyVault, environment, or User Secrets.");
        }
        if (!user.IsValidForLogin(DateTime.UtcNow, secretKey, out var failureReason))
        {
            _logger.LogCritical("SECURITY VIOLATION: Session rehydration rejected for user {Email}: {Reason}", user.Email, failureReason);
            return Result<AuthUserResult>.Failure(failureReason);
        }


        var isRootAdmin = _rootAdminService.IsRootAdmin(user.Email);
        var isHost = isRootAdmin;
        var dbTenants = await dbContext.Tenants
            .AsNoTracking()
            .Where(t => t.IsActive && t.Slug != _rootAdminService.RootTenantSlug)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

        var rootTenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Slug == _rootAdminService.RootTenantSlug, ct);

        var userMemberships = await dbContext.TenantUsers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(tu => tu.UserEntityId == user.Id && tu.IsActive && !tu.IsDeleted)
            .Include(tu => tu.Tenant)
            .ToListAsync(ct);

        var (allowedTenants, resolvedTenantId, resolvedTenantName, resolvedTenantSlug) = ResolveUserTenants(user, isHost, dbTenants, rootTenant, userMemberships);

        return Result<AuthUserResult>.Success(new AuthUserResult(
            user,
            isRootAdmin,
            allowedTenants,
            resolvedTenantId,
            resolvedTenantName,
            resolvedTenantSlug));
    }

    /// <summary>
    /// Evaluates tenant access using strict Zero-Trust Default-Deny.
    /// Only RootAdmin (Host) receives all active tenants, anchored initially in the Root Tenant.
    /// Regular users receive tenants derived strictly from active, date-valid records in tenancy.tenant_users.
    /// Zero tenant assumption: if a user belongs to multiple tenants, resolvedTenantId is Guid.Empty until explicitly selected.
    /// </summary>
    private (List<TenantInfo> AllowedTenants, Guid ResolvedTenantId, string ResolvedTenantName, string ResolvedTenantSlug) ResolveUserTenants(
        UserEntity user,
        bool isHost,
        List<TenantEntity> activeTenants,
        TenantEntity? rootTenant,
        List<TenantUserEntity> userMemberships)
    {
        List<TenantInfo> allowedTenants = [];
        Guid resolvedTenantId = Guid.Empty;
        string resolvedTenantSlug = string.Empty;
        string resolvedTenantName = string.Empty;

        // If user is a verified root admin and root tenant exists and user has root tenant membership
        var hasRootMembership = userMemberships.Any(m => m.TenantId == _rootAdminService.RootTenantId || m.Tenant?.Slug == _rootAdminService.RootTenantSlug);
        if (isHost && rootTenant is not null && hasRootMembership)
        {
            allowedTenants.Add(new TenantInfo(rootTenant.Id, rootTenant.Slug, rootTenant.Name, rootTenant.IsActive));
        }

        // Add customer tenants where user has explicit, active TenantUserEntity membership
        var customerMemberships = userMemberships
            .Where(m => m.Tenant != null && m.Tenant.IsActive && m.Tenant.Id != _rootAdminService.RootTenantId && m.Tenant.Slug != _rootAdminService.RootTenantSlug)
            .Select(m => new TenantInfo(m.Tenant!.Id, m.Tenant.Slug, m.Tenant.Name, m.Tenant.IsActive))
            .DistinctBy(t => t.Id);

        allowedTenants.AddRange(customerMemberships);

        // Zero Assumption: Only auto-resolve if the user belongs to exactly one tenant
        if (allowedTenants.Count == 1)
        {
            var single = allowedTenants[0];
            resolvedTenantId = single.Id;
            resolvedTenantSlug = single.Slug;
            resolvedTenantName = single.Name;
        }

        return (allowedTenants, resolvedTenantId, resolvedTenantName, resolvedTenantSlug);
    }
}
