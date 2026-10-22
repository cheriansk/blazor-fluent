using BlazorFluent.Shared.Attributes;
using System.ComponentModel.DataAnnotations;

namespace BlazorFluent.Shared.DataListTypes;

/// <summary>
/// Architectural classification of an identity in identity.users.
/// Distinguishes interactive human users from tenant-scoped service daemons and API bots.
/// </summary>
[ClassificationEnum]
public enum UserKindEnum
{
    [Display(Name = "Human User", Description = "Interactive human user authenticated via credentials or SSO")]
    Human = 1,

    [Display(Name = "System Daemon", Description = "Internal non-interactive automated service account scoped to a tenant")]
    SystemDaemon = 2,

    [Display(Name = "API Integration Bot", Description = "External service-to-service bot authenticating via machine tokens")]
    ApiIntegrationBot = 3
}
