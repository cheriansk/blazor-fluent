# 01. Getting Started Guide

> **Purpose**: Step-by-step developer onboarding instructions to clone, configure, migrate, run, and customize BlazorFluent for a new project.  
> **Who should read this**: Developers configuring their local workstation, DevOps engineers configuring CI/CD pipelines, or teams cloning the template.

---

## 💻 Prerequisites

Ensure your development environment meets the following specifications:

| Requirement | Supported Version | Notes |
|---|---|---|
| **.NET SDK** | `.NET 10.0` or higher | Target framework `net10.0`. Run `dotnet --version` to verify. |
| **PostgreSQL** | `14.0` to `17.x` | Local installation, Azure Database for PostgreSQL, or Docker container. |
| **PowerShell** | `7.0+` (`pwsh`) | Required for execution of `Rename-Project.ps1` and verification scripts. |
| **IDE / Editor** | Visual Studio 2026, VS Code, or JetBrains Rider | C# Dev Kit or Omnisharp extension recommended. |

---

## 📥 1. Cloning & Project Renaming

Clone the repository to your local workspace:

```bash
git clone https://github.com/your-org/BlazorFluent.git MyNewApp
cd MyNewApp
```

### Automated Template Renaming Script

BlazorFluent includes an automated renaming script, `Rename-Project.ps1`, which renames all projects, folders, namespaces, Docker references, and configuration strings in one command:

```powershell
# Preview changes without modifying files (Dry Run):
./Rename-Project.ps1 -NewName "AcmePortal" -DryRun

# Execute the renaming:
./Rename-Project.ps1 -NewName "AcmePortal"
```

The script systematically updates:
- All `.csproj` and `.slnx` solution references
- C# namespace declarations across all layers (`BlazorFluent.*` &rarr; `AcmePortal.*`)
- Folder structures and assembly outputs
- Configuration placeholders and launch profiles

---

## ⚙️ 2. Configuration Reference

All application settings are declared in `src/BlazorFluent.Web/appsettings.json`. For local development, overrides belong in `appsettings.Development.json` or `.NET User Secrets`.

> [!IMPORTANT]
> **Zero Secrets in Source Control**: Never commit real database passwords, encryption keys, or API tokens to git. Use `dotnet user-secrets` locally and Azure Key Vault or environment variables in production.

### Configuration Key Dictionary

| Configuration Section | Key Name | Purpose | Development Default | Production Strategy |
|---|---|---|---|---|
| `ConnectionStrings` | `DefaultConnection` | PostgreSQL database connection string. | `Host=localhost;Database=blazorfluent_db;...` | Azure Managed Identity or Key Vault secret |
| `ConnectionStrings` | `Redis` | Redis connection for distributed L2 cache. | *(Empty - falls back to Memory Cache)* | Azure Cache for Redis connection string |
| `Security` | `IntegritySecret` | Cryptographic HMAC-SHA256 secret for user row tamper detection (min 32 bytes). | Dev placeholder key | Azure Key Vault secret |
| `Security` | `MasterEncryptionKey` | 256-bit key for client-side AES-GCM file batch encryption. | Dev placeholder key | Azure Key Vault secret |
| `Security` | `RootAdmins` | Semicolon-separated list of superadmin emails granting host-level rights. | `admin@example.local;lead@example.local` | Corporate IT administrator emails |
| `Security` | `SystemBootstrapEmail` | System service account identity for initial database seeding. | `provisioner@example.local` | System principal email |
| `KeyVault` | `Uri` | Azure Key Vault vault URL. | `""` | `https://<vault-name>.vault.azure.net/` |
| `Notifications:Email` | `SmtpHost`, `SmtpPort` | SMTP server endpoint for outbound transactional emails. | `localhost` / `25` | Enterprise SMTP / SendGrid / Azure Email |
| `Notifications:Teams` | `DefaultWebhookUrl` | Microsoft Teams incoming webhook for system-wide notices. | `""` | Teams channel webhook URL |
| `Jobs:Schedules` | `TenantHealthCheckJob` | Cron expression for periodic tenant health verification. | `0 2 * * *` (Daily 2 AM UTC) | Configured cron schedule |
| `Jobs:Schedules` | `AuditPurgeJob` | Cron expression for soft-deleted audit record cleanup. | `0 3 * * 0` (Weekly Sunday 3 AM) | Configured cron schedule |

### Configuring Secrets Locally

Initialize and store your local development secrets:

```bash
cd src/BlazorFluent.Web
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=my_local_db;Username=postgres;Password=MySecretPassword;"
dotnet user-secrets set "Security:IntegritySecret" "MySuperSecretCryptographicKeyWithAtLeast32BytesLength!"
dotnet user-secrets set "Security:MasterEncryptionKey" "AnotherSecure32ByteEncryptionKeyForAESGCM!"
```

---

## 🗄️ 3. Database Initialization & EF Core Migrations

BlazorFluent uses EF Core 10 targeting PostgreSQL (`Npgsql.EntityFrameworkCore.PostgreSQL`).

### Applying Migrations

Run the database update command pointing to `BlazorFluent.Persistence` as the migration project and `BlazorFluent.Web` as the startup project:

```bash
dotnet ef database update --project src/BlazorFluent.Persistence --startup-project src/BlazorFluent.Web
```

### Adding New Migrations

Whenever domain entities or configurations change:

```bash
dotnet ef migrations add AddFeatureName --project src/BlazorFluent.Persistence --startup-project src/BlazorFluent.Web --output-dir Migrations
```

### Automatic Bootstrap Seeding

