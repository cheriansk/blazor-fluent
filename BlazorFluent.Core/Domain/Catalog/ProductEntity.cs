
using BlazorFluent.Core.Domain.Delegates;

namespace BlazorFluent.Core.Domain.Catalog;
/// <summary>
/// THIS IS EXAMPLE FILE ONLY
/// </summary>
/// <summary>
/// A catalog product — scoped to a tenant via <see cref="TenantAuditableEntity"/>.
/// Pure POCO; all EF Core mapping is in BlazorFluent.Persistence/Configurations.
/// </summary>
public class ProductEntity : TenantAuditableEntity, ISoftDeletableEntity
{
    public string Name { get; set; } = string.Empty;

    public string Sku { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int StockQuantity { get; set; }

    // --- ISoftDeletableEntity ---
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}
