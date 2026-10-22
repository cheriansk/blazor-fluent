using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Shared.DataListTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

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
            AuditSeverityEnum.Warning,
            $"Query filters bypassed for entity '{typeof(T).Name}'. Justification: {justification}",
            cancellationToken);

        return await query.IgnoreQueryFilters().ToListAsync(cancellationToken);
    }

    public static async Task<List<TResult>> ToListWithAuditedBypassAsync<T, TResult>(
        this IQueryable<T> query,
        IAuditService auditService,
        string justification,
        Expression<Func<T, TResult>> selector,
        CancellationToken cancellationToken = default) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(justification);
        ArgumentNullException.ThrowIfNull(auditService);
        ArgumentNullException.ThrowIfNull(selector);

        await auditService.LogSecurityEventAsync(
            "QueryFilterBypass",
            AuditSeverityEnum.Warning,
            $"Query filters bypassed for entity '{typeof(T).Name}'. Justification: {justification}",
            cancellationToken);

        return await query.IgnoreQueryFilters().Select(selector).ToListAsync(cancellationToken);
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
            AuditSeverityEnum.Warning,
            $"Query filters bypassed for entity '{typeof(T).Name}'. Justification: {justification}",
            cancellationToken);

        return await query.IgnoreQueryFilters().FirstOrDefaultAsync(cancellationToken);
    }

    public static async Task<TResult?> FirstOrDefaultWithAuditedBypassAsync<T, TResult>(
        this IQueryable<T> query,
        IAuditService auditService,
        string justification,
        Expression<Func<T, TResult>> selector,
        CancellationToken cancellationToken = default) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(justification);
        ArgumentNullException.ThrowIfNull(auditService);
        ArgumentNullException.ThrowIfNull(selector);

        await auditService.LogSecurityEventAsync(
            "QueryFilterBypass",
            AuditSeverityEnum.Warning,
            $"Query filters bypassed for entity '{typeof(T).Name}'. Justification: {justification}",
            cancellationToken);

        return await query.IgnoreQueryFilters().Select(selector).FirstOrDefaultAsync(cancellationToken);
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
            AuditSeverityEnum.Warning,
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
            AuditSeverityEnum.Warning,
            $"Query filters bypassed for entity '{typeof(T).Name}'. Justification: {justification}",
            cancellationToken);

        return await query.IgnoreQueryFilters().CountAsync(cancellationToken);
    }

    public static async Task<int> ExecuteUpdateWithAuditedBypassAsync<T>(
        this IQueryable<T> query,
        IAuditService auditService,
        string justification,
        Action<UpdateSettersBuilder<T>> setPropertyCalls,
        CancellationToken cancellationToken = default) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(justification);
        ArgumentNullException.ThrowIfNull(auditService);
        ArgumentNullException.ThrowIfNull(setPropertyCalls);

        await auditService.LogSecurityEventAsync(
            "QueryFilterBypass",
            AuditSeverityEnum.Warning,
            $"Query filters bypassed for bulk update on '{typeof(T).Name}'. Justification: {justification}",
            cancellationToken);

        return await query.IgnoreQueryFilters().ExecuteUpdateAsync(setPropertyCalls, cancellationToken);
    }

    public static async Task<int> ExecuteDeleteWithAuditedBypassAsync<T>(
        this IQueryable<T> query,
        IAuditService auditService,
        string justification,
        CancellationToken cancellationToken = default) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(justification);
        ArgumentNullException.ThrowIfNull(auditService);

        await auditService.LogSecurityEventAsync(
            "QueryFilterBypass",
            AuditSeverityEnum.Warning,
            $"Query filters bypassed for bulk delete on '{typeof(T).Name}'. Justification: {justification}",
            cancellationToken);

        return await query.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
    }
}
