using Microsoft.AspNetCore.Components;

namespace BlazorFluent.Components.Common;

public abstract class PersistentStateComponentBase : ComponentBase, IDisposable
{
    [Inject] protected PersistentComponentState ApplicationState { get; set; } = default!;

    private PersistingComponentStateSubscription _subscription;

    protected override void OnInitialized()
    {
        _subscription = ApplicationState.RegisterOnPersisting(PersistStateAsync);
        RestoreState();
    }

    protected abstract Task PersistStateAsync();
    protected abstract void RestoreState();

    public void Dispose()
    {
        _subscription.Dispose();
    }
}
