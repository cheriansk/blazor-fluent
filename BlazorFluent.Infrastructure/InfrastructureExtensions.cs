using BlazorFluent.Core.Contracts;
using BlazorFluent.Core.Security;
using BlazorFluent.Core.Storage;
using BlazorFluent.Infrastructure.Notifications;
using BlazorFluent.Infrastructure.Observability;
using BlazorFluent.Infrastructure.Security;
using BlazorFluent.Infrastructure.Storage;
using BlazorFluent.Infrastructure.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BlazorFluent.Infrastructure;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Outbound External Notification Senders (HTTP Webhooks & SMTP Email)
        services.AddHttpClient();
        services.TryAddScoped<ITeamsNotificationSender, TeamsNotificationSender>();
        services.TryAddScoped<IEmailNotificationSender, EmailNotificationSender>();

        // 2. Blazor Circuit Observability Lifecycle Handler & Session Circuit Tracker
        services.AddSingleton<ICircuitSessionTracker, CircuitSessionTracker>();
        services.AddScoped<CircuitHandler, BlazorCircuitObservabilityHandler>();
        services.AddScoped<CircuitHandler, UserSessionCircuitHandler>();

        // 3. Path-Aware Authorization Middleware Result Handler
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, PathAwareAuthorizationHandler>();

        // 4. Input Sanitization Service (XSS Protection)
        services.TryAddSingleton<IInputSanitizer, HtmlInputSanitizer>();

        // 5. Resource-Based Authorization Handler
        services.AddScoped<IAuthorizationHandler, ProjectResourceAuthorizationHandler>();

        // 6. Encrypted ProtectedLocalStorage Form Draft Auto-Save Service
        services.TryAddScoped<ILocalStorageFormService, ProtectedLocalStorageFormService>();

        // 7. Responsive Viewport Layout Breakpoint Service
        services.TryAddScoped<ILayoutBreakpointService, LayoutBreakpointService>();

        // 8. Bounded System.Threading.Channels Notification Queue & Background Worker
        services.AddSingleton<NotificationChannelQueue>();
        services.AddHostedService<NotificationQueueWorker>();

        // 9. Inbound Webhook HMAC-SHA256 Signature Validator
        services.TryAddSingleton<IWebhookSignatureValidator, HmacWebhookSignatureValidator>();

        // 10. Tenant-Partitioned AES-256-GCM Encryption Service
        services.TryAddSingleton<ITenantEncryptionService, TenantAesGcmEncryptionService>();

        // 11. Tenant Blob Storage Service (Azure Blob Storage with local filesystem fallback)
        services.TryAddSingleton<ITenantBlobStorageService, AzureAndLocalBlobStorageService>();

        // 12. Settings In-Memory Navigation State
        services.TryAddScoped<ISettingsNavigationState, SettingsNavigationState>();

        return services;
    }
}
