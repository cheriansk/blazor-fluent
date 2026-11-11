using BlazorEnterprise.Persistence.Context;
using BlazorEnterprise.Persistence.Services;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace BlazorEnterprise.Persistence.Services;

public class AmbientAwareDbContextFactory : IDbContextFactory<AppDbContext>
{
    private readonly IDbContextFactory<AppDbContext> _innerFactory;

    public AmbientAwareDbContextFactory(IDbContextFactory<AppDbContext> innerFactory)
    {
        _innerFactory = innerFactory;
    }

    public AppDbContext CreateDbContext()
    {
        if (AmbientTransactionContext.Current?.DbContext is { } ambientDb)
        {
            return ambientDb;
        }
        return _innerFactory.CreateDbContext();
    }

    public async Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        if (AmbientTransactionContext.Current?.DbContext is { } ambientDb)
        {
            return ambientDb;
        }
        return await _innerFactory.CreateDbContextAsync(cancellationToken);
    }

    // Expose method to create an independent context for root scope
    public async Task<AppDbContext> CreateIndependentDbContextAsync(CancellationToken cancellationToken = default)
    {
        return await _innerFactory.CreateDbContextAsync(cancellationToken);
    }
}
