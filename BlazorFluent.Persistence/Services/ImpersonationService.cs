using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Identity;
using BlazorFluent.Core.Dtos.Response;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Persistence.Services;

public class ImpersonationService : IImpersonationService
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditService _auditService;
    private readonly ILogger<ImpersonationService> _logger;

    public ImpersonationService(
        AppDbContext dbContext,
        ICurrentUser currentUser,
        ITenantContext tenantContext,
        IAuditService auditService,
        ILogger<ImpersonationService> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<Result<ImpersonationGrantEntity>> StartImpersonationAsync(
        string targetUserId,
        string reason,
        int durationMinutes = 60,
        CancellationToken ct = default)
    {
        // 1. Strict Host Admin Security Boundary
        if (!_currentUser.IsRootAdmin)
        {
            _logger.LogWarning("Unauthorized impersonation attempt by user {UserId}", _currentUser.UserId);
            return Result<ImpersonationGrantEntity>.Failure("Only Host Administrators can initiate operator impersonation.");
        }

        // 2. Mandatory Reason
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result<ImpersonationGrantEntity>.Failure("A justification reason or incident ticket is required for impersonation.");
        }

        // 3. Resolve Target User
        var targetUser = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id.ToString() == targetUserId || u.Email == targetUserId, ct);

        if (targetUser == null)
        {
            return Result<ImpersonationGrantEntity>.Failure($"Target user '{targetUserId}' was not found.");
        }

        // Revoke any previous active grants for this admin
        var adminId = _currentUser.UserId ?? "admin";
        var existingGrants = await _dbContext.ImpersonationGrants
            .Where(g => g.SourceAdminId == adminId && !g.IsRevoked)
            .ToListAsync(ct);

        foreach (var g in existingGrants)
        {
            g.IsRevoked = true;
            g.RevokedAtUtc = DateTime.UtcNow;
        }

        // Cap maximum duration to 60 minutes
        var cappedMinutes = Math.Clamp(durationMinutes, 5, 60);

        var grant = new ImpersonationGrantEntity
        {
            SourceAdminId = adminId,
            SourceAdminEmail = _currentUser.Email ?? "admin@blazorfluent.local",
            TargetUserId = targetUser.Id.ToString(),
            TargetUserEmail = targetUser.Email,
            TargetTenantId = targetUser.DefaultTenantId ?? _tenantContext.TenantId ?? "default",
            StartedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(cappedMinutes),
            Reason = reason,
            IsRevoked = false
        };

        _dbContext.ImpersonationGrants.Add(grant);
        await _dbContext.SaveChangesAsync(ct);

        // Forensic Security Audit Log
        await _auditService.LogSecurityEventAsync(
            "OperatorImpersonationStarted",
            AuditSeverity.Warning,
            $"SuperAdmin '{grant.SourceAdminEmail}' started impersonating '{grant.TargetUserEmail}' (Target Tenant: {grant.TargetTenantId}). Reason: '{reason}'. Duration: {cappedMinutes} mins.");

        _logger.LogWarning("IMPERSONATION STARTED: Admin {Admin} is now impersonating {TargetUser} (Expires at {ExpiresUtc})",
            grant.SourceAdminEmail, grant.TargetUserEmail, grant.ExpiresAtUtc);

        return Result<ImpersonationGrantEntity>.Success(grant);
    }

    public async Task<Result> StopImpersonationAsync(CancellationToken ct = default)
    {
        var adminId = _currentUser.UserId ?? "admin";
        var activeGrants = await _dbContext.ImpersonationGrants
            .Where(g => g.SourceAdminId == adminId && !g.IsRevoked)
            .ToListAsync(ct);

        if (activeGrants.Count == 0) return Result.Success();

        foreach (var g in activeGrants)
        {
            g.IsRevoked = true;
            g.RevokedAtUtc = DateTime.UtcNow;

            await _auditService.LogSecurityEventAsync(
                "OperatorImpersonationEnded",
                AuditSeverity.Information,
                $"SuperAdmin '{g.SourceAdminEmail}' exited impersonation of '{g.TargetUserEmail}'.");
        }

        await _dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<ImpersonationGrantEntity?> GetActiveGrantAsync(CancellationToken ct = default)
    {
        var adminId = _currentUser.UserId ?? "admin";
        var now = DateTime.UtcNow;

        return await _dbContext.ImpersonationGrants
            .AsNoTracking()
            .OrderByDescending(g => g.StartedAtUtc)
            .FirstOrDefaultAsync(g => g.SourceAdminId == adminId && !g.IsRevoked && g.ExpiresAtUtc > now, ct);
    }
}
