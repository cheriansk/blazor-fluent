using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.DataListTypes;
using Microsoft.EntityFrameworkCore;

namespace BlazorFluent.Persistence.Extensions;

/// <summary>
/// Security extensions for EF Core queries that require bypassing multi-tenant or global query filters.
/// Enforces mandatory business justification and generates an immutable forensic audit log entry.
/// </summary>
public static class AuditedQueryableExtensions
{
    public static async Task<List<T>> ToListWithAuditedBypassAsync<T>(
        this IQueryable<T> query,
        IAuditService auditService,
        string justification,
        CancellationToken cancellationToken = default) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(justification);
        ArgumentNullException.ThrowIfNull(auditService);

        await auditService.LogSecurityEventAsync(
            "QueryFilterBypass",
            AuditSeverity.Warning,
            $"Query filters bypassed for entity '{typeof(T).Name}'. Justification: {justification}",
            cancellationToken);

        return await query.IgnoreQueryFilters().ToListAsync(cancellationToken);
    }

    public static async Task<T?> FirstOrDefaultWithAuditedBypassAsync<T>(
        this IQueryable<T> query,
        IAuditService auditService,
        string justification,
        CancellationToken cancellationToken = default) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(justification);
        ArgumentNullException.ThrowIfNull(auditService);

        await auditService.LogSecurityEventAsync(
            "QueryFilterBypass",
            AuditSeverity.Warning,
            $"Query filters bypassed for entity '{typeof(T).Name}'. Justification: {justification}",
            cancellationToken);

        return await query.IgnoreQueryFilters().FirstOrDefaultAsync(cancellationToken);
    }

    public static async Task<bool> AnyWithAuditedBypassAsync<T>(
        this IQueryable<T> query,
        IAuditService auditService,
        string justification,
        CancellationToken cancellationToken = default) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(justification);
        ArgumentNullException.ThrowIfNull(auditService);

        await auditService.LogSecurityEventAsync(
            "QueryFilterBypass",
            AuditSeverity.Warning,
            $"Query filters bypassed for entity '{typeof(T).Name}'. Justification: {justification}",
            cancellationToken);

        return await query.IgnoreQueryFilters().AnyAsync(cancellationToken);
    }

    public static async Task<int> CountWithAuditedBypassAsync<T>(
        this IQueryable<T> query,
        IAuditService auditService,
        string justification,
        CancellationToken cancellationToken = default) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(justification);
        ArgumentNullException.ThrowIfNull(auditService);

        await auditService.LogSecurityEventAsync(
            "QueryFilterBypass",
            AuditSeverity.Warning,
            $"Query filters bypassed for entity '{typeof(T).Name}'. Justification: {justification}",
            cancellationToken);

        return await query.IgnoreQueryFilters().CountAsync(cancellationToken);
    }
}
