using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

// Database Hosting Mode: Native Local PostgreSQL (Default) vs Docker Container
var useDockerPostgres = builder.Configuration.GetValue<bool>("Aspire:UseDockerPostgres", false);

IResourceBuilder<IResourceWithConnectionString> db;

if (useDockerPostgres)
{
    // Option B: Docker Containerized PostgreSQL + pgAdmin
    var postgres = builder.AddPostgres("postgres")
        .WithDataVolume()
        .WithPgAdmin();

    db = postgres.AddDatabase("DefaultConnection");
}
else
{
    // Option A: Native Local PostgreSQL & pgAdmin (No Docker required)
    // Directly references local PostgreSQL running on localhost:5432
    db = builder.AddConnectionString("DefaultConnection");
}

// 2. Launch Company Azure Cloud ProcessAPI & Auth Gateway
var api = builder.AddProject<Projects.BlazorFluent_Api>("api")
    .WithExternalHttpEndpoints();

// 3. Launch Client WebApp with references to Postgres and ProcessAPI
var web = builder.AddProject<Projects.BlazorFluent_Web>("web")
    .WithReference(db)
    .WithReference(api)
    .WithExternalHttpEndpoints();

builder.Build().Run();
