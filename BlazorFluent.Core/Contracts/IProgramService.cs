using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Domain.Tenancy;
using BlazorFluent.Core.Dtos.Response;

namespace BlazorFluent.Core.Contracts;

/// <summary>
/// Service contract for managing enterprise programs within a tenant.
/// Enforces Zero Trust and role-based permissions (Tenant Admin, Program Admin).
/// </summary>
public interface IProgramService
{
    Task<IReadOnlyList<ProgramEntity>> GetProgramsAsync(string tenantSlug, CancellationToken ct = default);

    Task<Result<ProgramEntity>> GetProgramByIdAsync(Guid programId, CancellationToken ct = default);

    Task<Result<ProgramEntity>> CreateProgramAsync(
        string tenantSlug,
        string name,
        string code,
        string description,
        DateTime? startDateUtc,
        DateTime? targetEndDateUtc,
        ProgramStatus status = ProgramStatus.Active,
        CancellationToken ct = default);

    Task<Result<ProgramEntity>> UpdateProgramAsync(
        Guid programId,
        string name,
        string code,
        string description,
        DateTime? startDateUtc,
        DateTime? targetEndDateUtc,
        ProgramStatus status,
        bool isActive,
        CancellationToken ct = default);

    Task<IReadOnlyList<ProjectEntity>> GetProgramProjectsAsync(Guid programId, CancellationToken ct = default);

    Task<ProgramHealthSummaryDto> GetProgramHealthAsync(Guid programId, CancellationToken ct = default);
}
