using System;
using System.Security;
using BlazorEnterprise.Core.Contracts;
using Microsoft.Extensions.Logging;

namespace BlazorEnterprise.Persistence.Services;

/// <summary>
/// Abstract base service class providing zero-trust tenant resolution, context assertions,
/// and standardized audit logging for tenant-scoped persistence services.
/// </summary>
public abstract class TenantScopedServiceBase
{
    protected readonly ITenantContext TenantContext;
    protected readonly ICurrentUser CurrentUser;
    protected readonly ILogger Logger;

    protected TenantScopedServiceBase(ITenantContext tenantContext, ICurrentUser currentUser, ILogger logger)
    {
        TenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        CurrentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Resolves the current tenant identifier or throws an exception under Zero-Trust policies.
    /// </summary>
    protected Guid TenantId => TenantContext.HasActiveTenant && TenantContext.TenantId != Guid.Empty
        ? TenantContext.TenantId
        : throw new InvalidOperationException("Zero-Trust Security Violation: Active tenant context required.");

    /// <summary>
    /// Asserts that an active tenant context exists prior to executing a sensitive operation.
    /// </summary>
    /// <param name="operation">The operation name being audited/verified.</param>
    protected void RequireActiveTenant(string operation)
    {
        if (!TenantContext.HasActiveTenant || TenantContext.TenantId == Guid.Empty)
        {
            Logger.LogWarning("Zero-trust security violation: Operation {Operation} invoked without active tenant context.", operation);
            throw new SecurityException($"Zero-Trust Security Violation: Operation '{operation}' requires an active tenant context.");
        }
    }
}
