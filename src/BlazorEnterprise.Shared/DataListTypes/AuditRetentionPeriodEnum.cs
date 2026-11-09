using System.ComponentModel.DataAnnotations;
using BlazorEnterprise.Shared.Attributes;

namespace BlazorEnterprise.Shared.DataListTypes;

[ClassificationEnum]
public enum AuditRetentionPeriodEnum
{
    [Display(Name = "90 Days (3 Months)", Description = "Standard operational retention for entity diffs and diagnostics")]
    NinetyDays = 90,

    [Display(Name = "180 Days (6 Months)", Description = "Extended operational retention")]
    OneHundredEightyDays = 180,

    [Display(Name = "365 Days (1 Year)", Description = "Standard compliance retention for security and access audits")]
    ThreeHundredSixtyFiveDays = 365,

    [Display(Name = "540 Days (18 Months)", Description = "Extended regulatory compliance retention")]
    FiveHundredFortyDays = 540,

    [Display(Name = "730 Days (2 Years)", Description = "Maximum long-term forensic retention")]
    SevenHundredThirtyDays = 730
}
