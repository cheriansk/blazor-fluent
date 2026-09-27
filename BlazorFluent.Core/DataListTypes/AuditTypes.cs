namespace BlazorFluent.Core.DataListTypes;

public enum AuditEventType
{
    EntityChange = 1,
    Security = 2,
    TenantSwitch = 3,
    UserActivity = 4,
    Exception = 5
}

public enum AuditSeverity
{
    Information = 1,
    Warning = 2,
    Error = 3,
    Critical = 4
}

public enum EntityOperation
{
    Insert = 1,
    Update = 2,
    Delete = 3
}
