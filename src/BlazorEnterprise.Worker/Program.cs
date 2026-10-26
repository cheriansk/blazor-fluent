using BlazorEnterprise.Core.Configuration;
using BlazorEnterprise.Infrastructure;
using BlazorEnterprise.Infrastructure.EngineGateway;
using BlazorEnterprise.Jobs;
using BlazorEnterprise.Persistence;
using BlazorEnterprise.ServiceDefaults;
using Serilog;
using Serilog.Enrichers.Span;

// 0. Npgsql PostgreSQL Timestamp Compatibility (prevents DateTimeKind.Unspecified crashes)
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// 1. Serilog Bootstrapping
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting BlazorEnterprise.Worker background processing host...");

    var builder = Host.CreateApplicationBuilder(args);

    builder.AddServiceDefaults();

    // 2. Configure Serilog
    builder.Services.AddSerilog((services, loggerConfig) =>
    {
        loggerConfig
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithSpan()
            .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss 'UTC'}] [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
            .WriteTo.File(
                path: "logs/worker-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss 'UTC'}] [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");
    });

    // 3. Strongly typed Options
    builder.Services.AddOptions<JobsOptions>()
        .BindConfiguration(JobsOptions.SectionName)
        .Validate(o => !o.EnableScheduler || !string.IsNullOrWhiteSpace(o.CronServiceAccountEmail),
            "Zero-Trust Security Violation: 'Jobs:CronServiceAccountEmail' must be explicitly configured when 'Jobs:EnableScheduler' is enabled.")
        .ValidateOnStart();

    // 4. Persistence, Infrastructure, and Engine Gateway Client
    builder.Services.AddPersistence(builder.Configuration);
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddEngineClient(builder.Configuration);

    // 5. Register Background Jobs & Listener (Active Worker mode: listener enabled)
    var enableListener = builder.Configuration.GetValue<bool>("Jobs:EnableHostedListener", true);
    var enableScheduler = builder.Configuration.GetValue<bool>("Jobs:EnableScheduler", true);
    builder.Services.AddBackgroundJobs(enableListener: enableListener, enableScheduler: enableScheduler);

    var host = builder.Build();
    BlazorEnterprise.Core.Utilities.SystemIdentityUtility.SetCurrent(host.Services.GetRequiredService<BlazorEnterprise.Core.Contracts.ISystemIdentity>());

    Log.Information("BlazorEnterprise.Worker host built successfully. Starting background listener (Listener={EnableListener}, Scheduler={EnableScheduler})...",
        enableListener, enableScheduler);

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "BlazorEnterprise.Worker host terminated unexpectedly.");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
