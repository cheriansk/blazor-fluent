using System.ComponentModel.DataAnnotations;
using BlazorEnterprise.Shared.Attributes;

namespace BlazorEnterprise.Shared.DataListTypes;

/// <summary>
/// Operational resource roles for project team members.
/// </summary>
[ClassificationEnum]
public enum ProjectResourceRoleEnum
{
    [Display(Name = "Project Manager", Description = "Lead coordinator and project manager")]
    ProjectManager = 1,

    [Display(Name = "Developer", Description = "Software developer / engineer")]
    Developer = 2,

    [Display(Name = "QA Engineer", Description = "Quality assurance and testing engineer")]
    QA = 3,

    [Display(Name = "Designer", Description = "UI/UX designer")]
    Designer = 4,

    [Display(Name = "Business Analyst", Description = "Requirements and domain analyst")]
    BusinessAnalyst = 5,

    [Display(Name = "Stakeholder", Description = "Business or project stakeholder")]
    Stakeholder = 6,

    [Display(Name = "Other", Description = "Other specialized project contributor")]
    Other = 7
}
