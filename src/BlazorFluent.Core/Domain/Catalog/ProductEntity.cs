
using BlazorFluent.Core.Domain.Base;

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
    public required string Name { get; set; }

    public required string Sku { get; set; }

    public string? Description { get; set; }

    public required decimal Price { get; set; }

    public int StockQuantity { get; set; }

    // --- ISoftDeletableEntity ---
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}
