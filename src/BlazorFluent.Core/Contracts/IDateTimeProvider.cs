namespace BlazorFluent.Core.Contracts;

public interface IDateTimeProvider
{
    /// <summary>
    /// Returns the current time according to the configured timezone (UTC or Local).
    /// </summary>
    DateTime Now { get; }

    /// <summary>
    /// Always returns the current time in UTC.
    /// </summary>
    DateTime UtcNow { get; }

    /// <summary>
    /// Always returns the current time in local system timezone.
    /// </summary>
    DateTime LocalNow { get; }

    /// <summary>
    /// Indicates whether UTC is preferred.
    /// </summary>
    bool UseUtc { get; }
}

public class ConfigurableDateTimeProvider : IDateTimeProvider
{
    public bool UseUtc { get; }

    public ConfigurableDateTimeProvider(bool useUtc = true)
    {
        UseUtc = useUtc;
    }

    public DateTime Now => UseUtc
        ? DateTime.UtcNow
        : DateTime.SpecifyKind(DateTime.Now.ToUniversalTime(), DateTimeKind.Utc);

    public DateTime UtcNow => DateTime.UtcNow;

    public DateTime LocalNow => DateTime.Now;
}

public class SystemDateTimeProvider : ConfigurableDateTimeProvider
{
    public SystemDateTimeProvider() : base(useUtc: true)
    {
    }
}
