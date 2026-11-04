using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using BlazorEnterprise.Core.Contracts;
using BlazorEnterprise.Persistence.Context;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorEnterprise.Persistence.Interceptors;

public sealed class CircuitActivityDbInterceptor : DbCommandInterceptor
{
    private static ICircuitActivityTracker? ResolveTracker(CommandEventData eventData)
    {
        if (eventData.Context is AppDbContext appDb && appDb.ServiceProvider is not null)
        {
            return appDb.ServiceProvider.GetService<ICircuitActivityTracker>();
        }
        return null;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        var tracker = ResolveTracker(eventData);
        tracker?.BeginOperation(isWrite: false);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var tracker = ResolveTracker(eventData);
        tracker?.BeginOperation(isWrite: true);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }
}
