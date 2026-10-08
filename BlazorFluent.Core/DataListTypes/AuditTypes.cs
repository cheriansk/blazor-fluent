using BlazorFluent.Core.Attributes;
using System.ComponentModel.DataAnnotations;

namespace BlazorFluent.Core.DataListTypes;

public enum AuditEventType
{
    [Display(Name = "Entity Change", Description = "Database entity insertion, update, or deletion")]
    [DataListCategory(AuditDefinitions.Categories.Data)]
    EntityChange = 1,

    [Display(Name = "Security Event", Description = "Authentication, role changes, or access violations")]
    [DataListCategory(AuditDefinitions.Categories.Security)]
    Security = 2,

    [Display(Name = "Tenant Switch", Description = "Company staff switched active tenant context")]
    [DataListCategory(AuditDefinitions.Categories.Tenancy)]
    TenantSwitch = 3,

    [Display(Name = "User Activity", Description = "User profile or account actions")]
    [DataListCategory(AuditDefinitions.Categories.User)]
    UserActivity = 4,

    [Display(Name = "System Exception", Description = "Handled or unhandled system error incident")]
    [DataListCategory(AuditDefinitions.Categories.Diagnostics)]
    Exception = 5
}

public enum AuditSeverity
{
    [Display(Name = "Information", Description = "Normal operational event")]
    [DataListCategory(AuditDefinitions.Categories.Severity)]
    Information = 1,

    [Display(Name = "Warning", Description = "Potential anomaly or warning event")]
    [DataListCategory(AuditDefinitions.Categories.Severity)]
    Warning = 2,

    [Display(Name = "Error", Description = "System error or operation failure")]
    [DataListCategory(AuditDefinitions.Categories.Severity)]
    Error = 3,

    [Display(Name = "Critical", Description = "Critical security breach or catastrophic outage")]
    [DataListCategory(AuditDefinitions.Categories.Severity)]
    Critical = 4
}

public enum EntityOperation
{
    [Display(Name = "Insert", Description = "New record creation")]
    [DataListCategory(AuditDefinitions.Categories.Crud)]
    Insert = 1,

    [Display(Name = "Update", Description = "Existing record modification")]
    [DataListCategory(AuditDefinitions.Categories.Crud)]
    Update = 2,

    [Display(Name = "Delete", Description = "Record soft or physical deletion")]
    [DataListCategory(AuditDefinitions.Categories.Crud)]
    Delete = 3
}

public static class AuditDefinitions
{
    public static class Categories
    {
        [Display(Name = "Data Audit", Description = "Entity property and state mutations")]
        public const string Data = "Data";

        [Display(Name = "Security & Governance", Description = "Authentication and permission events")]
        public const string Security = "Security";

        [Display(Name = "Tenant Lifecycle", Description = "Multi-tenant context switches")]
        public const string Tenancy = "Tenancy";

        [Display(Name = "User Lifecycle", Description = "User session and profile actions")]
        public const string User = "User";

        [Display(Name = "System Diagnostics", Description = "Exceptions, errors, and health events")]
        public const string Diagnostics = "Diagnostics";

        [Display(Name = "Alert Severity", Description = "Audit log severity levels")]
        public const string Severity = "Severity";

        [Display(Name = "Database Operations", Description = "CRUD operations on entities")]
        public const string Crud = "CRUD";
    }
}
