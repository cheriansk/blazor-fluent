using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace BlazorEnterprise.Persistence.Services;

public enum SaveStatus
{
    Ok,
    Conflict,
    Deleted,
    ConstraintViolation
}

public sealed record PropertyDiffItem(
    string PropertyName,
    string? YourValue,
    string? DatabaseValue,
    bool HasConflict);

public sealed record SaveResult(
    SaveStatus Status,
    string? Message = null,
    EntityEntry? Entry = null,
    PropertyValues? DatabaseValues = null,
    IReadOnlyList<PropertyDiffItem>? Diffs = null);

public interface ISafeSaveService
{
    Task<SaveResult> SaveAsync(DbContext db, CancellationToken ct = default);
}
