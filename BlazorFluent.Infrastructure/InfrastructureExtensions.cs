using BlazorFluent.Core.Contracts;
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

        // 2. Blazor Circuit Observability Lifecycle Handler
        services.AddScoped<CircuitHandler, BlazorCircuitObservabilityHandler>();

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

        return services;
    }
}
