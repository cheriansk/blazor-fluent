using System;
using System.Threading;
using System.Threading.Tasks;

namespace BlazorFluent.Core.Contracts;

/// <summary>
/// Domain contract for managing user preferences and UI presentation settings.
/// Decouples UI session services from direct persistence DbContext access.
/// </summary>
public interface IUserPreferencesService
{
    /// <summary>
    /// Retrieves the stored timezone identifier for the specified user.
    /// </summary>
    Task<string?> GetUserTimeZoneAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates and persists the preferred timezone identifier for the specified user.
    /// </summary>
    Task SetUserTimeZoneAsync(Guid userId, string timeZoneId, CancellationToken cancellationToken = default);
}
