using System.Collections.Concurrent;
using System.Text.Json;
using BlazorEnterprise.Core.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;

namespace BlazorEnterprise.Persistence.Services;

/// <summary>
/// Central save wrapper providing safe, robust optimistic concurrency handling,
/// atomic All-or-None rollback, visual field diff calculation, and friendly
/// database constraint mapping for PostgreSQL EF Core operations.
/// </summary>
public class SafeSaveService : ISafeSaveService
{
    private static readonly HashSet<string> IgnoredProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "Version",
        "xmin",
        "Created",
        "CreatedBy",
        "Updated",
        "UpdatedBy",
        "DeletedAtUtc",
        "DeletedBy",
        "Id",
        "TenantId"
    };

    private readonly ILogger<SafeSaveService> _logger;

    public SafeSaveService(ILogger<SafeSaveService> logger)
    {
        _logger = logger;
    }

    public async Task<SaveResult> SaveAsync(DbContext db, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        try
        {
            await db.SaveChangesAsync(ct);
            return new SaveResult(SaveStatus.Ok);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Optimistic concurrency conflict detected during SaveChangesAsync.");

            var entry = ex.Entries.FirstOrDefault();
            if (entry is null)
            {
                // Guarantee All-or-None: Clear change tracker so no dirty state remains
                db.ChangeTracker.Clear();
                return new SaveResult(SaveStatus.Conflict, "A concurrency conflict occurred, but entity details could not be retrieved.");
            }

            var dbValues = await entry.GetDatabaseValuesAsync(ct);

            // Compute side-by-side property diffs while entry values are still intact
            var diffs = dbValues is not null ? ComputeDiffs(entry, dbValues) : null;

            // Guarantee All-or-None: Discard all pending mutations in the context so nothing is partially saved
            db.ChangeTracker.Clear();

            return dbValues is null
                ? new SaveResult(SaveStatus.Deleted, "This record was deleted by another user or process.", entry)
                : new SaveResult(
                    SaveStatus.Conflict,
                    "Another user modified this record while you were editing. To prevent data loss, your changes were not saved.",
                    entry,
                    dbValues,
                    diffs);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException pg)
        {
            _logger.LogWarning(ex, "PostgreSQL constraint violation detected: SqlState {SqlState}, Message {Message}", pg.SqlState, pg.MessageText);

            // Guarantee All-or-None on constraint failure
            db.ChangeTracker.Clear();

            return new SaveResult(SaveStatus.ConstraintViolation, MapConstraintMessage(pg));
        }
    }

    private static IReadOnlyList<PropertyDiffItem> ComputeDiffs(EntityEntry entry, PropertyValues dbValues)
    {
        var diffList = new List<PropertyDiffItem>();
        var currentValues = entry.CurrentValues;

        foreach (var property in currentValues.Properties)
        {
            if (IgnoredProperties.Contains(property.Name))
            {
                continue;
            }

            var yourValRaw = currentValues[property];
            var dbValRaw = dbValues[property];

            var yourValStr = FormatValue(yourValRaw);
            var dbValStr = FormatValue(dbValRaw);

            var hasConflict = !string.Equals(yourValStr, dbValStr, StringComparison.Ordinal);

            // Add all non-ignored modified properties or properties with conflicting database values
            if (hasConflict || entry.Property(property.Name).IsModified)
            {
                diffList.Add(new PropertyDiffItem(
                    PropertyName: property.Name,
                    YourValue: yourValStr,
                    DatabaseValue: dbValStr,
                    HasConflict: hasConflict));
            }
        }

        return diffList;
    }

    private static string FormatValue(object? val)
    {
        if (val is null) return string.Empty;
        if (val is DateTime dt) return dt.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
        return val.ToString() ?? string.Empty;
    }

    private static string MapConstraintMessage(Npgsql.PostgresException pg)
    {
        return pg.SqlState switch
        {
            // 23505: unique_violation
            "23505" => !string.IsNullOrWhiteSpace(pg.ConstraintName)
                ? $"A record with matching unique details already exists (Constraint: '{pg.ConstraintName}')."
                : "A record with matching unique details already exists.",

            // 23503: foreign_key_violation
            "23503" => !string.IsNullOrWhiteSpace(pg.ConstraintName)
                ? $"Operation cannot be completed because a referenced record does not exist or is currently in use (Constraint: '{pg.ConstraintName}')."
                : "A required referenced record does not exist or is in use.",

            // 23514: check_violation
            "23514" => !string.IsNullOrWhiteSpace(pg.ConstraintName)
                ? $"The provided data violates a business rule constraint (Constraint: '{pg.ConstraintName}')."
                : "The provided data violates a system validation rule.",

            _ => $"A database rule violation occurred: {pg.MessageText}"
        };
    }
}
