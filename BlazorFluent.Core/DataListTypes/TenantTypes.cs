namespace BlazorFluent.Core.DataListTypes;

public enum UserType
{
    /// <summary>Client / external user. Strictly bound to a single tenant. Cannot switch.</summary>
    ClientUser = 1,

    /// <summary>Internal company staff. May be member of multiple tenants and switch freely.</summary>
    CompanyUser = 2
}

public record TenantInfo(string Id, string Name, bool IsActive);
