using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

// Database Hosting Mode: Native Local PostgreSQL (Default) vs Docker Container
// When false, BlazorEnterprise.Web reads ConnectionStrings:DefaultConnection directly from its own appsettings.Development.json.
var useDockerPostgres = builder.Configuration.GetValue<bool>("Aspire:UseDockerPostgres", false);

// 2. Launch Company Azure Cloud ProcessAPI & Auth Gateway
var api = builder.AddProject<Projects.BlazorEnterprise_Api>("api")
    .WithExternalHttpEndpoints();

// 3. Launch Client WebApp with references to ProcessAPI (and Docker Postgres when enabled)
var web = builder.AddProject<Projects.BlazorEnterprise_Web>("web")
    .WithReference(api)
    .WithExternalHttpEndpoints();

if (useDockerPostgres)
{
    // Option B: Docker Containerized PostgreSQL + pgAdmin
    var postgres = builder.AddPostgres("postgres")
        .WithDataVolume()
        .WithPgAdmin();

    var db = postgres.AddDatabase("DefaultConnection");
    web.WithReference(db);
}

builder.Build().Run();
