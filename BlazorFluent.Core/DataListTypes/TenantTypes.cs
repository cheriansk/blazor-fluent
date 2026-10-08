using BlazorFluent.Core.Attributes;
using System.ComponentModel.DataAnnotations;

namespace BlazorFluent.Core.DataListTypes;

public enum UserType
{
    /// <summary>Client / external user. Strictly bound to a single tenant. Cannot switch.</summary>
    [Display(Name = "Client User", Description = "External client user bound to single tenant")]
    [DataListCategory(UserTypeDefinitions.Categories.External)]
    ClientUser = 1,

    /// <summary>Internal company staff. May be member of multiple tenants and switch freely.</summary>
    [Display(Name = "Company User", Description = "Internal company staff member")]
    [DataListCategory(UserTypeDefinitions.Categories.Internal)]
    [DataListFilterCriterias(UserTypeDefinitions.Filters.HostOnly, UserTypeDefinitions.Filters.SuperAdmin)]
    CompanyUser = 2
}

public static class UserTypeDefinitions
{
    public static class Categories
    {
        [Display(Name = "External Users", Description = "External tenant client users")]
        public const string External = "External";

        [Display(Name = "Internal Staff", Description = "Internal company staff members")]
        public const string Internal = "Internal";
    }

    public static class Filters
    {
        [Display(Name = "Host Context Only", Description = "Visible only when operating in host tenant context")]
        public const string HostOnly = "HostOnly";

        [Display(Name = "SuperAdmin Required", Description = "Requires SuperAdmin operator privileges")]
        public const string SuperAdmin = "SuperAdmin";
    }
}

public record TenantInfo(string Id, string Name, bool IsActive);
