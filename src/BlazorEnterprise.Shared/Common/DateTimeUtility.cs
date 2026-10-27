namespace BlazorEnterprise.Shared.Common;

/// <summary>
/// Universal UTC normalization and date manipulation utilities.
/// </summary>
public static class DateTimeUtility
{
    public static DateTime NormalizeUtc(DateTime dateTime)
    {
        return dateTime.Kind switch
        {
            DateTimeKind.Utc => dateTime,
            DateTimeKind.Local => dateTime.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
        };
    }

    public static DateTime? NormalizeUtc(DateTime? dateTime) =>
        dateTime.HasValue ? NormalizeUtc(dateTime.Value) : null;

    public static string ToIso8601String(DateTime dateTime) =>
        NormalizeUtc(dateTime).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
}
