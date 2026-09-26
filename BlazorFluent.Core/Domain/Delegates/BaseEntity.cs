namespace BlazorFluent.Core.Domain.Delegates;

public abstract class BaseEntity<TId>
{
    public TId Id { get; set; } = default!;
}

public abstract class BaseEntity : BaseEntity<Guid>
{
    protected BaseEntity()
    {
        Id = Guid.NewGuid();
    }
}
