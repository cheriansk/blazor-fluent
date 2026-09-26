
using BlazorFluent.Core.Domain.Delegates;

namespace BlazorFluent.Core.Domain.Catalog;
/// <summary>
/// THIS IS EXAMPLE FILE ONLY
/// </summary>
public class ProductEntity : AuditableEntity, ISoftDeletableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string Sku { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    // Soft delete properties
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    // Pure domain logic
    public void UpdatePrice(decimal newPrice)
    {
        if (newPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(newPrice), "Price cannot be negative.");
        }

        Price = newPrice;
    }
}
