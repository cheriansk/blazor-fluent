# 15. Configuration Reference Guide

> **Target Audience**: Solution Architects, DevOps Engineers, and System Developers.  
> **Key Objective**: Comprehensive dictionary of all configuration sections, keys, default values, and environment variable overrides across `BlazorEnterprise.Web` and `BlazorEnterprise.Api`.

---

## ⚙️ 1. Configuration Precedence Order

ASP.NET Core resolves configuration keys using the following hierarchical precedence (highest to lowest priority):

1. **Environment Variables** (e.g., `ConnectionStrings__DefaultConnection`)
2. **Azure Key Vault / Cloud Secrets**
3. **User Secrets** (`secrets.json` - Local Development only)
4. **Environment-Specific Settings** (`appsettings.Development.json` / `appsettings.Production.json`)
5. **Base Settings** (`appsettings.json`)

---

## 📋 2. Comprehensive Configuration Key Dictionary

### 2.1 Database & Connection Strings

| Configuration Key | Env Var Equivalent | Default / Example | Purpose |
| :--- | :--- | :--- | :--- |
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` | `Host=localhost;Database=blazorenterprise;Username=...` | PostgreSQL connection string for `AppDbContext`. |
| `POSTGRES_APP_PASSWORD` | `POSTGRES_APP_PASSWORD` | `<secure-password>` | Runtime application password for PostgreSQL Row Level Security role (`blazorenterprise_app`). |

---

### 2.2 Security & Integrity

| Configuration Key | Env Var Equivalent | Default / Example | Purpose |
| :--- | :--- | :--- | :--- |
| `Security:IntegritySecret` | `Security__IntegritySecret` | `<32+ byte string>` | Master secret key used for HMAC row-level integrity seals on `UserEntity`. |
| `Security:CspReportUri` | `Security__CspReportUri` | `https://api.example.com/csp-reports` | Optional URI receiving Content Security Policy violation reports. |
| `Security:EnforceHttps` | `Security__EnforceHttps` | `true` | Enforces HSTS and HTTPS redirection middleware. |

---

### 2.3 Caching Subsystem

| Configuration Key | Env Var Equivalent | Default / Example | Purpose |
| :--- | :--- | :--- | :--- |
| `Caching:Provider` | `Caching__Provider` | `Memory` / `Redis` | Cache provider mode (`Memory` for dev, `Redis` for distributed production). |
| `Caching:RedisConnectionString` | `Caching__RedisConnectionString` | `localhost:6379,ssl=True` | Redis endpoint for L2 distributed cache and session protection keyring. |
| `Caching:DefaultExpirationMinutes` | `Caching__DefaultExpirationMinutes` | `30` | Default sliding expiration for cached tenant and user metadata. |

---

### 2.4 Background Jobs Pipeline

| Configuration Key | Env Var Equivalent | Default / Example | Purpose |
| :--- | :--- | :--- | :--- |
| `Jobs:QueueChannelCapacity` | `Jobs__QueueChannelCapacity` | `10000` | Bounded capacity for the background event `Channel<IJobEvent>`. |
| `Jobs:MaxConcurrentJobs` | `Jobs__MaxConcurrentJobs` | `4` | Maximum parallel worker tasks processing queued batch jobs. |
| `Jobs:CronServiceAccountEmail` | `Jobs__CronServiceAccountEmail` | `daemon@system.local` | Fallback service account identity for automated scheduled cron tasks. |

---

### 2.5 Notifications Subsystem

| Configuration Key | Env Var Equivalent | Default / Example | Purpose |
| :--- | :--- | :--- | :--- |
| `Notifications:Email:Enabled` | `Notifications__Email__Enabled` | `false` | Global toggle enabling outbound SMTP email dispatch. |
| `Notifications:Email:SmtpHost` | `Notifications__Email__SmtpHost` | `smtp.office365.com` | Outbound SMTP relay hostname. |
| `Notifications:Email:SmtpPort` | `Notifications__Email__SmtpPort` | `587` | Outbound SMTP TLS port. |
| `Notifications:Email:SenderEmail` | `Notifications__Email__SenderEmail` | `notifications@app.example.com` | Zero-Trust required sender address. |
| `Notifications:Teams:Enabled` | `Notifications__Teams__Enabled` | `true` | Global toggle enabling Microsoft Teams webhook dispatches. |

---

### 2.6 Stateless Engine Integration

| Configuration Key | Env Var Equivalent | Default / Example | Purpose |
| :--- | :--- | :--- | :--- |
| `Engine:ApiBaseUrl` | `Engine__ApiBaseUrl` | `https://api.internal.example.com` | Base URL pointing to stateless `BlazorEnterprise.Api`. |
| `Engine:TimeoutSeconds` | `Engine__TimeoutSeconds` | `60` | HTTP client timeout for large batch transformations. |
| `Engine:ApiKey` | `Engine__ApiKey` | `<service-api-key>` | Service authentication key for internal engine calls. |

---

## 🛠️ 3. Local Development User Secrets Recipe

To avoid committing sensitive credentials into source control, use .NET User Secrets during local development:

```bash
# Navigate to Web project
cd src/BlazorEnterprise.Web

# Initialize User Secrets
dotnet user-secrets init

# Set local database password and HMAC key
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=blazorenterprise;Username=postgres;Password=local_dev_password"
dotnet user-secrets set "Security:IntegritySecret" "dev_super_secret_hmac_key_for_local_testing_only_32_chars"
```

---

*Related Documentation*:
- [01. Getting Started & Environment Setup](file:///c:/Users/cheri/source/repos/BlazorEnterprise/docs/01-Getting-Started-and-Environment-Setup.md)
- [11. API Reference & Integration](file:///c:/Users/cheri/source/repos/BlazorEnterprise/docs/11-API-Reference-and-Integration.md)
- [12. Production Deployment & Operations](file:///c:/Users/cheri/source/repos/BlazorEnterprise/docs/12-Production-Deployment-and-Operations.md)
