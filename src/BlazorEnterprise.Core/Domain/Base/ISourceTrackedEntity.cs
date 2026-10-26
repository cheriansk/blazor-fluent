namespace BlazorEnterprise.Core.Domain.Base;

/// <summary>
/// Contract for domain entities that track end-to-end data lineage back to a raw canonical
/// staged import record (e.g. inside <c>staging.StagedImports</c>).
/// <para>
/// <b>WHEN TO USE:</b> Apply to any domain entity that can be created or populated via batch file
/// imports (CSV, Excel, XML, JSON) and needs an auditable forensic pointer to its raw ingested row.
/// </para>
/// </summary>
public interface ISourceTrackedEntity
{
    /// <summary>
    /// Deterministic UUID v7 linking this domain entity back to the exact source row inside staging.StagedImports.PayloadJson.
    /// </summary>
    Guid? SourceRowId { get; set; }
}
