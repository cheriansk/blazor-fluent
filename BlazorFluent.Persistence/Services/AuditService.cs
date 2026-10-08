using System.Text.Json;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Auditing;
using BlazorFluent.Core.Utilities;
using BlazorFluent.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BlazorFluent.Core.Dtos.Requests;
using BlazorFluent.Core.Dtos.Response;

namespace BlazorFluent.Persistence.Services;

internal class AuditService : IAuditService
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantContext _tenantContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditService> _logger;

    public AuditService(
        AppDbContext dbContext,
        ICurrentUser currentUser,
        ITenantContext tenantContext,
        IDateTimeProvider dateTimeProvider,
        IServiceScopeFactory scopeFactory,
        ILogger<AuditService> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
        _dateTimeProvider = dateTimeProvider;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task LogTenantSwitchAsync(
        string fromTenantId,
        string toTenantId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        SystemIdentityUtility.RequireAuthenticatedContext(_currentUser, _tenantContext, "LogTenantSwitch");
        var currentUserId = SystemIdentityUtility.ResolveAuditableUserId(_currentUser, "LogTenantSwitch");
        var now = _dateTimeProvider.Now;

        var record = new AuditRecordEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = toTenantId, // Audit lives in target tenant's audit trail
            UserId = currentUserId,
            UserEmail = _currentUser.Email,
            UserType = _tenantContext.UserType,
            EventType = AuditEventType.TenantSwitch,
            Severity = AuditSeverity.Information,
            Description = $"User switched active tenant from '{fromTenantId}' to '{toTenantId}'. Reason: {reason ?? "User interactive selection"}",
            Created = now,
            CreatedBy = currentUserId,
            Updated = now,
            UpdatedBy = currentUserId
        };

        await SaveAuditRecordAsync(record, cancellationToken);

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
        SystemIdentityUtility.RequireAuthenticatedContext(_currentUser, _tenantContext, $"LogSecurityEvent:{action}");
        var currentUserId = SystemIdentityUtility.ResolveAuditableUserId(_currentUser, $"LogSecurityEvent:{action}");
        var tenantId = _tenantContext.TenantId!;
        var now = _dateTimeProvider.Now;

        var record = new AuditRecordEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            UserId = currentUserId,
            UserEmail = _currentUser.Email,
            UserType = _tenantContext.UserType,
            EventType = AuditEventType.Security,
            Severity = severity,
            Description = $"{action}. Details: {details}",
            Created = now,
            CreatedBy = currentUserId,
            Updated = now,
            UpdatedBy = currentUserId
        };

        await SaveAuditRecordAsync(record, cancellationToken);

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
        SystemIdentityUtility.RequireAuthenticatedContext(_currentUser, _tenantContext, $"LogUserActivity:{action}");
        var currentUserId = SystemIdentityUtility.ResolveAuditableUserId(_currentUser, $"LogUserActivity:{action}");
        var tenantId = _tenantContext.TenantId!;
        var now = _dateTimeProvider.Now;

        var record = new AuditRecordEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            UserId = currentUserId,
            UserEmail = _currentUser.Email,
            UserType = _tenantContext.UserType,
            EventType = AuditEventType.UserActivity,
            Severity = AuditSeverity.Information,
            Description = $"{action}. Details: {details}",
            Created = now,
            CreatedBy = currentUserId,
            Updated = now,
            UpdatedBy = currentUserId
        };

        await SaveAuditRecordAsync(record, cancellationToken);

        _logger.LogInformation(
            "AUDIT [UserActivity]: Action={Action}, Tenant={TenantId}, User={UserId}, Details={Details}",
            action, tenantId, currentUserId, details);
    }

    private async Task SaveAuditRecordAsync(AuditRecordEntity record, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var currentUser = scope.ServiceProvider.GetService<ICurrentUser>();
            currentUser?.SetSystemDaemon("AuditTrailLogger");
            var isolatedDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            isolatedDbContext.AuditRecords.Add(record);
            await isolatedDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist audit record {RecordId} via isolated scope: {Description}", record.Id, record.Description);
        }
    }
    //REVIEWED-CSK
    public async Task<PagedResultRespDto<AuditRecordEntity>> GetAuditTrailAsync(
        AuditTrailFilterReqDto filter,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.AuditRecords.Where(a => a.TenantId == filter.TenantId)
            .IgnoreQueryFilters()
            .AsNoTracking();

        if (filter.AllowedTenantIds != null && filter.AllowedTenantIds.Count > 0)
        {
            query = query.Where(a => filter.AllowedTenantIds.Contains(a.TenantId));
        }

        if (!string.IsNullOrWhiteSpace(filter.UserId))
        {
            query = query.Where(a => a.UserId == filter.UserId || a.UserEmail == filter.UserId);
        }

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

        return PagedResultRespDto<AuditRecordEntity>.Create(items, totalCount, filter.PageNumber, filter.PageSize);
    }

    public async Task<string> LogExceptionAsync(
        Exception exception,
        string? contextDescription = null,
        AuditSeverity severity = AuditSeverity.Error,
        CancellationToken cancellationToken = default)
    {
        var errorId = $"ERR-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var now = _dateTimeProvider.Now;
        SystemIdentityUtility.RequireAuthenticatedContext(_currentUser, _tenantContext, "LogException");
        var currentUserId = SystemIdentityUtility.ResolveAuditableUserId(_currentUser, "LogException");
        var tenantId = _tenantContext.TenantId!;

        // Structured JSON payload detailing the incident
        var exceptionPayload = new
        {
            IncidentReference = errorId,
            ExceptionType = exception.GetType().FullName,
            exception.Message,
            exception.Source,
            StackTrace = exception.StackTrace?.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries),
            InnerException = exception.InnerException is not null ? new
            {
                Type = exception.InnerException.GetType().FullName,
                exception.InnerException.Message,
                StackTrace = exception.InnerException.StackTrace?.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            } : null
        };

        var changesJson = JsonSerializer.Serialize(exceptionPayload, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        // 1. Always log structured incident to Serilog stream & rolling file sink
        _logger.LogError(
            exception,
            "INCIDENT [{ErrorId}]: Context={Context}, Tenant={TenantId}, User={UserId}, Message={Message}",
            errorId, contextDescription ?? "General", tenantId, currentUserId, exception.Message);

        // 2. Persist to audit.AuditRecords using an isolated scope
        // An isolated scope ensures that even if the caller's DbContext transaction is currently aborted/rolling back,
        // this exception record is guaranteed to be saved to PostgreSQL!
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var currentUser = scope.ServiceProvider.GetService<ICurrentUser>();
            currentUser?.SetSystemDaemon("ForensicAuditLogger", "audit-logger@blazorfluent.local");
            var isolatedDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var record = new AuditRecordEntity
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenantId,
                UserId = currentUserId,
                UserEmail = _currentUser.Email,
                UserType = _tenantContext.UserType,
                EventType = AuditEventType.Exception,
                Severity = severity,
                Description = string.IsNullOrWhiteSpace(contextDescription)
                    ? $"Exception: {exception.Message}"
                    : $"{contextDescription}: {exception.Message}",
                ChangesJson = changesJson,
                TraceId = errorId,
                Created = now,
                CreatedBy = currentUserId,
                Updated = now,
                UpdatedBy = currentUserId
            };

            isolatedDbContext.AuditRecords.Add(record);
            await isolatedDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception persistEx)
        {
            _logger.LogCritical(persistEx, "Failed to persist exception audit record for Incident {ErrorId} to PostgreSQL!", errorId);
        }

        return errorId;
    }

    public async Task<List<EntityFieldHistoryRespDto>> GetEntityHistoryAsync(
        string entityName,
        string entityId,
        string? propertyName = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.AuditRecords
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(a => a.EntityName == entityName && a.EntityId == entityId);

        // Zero Trust: Non-host users can only view audit trail for their own active tenant
        if (!_tenantContext.IsHost)
        {
            var activeTenant = _tenantContext.TenantId ?? string.Empty;
            query = query.Where(a => a.TenantId == activeTenant);
        }

        var records = await query
            .OrderByDescending(a => a.Created)
            .ToListAsync(cancellationToken);

        var result = new List<EntityFieldHistoryRespDto>();

        foreach (var record in records)
        {
            var userEmail = !string.IsNullOrWhiteSpace(record.UserEmail)
                ? record.UserEmail
                : (!string.IsNullOrWhiteSpace(record.UserId) ? record.UserId : "System");

            if (string.IsNullOrWhiteSpace(record.ChangesJson))
            {
                result.Add(new EntityFieldHistoryRespDto
                {
                    AuditRecordId = record.Id.ToString(),
                    EntityName = record.EntityName ?? entityName,
                    EntityId = record.EntityId ?? entityId,
                    PropertyName = propertyName ?? "Entity Record",
                    OldValue = null,
                    NewValue = record.Operation?.ToString() ?? record.EventType.ToString(),
                    UserId = record.UserId ?? "Unknown",
                    UserEmail = userEmail,
                    UserType = record.UserType,
                    Operation = record.Operation ?? EntityOperation.Update,
                    Timestamp = record.Created,
                    Description = record.Description
                });
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(record.ChangesJson);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) continue;

                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    var propName = prop.Name;

                    if (!string.IsNullOrWhiteSpace(propertyName) &&
                        !string.Equals(propName, propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string? oldVal = null;
                    string? newVal = null;

                    if (record.Operation == EntityOperation.Update)
                    {
                        if (prop.Value.ValueKind == JsonValueKind.Object &&
                            (prop.Value.TryGetProperty("Old", out var oldElem) || prop.Value.TryGetProperty("old", out oldElem)) &&
                            (prop.Value.TryGetProperty("New", out var newElem) || prop.Value.TryGetProperty("new", out newElem)))
                        {
                            oldVal = oldElem.ValueKind == JsonValueKind.Null ? null : oldElem.ToString();
                            newVal = newElem.ValueKind == JsonValueKind.Null ? null : newElem.ToString();
                        }
                        else
                        {
                            newVal = prop.Value.ToString();
                        }
                    }
                    else if (record.Operation == EntityOperation.Insert)
                    {
                        newVal = prop.Value.ValueKind == JsonValueKind.Null ? null : prop.Value.ToString();
                    }
                    else if (record.Operation == EntityOperation.Delete)
                    {
                        oldVal = prop.Value.ValueKind == JsonValueKind.Null ? null : prop.Value.ToString();
                    }

                    result.Add(new EntityFieldHistoryRespDto
                    {
                        AuditRecordId = record.Id.ToString(),
                        EntityName = record.EntityName ?? entityName,
                        EntityId = record.EntityId ?? entityId,
                        PropertyName = propName,
                        OldValue = oldVal,
                        NewValue = newVal,
                        UserId = record.UserId ?? "Unknown",
                        UserEmail = userEmail,
                        UserType = record.UserType,
                        Operation = record.Operation ?? EntityOperation.Update,
                        Timestamp = record.Created,
                        Description = record.Description
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse ChangesJson for audit record {AuditId}", record.Id);
            }
        }

        return result;
    }
}
