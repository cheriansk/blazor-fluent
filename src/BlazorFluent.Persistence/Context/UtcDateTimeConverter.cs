using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BlazorFluent.Persistence.Context;

/// <summary>
/// EF Core ValueConverter that ensures non-nullable DateTime values are always converted
/// to UTC with DateTimeKind.Utc before being written to PostgreSQL timestamp columns.
/// Handles Local, Utc, and Unspecified kinds cleanly.
/// Also ensures read values are returned as DateTimeKind.Utc.
/// </summary>
public class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter() : base(
        d => d.Kind == DateTimeKind.Utc 
            ? d 
            : (d.Kind == DateTimeKind.Local ? d.ToUniversalTime() : DateTime.SpecifyKind(d, DateTimeKind.Utc)),
        d => d.Kind == DateTimeKind.Local 
            ? d.ToUniversalTime() 
            : (d.Kind == DateTimeKind.Utc ? d : DateTime.SpecifyKind(d, DateTimeKind.Utc)))
    {
    }
}

/// <summary>
/// EF Core ValueConverter that ensures nullable DateTime? values are always converted
/// to UTC with DateTimeKind.Utc before being written to PostgreSQL timestamp columns.
/// </summary>
public class NullableUtcDateTimeConverter : ValueConverter<DateTime?, DateTime?>
{
    public NullableUtcDateTimeConverter() : base(
        d => d.HasValue 
            ? (d.Value.Kind == DateTimeKind.Utc 
                ? d.Value 
                : (d.Value.Kind == DateTimeKind.Local ? d.Value.ToUniversalTime() : DateTime.SpecifyKind(d.Value, DateTimeKind.Utc))) 
            : d,
        d => d.HasValue 
            ? (d.Value.Kind == DateTimeKind.Local 
                ? d.Value.ToUniversalTime() 
                : (d.Value.Kind == DateTimeKind.Utc ? d.Value : DateTime.SpecifyKind(d.Value, DateTimeKind.Utc))) 
            : d)
    {
    }
}
