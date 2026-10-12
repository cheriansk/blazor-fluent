using System.ComponentModel.DataAnnotations.Schema;

namespace BlazorFluent.Core.Domain.Tasks;

/// <summary>
/// Backward-compatible inheritance subclass for UserTaskEntity mapping to TaskEntity.
/// </summary>
[NotMapped]
public class UserTaskEntity : TaskEntity
{
}
