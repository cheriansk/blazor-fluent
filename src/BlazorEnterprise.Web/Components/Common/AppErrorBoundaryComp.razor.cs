using BlazorEnterprise.Shared.UI.Components;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Shared.Contracts.Engine.Client;
using BlazorEnterprise.Shared.DataListTypes;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace BlazorEnterprise.Web.Components.Common;

public partial class AppErrorBoundaryComp : Microsoft.AspNetCore.Components.ErrorBoundaryBase
{
    [Inject] private IAuditService AuditService { get; set; } = default!;
    [Inject] private ILogger<AppErrorBoundaryComp> Logger { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ITenantContext TenantContext { get; set; } = default!;
    [Inject] private ICurrentUser CurrentUser { get; set; } = default!;
    [Inject] private ICorrelationContext CorrelationContext { get; set; } = default!;

    private string? _errorReferenceId;

    protected override async Task OnErrorAsync(Exception exception)
    {
        var currentUri = NavigationManager.Uri;
        var tenantId = TenantContext.HasActiveTenant ? TenantContext.TenantId.ToString() : "none";
        var userId = CurrentUser.IsAuthenticated ? CurrentUser.UserId : "anonymous";
        
        // Use active CorrelationId as the universal Incident Reference ID for instant dev & prod log trace connectivity
        _errorReferenceId = !string.IsNullOrWhiteSpace(CorrelationContext.CorrelationId)
            ? CorrelationContext.CorrelationId
            : CorrelationContext.Renew();

        try
        {
            await AuditService.LogExceptionAsync(
                exception,
                $"Razor Page Rendering Error [Correlation: {_errorReferenceId}] at URL: {currentUri} (Tenant: {tenantId}, User: {userId})",
                AuditSeverityEnum.Critical);
        }
        catch (Exception logEx)
        {
            Logger.LogCritical(logEx, "Failed to log UI exception to AuditService for CorrelationId '{CorrelationId}'. Original message: {Message}", _errorReferenceId, exception.Message);
        }

        Logger.LogCritical(
            exception,
            "Razor Page Rendering Error: Incident='{IncidentRef}', URL='{CurrentUri}', Tenant='{TenantId}', User='{UserId}', ExceptionType='{ExceptionType}': {ErrorMessage}",
            _errorReferenceId,
            currentUri,
            tenantId,
            userId,
            exception.GetType().FullName,
            exception.Message);
    }

    private void RecoverAndReload()
    {
        _errorReferenceId = null;
        Recover();
    }
}
