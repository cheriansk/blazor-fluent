using BlazorEnterprise.Shared.UI.Components;
using BlazorEnterprise.Core.Contracts;
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

    private string? _errorReferenceId;

    protected override async Task OnErrorAsync(Exception exception)
    {
        var currentUri = NavigationManager.Uri;
        var tenantId = TenantContext.HasActiveTenant ? TenantContext.TenantId.ToString() : "none";
        var userId = CurrentUser.IsAuthenticated ? CurrentUser.UserId : "anonymous";

        try
        {
            _errorReferenceId = await AuditService.LogExceptionAsync(
                exception,
                $"Razor Page Rendering Error at URL: {currentUri} (Tenant: {tenantId}, User: {userId})",
                AuditSeverityEnum.Critical);
        }
        catch (Exception logEx)
        {
            Logger.LogCritical(logEx, "Failed to log UI exception to AuditService. Original message: {Message}", exception.Message);
            _errorReferenceId = $"ERR-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
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
