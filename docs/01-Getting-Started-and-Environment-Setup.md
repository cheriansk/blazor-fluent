# 01. Getting Started & Environment Setup

> **Target Audience**: Developers, DevOps Engineers, and System Architects setting up the BlazorFluent solution locally or in CI/CD.  
> **Key Objective**: Step-by-step setup guide covering .NET 10, Aspire orchestration, dual-mode PostgreSQL routing, configuration keys, migrations, and troubleshooting.

---

## 💻 1. Prerequisites

Before running the solution, verify your workstation has the following installed:

| Tool | Minimum Version | Installation Link | Notes |
| :--- | :--- | :--- | :--- |
| **.NET SDK** | `10.0.100+` | [Download .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0) | Verify via `dotnet --version`. |
| **IDE** | VS 2026 or VS Code | [Visual Studio Preview](https://visualstudio.microsoft.com/) / [VS Code](https://code.visualstudio.com/) | C# Dev Kit extension required for VS Code. |
| **PostgreSQL** | `16.0+` | [PostgreSQL Downloads](https://www.postgresql.org/download/) | **Native local installation supported** (no Docker required). |
| **Docker Desktop** | Optional | [Docker Desktop](https://www.docker.com/products/docker-desktop/) | Only required if choosing containerized PostgreSQL. |

---

## 🗄️ 2. Dual-Mode Database Routing

BlazorFluent provides two out-of-the-box database topologies configured in [`src/BlazorFluent.AppHost/appsettings.json`](file:///c:/Users/cheri/source/repos/BlazorFluent/src/BlazorFluent.AppHost/appsettings.json):

```mermaid
flowchart TD
    AppHost["BlazorFluent.AppHost\n(Program.cs)"] --> Check{"Aspire:UseDockerPostgres\n== true?"}
    
    Check -->|false (Default)| Native["Option A: Native Local PostgreSQL\n• Host: localhost:5432\n• Zero Docker daemon required\n• Connects to Windows PostgreSQL service\n• Direct pgAdmin 4 connection"]
    Check -->|true| Docker["Option B: Containerized PostgreSQL\n• Spins up postgres:latest container\n• Spins up dpage/pgadmin4 container\n• Auto-wired into Aspire Dashboard"]
```

### Option A: Native Local PostgreSQL (Default — Zero Docker Overhead)
If you have PostgreSQL installed natively on Windows:
1. Ensure your PostgreSQL service is running on port `5432`.
2. Configure [`src/BlazorFluent.AppHost/appsettings.json`](file:///c:/Users/cheri/source/repos/BlazorFluent/src/BlazorFluent.AppHost/appsettings.json):
   ```json
   {
     "Aspire": {
       "UseDockerPostgres": false
     },
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Port=5432;Database=blazorfluent_db;Username=postgres;Password=<your_password>;Pooling=true;"
     }
   }
   ```
3. Run the solution. Aspire passes this connection string directly to `BlazorFluent.Web` and `BlazorFluent.Api`.

### Option B: Docker Containerized PostgreSQL
If you prefer containerized services managed by Aspire:
1. Start Docker Desktop.
2. In [`src/BlazorFluent.AppHost/appsettings.json`](file:///c:/Users/cheri/source/repos/BlazorFluent/src/BlazorFluent.AppHost/appsettings.json), set:
   ```json
   {
     "Aspire": {
       "UseDockerPostgres": true
     }
   }
   ```
3. Aspire automatically provisions PostgreSQL and pgAdmin containers, binding them to dynamic host ports visible in the Aspire Dashboard.

---

## ⚙️ 3. Configuration Dictionary (`appsettings.json`)

The central settings file (`src/BlazorFluent.Web/appsettings.json` and `src/BlazorFluent.AppHost/appsettings.json`) controls enterprise behavior:

| Configuration Section | Key | Default Value | Description |
| :--- | :--- | :--- | :--- |
| **`ConnectionStrings`** | `DefaultConnection` | `Host=localhost;...` | Primary PostgreSQL database connection string. |
| **`Aspire`** | `UseDockerPostgres` | `false` | When `false`, uses local native PostgreSQL. When `true`, provisions Docker containers. |
| **`Security`** | `IntegritySecret` | `(32+ byte string)` | Cryptographic secret used for HMAC-SHA256 user row integrity seals. |
| **`Security`** | `StorageEncryptionKey` | `(32+ byte string)` | Master key for HKDF-SHA256 tenant envelope stream encryption. |
| **`Jobs`** | `EnableHostedListener` | `true` | When `true`, `BlazorFluent.Web` starts the in-process batch queue listener (single-node). Set to `false` when running `BlazorFluent.Worker`. |
| **`Jobs`** | `EnableScheduler` | `true` | Enables Cronos periodic background schedulers. |
| **`Jobs`** | `CronServiceAccountEmail` | `daemon-worker@local.system` | Service account identity for cron-triggered background jobs. |
| **`Tenants:{tenantId}`** | `LicenseKey` | `(256-bit base64)` | Per-tenant private license key used for ephemeral S2S token signing and AES payload encryption. |
| **`TemplateGuard`** | `TemplateGuardMode` | `TemplateAuthoring` | `TemplateAuthoring` allows free editing and auto-generates manifests. `DownstreamEnforce` strictly blocks unauthorized changes. |
| **`Notifications:Email`**| `SmtpHost` | `localhost` | Outbound SMTP relay for workflow and security notifications. |
| **`Notifications:Teams`**| `DefaultWebhookUrl` | `""` | Microsoft Teams incoming webhook for system alerts. |

---

## 🚀 4. Launching the Solution

### Approach 1: Via .NET Aspire Orchestrator (Recommended)
Launching through `BlazorFluent.AppHost` runs the web application, API gateway, and the Aspire Telemetry Dashboard together:
1. In Visual Studio, right-click [`src/BlazorFluent.AppHost`](file:///c:/Users/cheri/source/repos/BlazorFluent/src/BlazorFluent.AppHost/BlazorFluent.AppHost.csproj) -> **Set as Startup Project**.
2. Press **F5** (or **Ctrl+F5** to start without debugging).
3. The Aspire Dashboard opens at `http://localhost:15888` (or assigned port), listing all running endpoints, live traces, metrics, and application logs.
4. Click on the `web` endpoint to open the BlazorFluent user interface.

### Approach 2: Standalone Web Application
If launching only the UI:
1. Set [`src/BlazorFluent.Web`](file:///c:/Users/cheri/source/repos/BlazorFluent/src/BlazorFluent.Web/BlazorFluent.Web.csproj) as Startup Project.
2. Press **F5** / **Ctrl+F5**. The app launches at `https://localhost:7001`.

### Approach 3: Standalone Background Worker
When deploying in distributed mode (with `Jobs:EnableHostedListener = false` in `BlazorFluent.Web`):
1. In a terminal, launch the dedicated console worker:
   ```powershell
   dotnet run --project src/BlazorFluent.Worker
   ```
2. The worker boots with Aspire ServiceDefaults, connects to PostgreSQL, and begins processing channel job events and periodic cron schedules.

---

## 🔄 5. Applying Database Migrations

BlazorFluent uses EF Core 10 migrations managed in `BlazorFluent.Persistence`:

```powershell
# Apply pending migrations to your database
dotnet ef database update --project src/BlazorFluent.Persistence --startup-project src/BlazorFluent.Web

# Adding a new migration after entity changes
dotnet ef migrations add <MigrationName> --project src/BlazorFluent.Persistence --startup-project src/BlazorFluent.Web
```

---

## 🛠️ 6. Troubleshooting & Common Scenarios

### 1. Aspire Orchestration `CliPath` or `DashboardPath` Error
**Symptom**: `Property CliPath: The path to the DCP executable used for Aspire orchestration is required.`  
**Cause**: The project file was using `Microsoft.NET.Sdk` instead of the Aspire SDK.  
**Resolution**: Verify that [`src/BlazorFluent.AppHost/BlazorFluent.AppHost.csproj`](file:///c:/Users/cheri/source/repos/BlazorFluent/src/BlazorFluent.AppHost/BlazorFluent.AppHost.csproj) declares `<Project Sdk="Aspire.AppHost.Sdk/13.4">`.

### 2. Global Usings CS0246 Error on AppHost
**Symptom**: `The type or namespace name 'BlazorFluent' could not be found in BlazorFluent.AppHost.GlobalUsings.g.cs`  
**Resolution**: [`Directory.Build.props`](file:///c:/Users/cheri/source/repos/BlazorFluent/Directory.Build.props#L49) explicitly excludes `BlazorFluent.AppHost` from injecting shared library namespaces.

### 3. TemplateGuard Skipping During Debug / Run
**Design**: Base manifest generation (`.basefiles.json`) and TemplateGuard AST indexing run during explicit **Build** or **Rebuild**. When you click **Start Debugging (F5)** or **Start Without Debugging (Ctrl+F5)**, Visual Studio's **Fast Up-to-Date Check (FUTDC)** skips this target in ~0 ms if source files are unchanged.
