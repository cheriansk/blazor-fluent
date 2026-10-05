using System.Reflection;
using BlazorFluent.Core.Contracts;
using BlazorFluent.Jobs.Abstractions;
using BlazorFluent.Jobs.FileImports.Abstractions;
using BlazorFluent.Jobs.Listeners;
using BlazorFluent.Jobs.Queue;
using BlazorFluent.Jobs.Schedulers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BlazorFluent.Jobs;

public static class JobsExtensions
{
    public static IServiceCollection AddBackgroundJobs(this IServiceCollection services, bool enableScheduler = true)
    {
        // 1. Register the in-memory high performance Channel Job Queue & Event Publisher
        services.TryAddSingleton<ChannelJobEventQueue>();
        services.TryAddSingleton<IJobEventQueue>(sp => sp.GetRequiredService<ChannelJobEventQueue>());
        services.TryAddSingleton<IJobEventPublisher>(sp => sp.GetRequiredService<ChannelJobEventQueue>());

        // 2. Register the Queue Listener background worker
        services.AddHostedService<BatchJobQueueListener>();

        // 3. Register the Periodic Scheduler if enabled
        if (enableScheduler)
        {
            services.AddHostedService<PeriodicBatchScheduler>();
        }

        // 4. Auto-discover and register all IBatchJobHandler<T> in this assembly
        RegisterBatchJobHandlers(services, typeof(JobsExtensions).Assembly);

        // 5. Register JobManagerService for dashboard queries and manual triggers
        services.TryAddScoped<IJobManagerService, Services.JobManagerService>();

        // 6. Auto-discover and register all IFileStager and IFileProcessor implementations
        RegisterImplementationsOf(services, typeof(JobsExtensions).Assembly, typeof(IFileStager));
        RegisterImplementationsOf(services, typeof(JobsExtensions).Assembly, typeof(IFileProcessor));

        return services;
    }

    private static void RegisterBatchJobHandlers(IServiceCollection services, Assembly assembly)
    {
        var handlerInterfaceType = typeof(IBatchJobHandler<>);

        var handlerTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterfaceType)
                .Select(i => new { ServiceType = i, ImplementationType = t }));

        foreach (var item in handlerTypes)
        {
            services.AddScoped(item.ServiceType, item.ImplementationType);
        }
    }

    private static void RegisterImplementationsOf(IServiceCollection services, Assembly assembly, Type serviceType)
    {
        var implementations = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && serviceType.IsAssignableFrom(t));

        foreach (var impl in implementations)
        {
            services.AddScoped(serviceType, impl);
        }
    }
}