When the database starts up empty, `PersistenceExtensions.cs` executes a startup initialization pass:
1. Verifies database connectivity.
2. Applies any pending EF Core migrations automatically if configured.
3. Seeds the default root tenant, system settings, and creates the initial root administrator account matching the first email in `Security:RootAdmins`.

---

## 🔐 4. Authentication Modes & Dev Login

BlazorFluent supports dual-mode authentication out of the box:

### Development Mode (Default)
In development (`ASPNETCORE_ENVIRONMENT=Development`), navigating to `/login` reveals a **Dev Authentication Switcher**. This enables instant switching between:
- **Root SuperAdmin** (`admin@example.local`)
- **Tenant Administrator** (`tenantadmin@company.local`)
- **Project Manager** (`pm@company.local`)
- **Team Member** (`engineer@company.local`)
- **Client User** (`client@partner.local`)

This allows comprehensive RBAC testing without requiring a live Azure Active Directory tenant.

### Production Mode (Enterprise Azure AD / Entra ID)
In production, the application connects to your corporate Azure AD via OpenID Connect (OIDC) with PKCE. Authentication validates user accounts against the PostgreSQL `identity.Users` table, enforcing active status, date windows, and the HMAC row integrity seal.

---

## ▶️ 5. Running the Application

Launch the Web host:

```bash
dotnet run --project src/BlazorFluent.Web
```

Open your browser at `https://localhost:7001` (or the port specified in `Properties/launchSettings.json`).

### Health Check Probes

Verify system readiness using standard endpoints:
- **Liveness Probe**: `GET https://localhost:7001/healthz`  
  *Returns `200 Healthy` immediately without database queries (ideal for Kubernetes liveness).*
- **Readiness Probe**: `GET https://localhost:7001/health/ready`  
  *Verifies PostgreSQL connection and EF Core context responsiveness.*

---

## 🧹 6. Deleting Sample Entities for Clean Slate

If you are using this starter kit for a new domain and wish to remove the demo catalog entities:

1. **Delete Entity**: Remove `src/BlazorFluent.Core/Domain/Catalog/ProductEntity.cs`.
2. **Delete Configuration**: Remove `src/BlazorFluent.Persistence/Configurations/Catalog/ProductEntityConfiguration.cs`.
3. **Remove DbSet**: In `src/BlazorFluent.Persistence/Context/AppDbContext.cs`, delete `public DbSet<ProductEntity> Products => Set<ProductEntity>();`.
4. **Remove UI Page**: Remove `src/BlazorFluent.Web/Modules/Catalog/Pages/CatalogDashboard.razor`.
5. **Remove Job**: Remove `src/BlazorFluent.Jobs/Jobs/Catalog/CatalogSyncJobHandler.cs`.
6. **Generate Migration**: Run `dotnet ef migrations add RemoveCatalogSample --project src/BlazorFluent.Persistence --startup-project src/BlazorFluent.Web`.

---

## 🩺 7. Common Troubleshooting

### Ambiguous `TaskStatus` Compilation Error
- **Symptom**: C# compiler error `CS0104: 'TaskStatus' is an ambiguous reference between 'System.Threading.Tasks.TaskStatus' and 'BlazorFluent.Core.DataListTypes.TaskStatus'`.
- **Cause**: .NET 10 implicitly imports `System.Threading.Tasks` in web SDK projects.
- **Solution**: The solution repository globally aliases this in `Directory.Build.props` for all C# files, and Razor files include:
  ```razor
  @using TaskStatus = BlazorFluent.Core.DataListTypes.TaskStatus
  ```
  in `src/BlazorFluent.Web/_Imports.razor`. Ensure any new Razor files or independent projects import this alias.

### Captive Dependency DI Scope Exception
- **Symptom**: Application crashes on launch with `InvalidOperationException: Cannot consume scoped service 'AppDbContext' from singleton ...`.
- **Cause**: `Program.cs` enforces `options.ValidateScopes = true` across all environments to protect against threading crashes.
- **Solution**: Never inject a `Scoped` service into a `Singleton`. In background workers or channel listeners, inject `IServiceScopeFactory` and create a scope using `using var scope = _scopeFactory.CreateScope();`.

### Fluent UI Blazor V5 Component Compilation Warnings
- **Symptom**: Compiler warning `CS0618` on legacy Fluent UI controls.
- **Cause**: `Directory.Build.props` enforces `<WarningsAsErrors>CS0618</WarningsAsErrors>` to reject legacy V4 controls.
- **Solution**: Run `pwsh -ExecutionPolicy Bypass -File .\scripts\verify-fluent-v5.ps1` to detect and fix any deprecated component parameters.

---

## 🔗 Related Documentation

- [00. Overview & Glossary](00-Overview-and-Glossary.md) — Personas, routes, and domain glossary.
- [02. Architecture Deep Dive](02-Architecture.md) — Layer boundaries, DI rules, and caching topology.
- [03. Security Model](03-Security-Model.md) — Multi-tenant write barriers, HMAC seals, and session controls.
- [04. Task Hierarchy & Workflows](04-Task-Hierarchy-and-Workflows.md) — Workflow orchestration and wizard models.
- [05. UI Conventions & Fluent V5](05-UI-Conventions-Fluent-V5.md) — Razor naming taxonomy and V5 component guidelines.
- [06. How to Extend Recipes](06-How-To-Extend.md) — Step-by-step recipes for creating new features.
- [07. AI Agent Operating Rules](07-AI-Agent-Rules.md) — Guidelines for pairing with AI assistants on this repo.
