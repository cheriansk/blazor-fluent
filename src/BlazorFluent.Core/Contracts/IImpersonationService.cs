using BlazorFluent.Core.Domain.Identity;
using BlazorFluent.Core.Dtos.Response;

namespace BlazorFluent.Core.Contracts;

public interface IImpersonationService
{
    /// <summary>
    /// Initiates a time-bound operator impersonation grant for a target user.
    /// Strictly restricted to SuperAdmins / Host Administrators.
    /// </summary>
    Task<Result<ImpersonationGrantEntity>> StartImpersonationAsync(
        string targetUserId,
        string reason,
        int durationMinutes = 60,
        CancellationToken ct = default);

    /// <summary>
    /// Terminates the active impersonation session and reverts identity back to the SuperAdmin.
    /// </summary>
    Task<Result> StopImpersonationAsync(CancellationToken ct = default);

    /// <summary>
    /// Retrieves the current active impersonation grant for the caller, if any.
    /// </summary>
    Task<ImpersonationGrantEntity?> GetActiveGrantAsync(CancellationToken ct = default);
}
