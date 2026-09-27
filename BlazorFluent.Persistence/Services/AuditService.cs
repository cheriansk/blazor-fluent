using BlazorFluent.Core.Common;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Auditing;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorFluent.Persistence.Services;

public class AuditService : IAuditService
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantContext _tenantContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<AuditService> _logger;

    public AuditService(
        AppDbContext dbContext,
        ICurrentUser currentUser,
        ITenantContext tenantContext,
        IDateTimeProvider dateTimeProvider,
        ILogger<AuditService> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task LogTenantSwitchAsync(
        string fromTenantId,
        string toTenantId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var now = _dateTimeProvider.Now;
        var currentUserId = _currentUser.UserId ?? "system";

        var record = new AuditRecordEntity
        {
            Id = Guid.NewGuid(),
            TenantId = toTenantId, // Audit lives in target tenant's audit trail
            UserId = currentUserId,
            UserEmail = _currentUser.Email,
            UserType = _tenantContext.UserType,
            EventType = AuditEventType.TenantSwitch,
            Severity = AuditSeverity.Information,
            Description = $"User switched active tenant from '{fromTenantId}' to '{toTenantId}'. Reason: {reason ?? "User interactive selection"}",
            Created = now,
            CreatedBy = currentUserId
        };

        _dbContext.AuditRecords.Add(record);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "AUDIT [TenantSwitch]: User {UserId} switched tenant from {FromTenant} to {ToTenant}. Reason: {Reason}",
            currentUserId, fromTenantId, toTenantId, reason ?? "User interactive selection");
    }

    public async Task LogSecurityEventAsync(
        string action,
        AuditSeverity severity,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        var now = _dateTimeProvider.Now;
        var currentUserId = _currentUser.UserId ?? "system";
        var tenantId = _tenantContext.TenantId ?? "host";

        var record = new AuditRecordEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = currentUserId,
            UserEmail = _currentUser.Email,
            UserType = _tenantContext.UserType,
            EventType = AuditEventType.Security,
            Severity = severity,
            Description = $"{action}. Details: {details}",
            Created = now,
            CreatedBy = currentUserId
        };

        _dbContext.AuditRecords.Add(record);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var logLevel = severity switch
        {
            AuditSeverity.Information => LogLevel.Information,
            AuditSeverity.Warning => LogLevel.Warning,
            AuditSeverity.Error => LogLevel.Error,
            AuditSeverity.Critical => LogLevel.Critical,
            _ => LogLevel.Information
        };

        _logger.Log(
            logLevel,
            "AUDIT [Security]: Action={Action}, Severity={Severity}, Tenant={TenantId}, User={UserId}, Details={Details}",
            action, severity, tenantId, currentUserId, details);
    }

    public async Task LogUserActivityAsync(
        string action,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        var now = _dateTimeProvider.Now;
        var currentUserId = _currentUser.UserId ?? "system";
        var tenantId = _tenantContext.TenantId ?? "host";

        var record = new AuditRecordEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = currentUserId,
            UserEmail = _currentUser.Email,
            UserType = _tenantContext.UserType,
            EventType = AuditEventType.UserActivity,
            Severity = AuditSeverity.Information,
            Description = $"{action}. Details: {details}",
            Created = now,
            CreatedBy = currentUserId
        };

        _dbContext.AuditRecords.Add(record);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "AUDIT [UserActivity]: Action={Action}, Tenant={TenantId}, User={UserId}, Details={Details}",
            action, tenantId, currentUserId, details);
    }

    public async Task<PagedResult<AuditRecordEntity>> GetAuditTrailAsync(
        AuditTrailFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.AuditRecords
            .IgnoreQueryFilters()
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.TenantId))
            query = query.Where(a => a.TenantId == filter.TenantId);

        if (filter.EventType.HasValue)
            query = query.Where(a => a.EventType == filter.EventType.Value);

        if (filter.Severity.HasValue)
            query = query.Where(a => a.Severity == filter.Severity.Value);

        if (filter.FromDate.HasValue)
            query = query.Where(a => a.Created >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(a => a.Created <= filter.ToDate.Value);

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.ToLower();
            query = query.Where(a =>
                (a.Description != null && a.Description.ToLower().Contains(term)) ||
                (a.UserId != null && a.UserId.ToLower().Contains(term)) ||
                (a.EntityName != null && a.EntityName.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.Created)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<AuditRecordEntity>.Create(items, totalCount, filter.PageNumber, filter.PageSize);
    }
}
