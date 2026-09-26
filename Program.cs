using BlazorFluent.Components;
using BlazorFluent.Jobs;
using BlazorFluent.Persistence;
using Microsoft.FluentUI.AspNetCore.Components;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure console logging timezone based on DateTimeSettings:UseUtc
var useUtc = builder.Configuration.GetValue<bool>("DateTimeSettings:UseUtc", defaultValue: true);
builder.Logging.AddSimpleConsole(options =>
{
    options.IncludeScopes = true;
    options.UseUtcTimestamp = useUtc;
    options.TimestampFormat = useUtc ? "[yyyy-MM-dd HH:mm:ss UTC] " : "[yyyy-MM-dd HH:mm:ss] ";
});

// 2. Add presentation and UI services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddFluentUIComponents();

// 3. Lean Modular Monolith Registrations
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddBackgroundJobs(enableScheduler: true);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
