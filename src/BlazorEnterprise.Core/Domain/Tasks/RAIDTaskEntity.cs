using System.ComponentModel.DataAnnotations.Schema;
using BlazorEnterprise.Core.Domain.Base;

namespace BlazorEnterprise.Core.Domain.Tasks;

/// <summary>
/// Abstract base class for all RAID log entities:
/// RiskEntity ("R"), AssumptionEntity ("A"), IssueEntity ("I"), and WorkDependencyEntity ("D").
/// Implements IMilestoneLinkedEntity and IUnversionedEntity.
/// </summary>
public abstract class RAIDTaskEntity : BaseTaskEntity, IMilestoneLinkedEntity
{
    /// <summary>
    /// Optional foreign key to a project or program milestone (hydrated via MilestoneRelationEntity).
    /// </summary>
    [NotMapped]
    public Guid? MilestoneId { get; set; }
}
