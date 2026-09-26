namespace BlazorFluent.Core.Domain.Delegates;

public interface IAuditableEntity
{
    DateTime Created { get; set; }
    string CreatedBy { get; set; }
    DateTime? Updated { get; set; }
    string? UpdatedBy { get; set; }
}
