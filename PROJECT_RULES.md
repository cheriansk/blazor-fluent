# BlazorFluent — Project Rules & Best Practices

> **Canonical reference for all code authored by humans or AI agents in this template and any project derived from it.**
> Every contributor — developer, reviewer, or AI coding assistant — must follow these rules without exception.

---

## Quick-Start Checklist (New Project from Template)

When cloning this template for a new Blazor WebApp, complete these steps before writing any feature code:

- [ ] **Run the rename script**: `./Rename-Project.ps1 -NewName "YourAppName"` — automatically renames all projects, namespaces, files, directories, config values, and string literals.
- [ ] Update `ConnectionStrings:DefaultConnection` in `appsettings.json` with your PostgreSQL database.
- [ ] Update `Security:IntegritySecret` with a new cryptographically random HMAC key (≥ 32 bytes).
- [ ] Configure `Notifications:Teams:DefaultWebhookUrl` and `Notifications:Email:*` settings.
- [ ] Configure `KeyVault:Uri` and `ApplicationInsights:ConnectionString` for production.
- [ ] Review and update `.github/dependabot.yml` schedule and assignees.
- [ ] Review and update `.github/workflows/owasp-zap-scan.yml` target URL.
- [ ] Run initial EF Core migration: `dotnet ef migrations add InitialCreate -p YourAppName.Persistence -s YourAppName`.
- [ ] Delete sample entities (`ProductEntity`, `CatalogDashboard`, `CatalogSyncJobHandler`, `Weather.razor`, `Counter.razor`) once your domain entities are in place.
- [ ] Update `README.md` with your project's description, setup instructions, and deployment guide.

---

## 1. Architecture Rules

### 1.1 Clean Architecture Layer Boundaries

```
BlazorFluent.Core          → Domain entities, contracts, enums, value objects, validation
BlazorFluent.Persistence   → EF Core DbContext, configurations, interceptors, migrations, data services
BlazorFluent.Infrastructure→ Outbound senders (email/Teams), security middleware, observability, UI services
BlazorFluent.Jobs          → Background job handlers, schedulers, event queues
BlazorFluent (Web)         → Razor components, pages, layouts, Program.cs composition root
```

| Rule | Description |
|------|-------------|
| **Dependency direction** | Dependencies flow inward only. `Core` has zero internal project references. |
| **Persistence isolation** | `BlazorFluent.Persistence` contains 100% database persistence code. No HTTP clients, no SMTP, no file I/O. |
| **Infrastructure isolation** | `BlazorFluent.Infrastructure` handles outbound integrations, security middleware, and browser interop services. Never references `Persistence`. |
| **No circular references** | `Core` ← `Persistence`, `Core` ← `Infrastructure`, `Core` + `Persistence` ← `Jobs`, All ← `Web`. |
| **Abstractions in Core** | Every service contract (`I*Service`, `I*Sender`, `I*Validator`) lives in `BlazorFluent.Core/Contracts/`. Implementations live in `Persistence`, `Infrastructure`, or `Jobs`. |

### 1.2 Entity Naming Convention

Every domain class and abstract base class in `BlazorFluent.Core` **must end with `Entity`**.

| ✅ Correct | ❌ Wrong |
|-----------|---------|
| `ProductEntity` | `Product` |
| `TenantAuditableEntity` | `TenantAuditable` |
| `UserSessionEntity` | `UserSession` |

### 1.2 Entity Architecture & Developer Decision Guide

When creating a new entity in `BlazorFluent.Core/Domain/`, follow this 3-step decision framework:

#### Step 1: Choose the Base Class (Single Class Inheritance)
| If the entity is... | Inherit from... | What you get |
|---|---|---|
| **Tenant-scoped with audit trail** (90% of business entities) | `TenantAuditableEntity` | Sequential UUIDv7 `Id`, `Created/Updated` stamps, `TenantId` |
| **Host-wide / System entity with audit trail** (e.g. Users, Tenants) | `AuditableEntity` | Sequential UUIDv7 `Id`, `Created/Updated` stamps (no `TenantId`) |
| **Lightweight table with no audit trail** (e.g. keyrings, technical caches) | `BaseEntity` | Sequential UUIDv7 `Id` only |

#### Step 2: Choose Required Multi-Tenancy Boundary
| If the entity is... | Must Implement... | Enforcement |
|---|---|---|
| **Scoped to a single tenant** | `ITenantEntity` (already included if inheriting `TenantAuditableEntity`) | EF Core automatic `QueryFilters.Tenant` isolation |
| **System-wide / Cross-tenant** | `IGlobalEntity` | Explicit opt-out; startup fail-closed guard throws if unassigned |

#### Step 3: Choose Composable Capabilities (Multiple Marker Interfaces)
| Capability Needed | Implement Marker Interface | Automatic Behavior Enabled |
|---|---|---|
| **Soft Deletion** | `ISoftDeletableEntity` | `DbContext.Remove()` becomes soft delete; filtered by `QueryFilters.SoftDelete` |
| **Project Workspace Scoping** | `IProjectScopedEntity` | Write operations guarded by `ProjectSecurityInterceptor` against user role |
| **Effective Dating (Active Date Ranges)** | `IEffectiveDatedEntity` | Adds `StartDate`/`EndDate?`; interceptor enforces `EndDate >= StartDate`; LINQ `query.WhereActive()` |
| **Revision / Version Tracking** | `IVersionedEntity` | Adds `VerNum` and `SetVersionNumber(newVer)`. Interceptor validates `VerNum >= 1`. Versions are explicitly controlled via domain logic, not blind auto-increments |
| **Skip Audit Diff Logging** | `IAuditExemptEntity` | Prevents recursive diff logging in `audit.AuditRecords` |

#### Common Entity Scenarios Quick Reference Table
| Entity Scenario | Base Class | Marker Interfaces |
|---|---|---|
| **Standard Business Entity** (Products, Customers, Orders) | `TenantAuditableEntity` | `ISoftDeletableEntity` |
| **Project Workspace Entity** (Tasks, Documents, Features) | `TenantAuditableEntity` | `IProjectScopedEntity`, `ISoftDeletableEntity` |
| **Effective-Dated Contract / Pricing** | `TenantAuditableEntity` | `IEffectiveDatedEntity`, `IVersionedEntity`, `ISoftDeletableEntity` |
| **Company Tenant Entity** | `AuditableEntity` | `IGlobalEntity`, `IEffectiveDatedEntity` |
| **User Account Entity** | `AuditableEntity` | `IGlobalEntity`, `ISoftDeletableEntity` |
| **System Audit Record / Job Execution** | `AuditableEntity` / `TenantAuditableEntity` | `IAuditExemptEntity` |

### 1.3 DataListTypes Enum Standard

All shared domain enums live in `BlazorFluent.Core/DataListTypes/`. Never declare shared enums in `Contracts/`, `Common/`, or `Constants/`.

| Standard | Rule & Implementation |
|---|---|
| **Clean Code Identifiers** | Enum entries use alphanumeric codes with zero spaces or symbols (e.g. `ClientUser`, `CompanyUser`, `Admin`). This code is used in logic and database persistence. |
| **Mandatory Display & Optional Description** | Every enum member, Category, and Filter declares `[Display(Name = "...", Description = "...")]` with mandatory `Name` and optional `Description`. Never use display names for logic comparisons. Retrieve via `.GetDisplayName()` and `.GetDescription()`. |
| **Companion Definition Classes** | Enums define structured companion classes in the same file (e.g. `{EnumName}Definitions.Categories`, `{EnumName}Definitions.Filters`) declaring `const string` codes with `[Display]`. |
| **Code-Only Attribute Signature** | `[DataListCategory(Definitions.Categories.Code)]` and `[DataListFilterCriterias(Definitions.Filters.Code)]` accept **only the constant code**, eliminating string repetition across members. |
| **Rich Metadata Resolution** | `DataListExtensions` inspects companion definition classes via cached reflection ($O(1)$) to link each enum member to its `CategoryInfo` and `FilterInfo` records (resolving `Code`, `DisplayName`, and `Description`). |
| **UI Projection** | Use `.ToDataListItems<TEnum>()` to project enums into immutable `DataListItem<TEnum>` records for direct binding to `FluentSelect` or data grids. |

### 1.4 Primary Keys

All entities use **sequential UUIDv7** via `Guid.CreateVersion7()` in `BaseEntity`. Never use auto-increment integers or random GUIDs.

---

## 2. Multi-Tenancy Rules

| Rule | Description |
|------|-------------|
| **Fail-closed guard** | Every EF Core entity must implement either `ITenantEntity` (tenant-scoped) or `IGlobalEntity` (host-wide). Unlabeled entities cause a startup exception. |
| **Automatic query filters** | EF Core 10 named query filters (`QueryFilters.Tenant`, `QueryFilters.SoftDelete`) are applied automatically in `AppDbContext.OnModelCreating`. Never bypass filters without explicit `IgnoreQueryFilters()`. |
| **TenantId immutability** | `AuditableEntityInterceptor` blocks any attempt to change `TenantId` after initial creation. |
| **Cache isolation** | All cached values use tenant-prefixed keys: `t:{tenantId}:{key}`. Eviction uses `tenant:{tenantId}` tags. |
| **Host-only queries** | Only host-level admin operations may use `IgnoreQueryFilters()`. Scope these queries clearly and audit them. |

---

## 3. Security Rules

### 3.1 Authentication & Authorization

| Rule | Description |
|------|-------------|
| **Dual-gate authorization** | Both `DefaultPolicy` and `FallbackPolicy` require authenticated users. Every route is protected by default. |
| **Page-level role contract** | All protected pages extend `AuthorizedPageComponentBase` and declare `MinimumVisitRole` and `MinimumEditRole` at compile time. |
| **Resource-based AuthZ** | Entity-instance write operations use `IAuthorizationService.AuthorizeAsync(User, entity, requirement)` via `ProjectResourceAuthorizationHandler`. |
| **Fail-closed interceptor** | `ProjectSecurityInterceptor` aborts all `SaveChanges` operations on `IProjectScopedEntity` if the user lacks write permissions. |

### 3.2 Input & Output Protection

| Rule | Description |
|------|-------------|
| **XSS sanitization** | All user-submitted HTML/text must pass through `IInputSanitizer.SanitizeHtml()` before database persistence. |
| **No raw SQL** | All queries use LINQ / EF Core. Never use `FromSqlRaw()` or `ExecuteSqlRaw()` with user-provided interpolation. |
| **Anti-forgery** | `UseAntiforgery()` is registered in the middleware pipeline. Never disable it. |
| **Error sanitization** | The `/Error` page displays only a correlation ID (`ERR-XXXXXXXX`). Never expose stack traces, connection strings, or internal paths to users. |

### 3.3 Cryptographic Standards

| Rule | Description |
|------|-------------|
| **Row integrity seals** | `UserEntity` uses HMAC-SHA256 row signatures. The secret key must be ≥ 32 bytes and stored in Azure Key Vault (production) or `appsettings.json` (development only). |
| **Webhook verification** | All inbound webhooks must validate payloads via `IWebhookSignatureValidator` using constant-time comparison (`CryptographicOperations.FixedTimeEquals`). |
| **Form draft encryption** | Browser-persisted form drafts use `ProtectedLocalStorage` backed by the Data Protection keyring. Never store unencrypted user data in `localStorage`. |

### 3.4 HTTP Security Headers

The following headers are injected by `SecurityHeadersExtensions` middleware and must not be weakened:

- `Content-Security-Policy` (Blazor Server-safe: allows `wss:`/`ws:` WebSockets, `data:` for SVG icons)
- `X-Frame-Options: SAMEORIGIN`
- `X-Content-Type-Options: nosniff`
- `Referrer-Policy: strict-origin-when-cross-origin`
- `Permissions-Policy` (restricts camera, microphone, geolocation)
- `X-XSS-Protection: 1; mode=block`

### 3.5 Secret Management

| Environment | Method |
|-------------|--------|
| Development | `appsettings.json` / `appsettings.Development.json` (gitignored secrets) |
| Production | Azure Key Vault via `DefaultAzureCredential()`. Never commit production secrets to source control. |

### 3.6 Supply Chain Security

- `Directory.Build.props` enforces `NuGetAudit=true` with `NuGetAuditLevel=moderate`.
- NuGet CVE warnings (`NU1901`–`NU1904`) are promoted to build errors.
- `.github/dependabot.yml` runs weekly NuGet dependency updates.
- `.github/workflows/owasp-zap-scan.yml` runs weekly OWASP ZAP baseline vulnerability scans.

---

## 4. EF Core & Database Rules

### 4.1 Configuration Standards

| Rule | Description |
|------|-------------|
| **Fluent API only** | All entity configurations use `IEntityTypeConfiguration<T>` in `BlazorFluent.Persistence/Configurations/`. Never use data annotation attributes for EF mapping. |
| **PostgreSQL schemas** | Each domain area has its own PostgreSQL schema: `audit`, `catalog`, `identity`, `jobs`, `notifications`, `tenancy`. |
| **Audit columns** | Every entity gets `Created`, `CreatedBy`, `Updated`, `UpdatedBy` columns — either via `IAuditableEntity` or shadow properties injected by `AuditableEntityInterceptor`. |
| **Exception processing** | `options.UseExceptionProcessor()` is configured in `AddDbContext`. Catch strongly-typed exceptions (`UniqueConstraintException`, `ForeignKeyConstraintException`) instead of generic `DbUpdateException`. |
| **Connection string** | Always read from `configuration.GetConnectionString("DefaultConnection")`. Never hardcode. |

### 4.2 Performance Standards

| Rule | Description |
|------|-------------|
| **Offset pagination** | Use `PagedResult<T>` with `Skip/Take` only for small data sets (< 1,000 records). |
| **Keyset pagination** | Use `KeysetPaginationExtensions.ToKeysetPagedResult()` for tables exceeding 10,000 records. |
| **Bulk operations** | Use `ExecuteDeleteAsync` / `ExecuteUpdateAsync` for mass operations. Never load entities into memory for bulk deletes. |
| **No N+1 queries** | Always use `.Include()` / `.ThenInclude()` or projection (`.Select()`) to prevent lazy-loading N+1 traps. EF Core lazy loading is disabled. |
| **Scoped DbContext** | `AppDbContext` is registered as `Scoped`. Never inject it into `Singleton` services. |

### 4.3 ComplexType Value Objects

Value objects use EF Core 10 `[ComplexType]` and are declared as C# `record` types in `BlazorFluent.Core/Domain/ValueObjects/`. They embed inline as columns in the host entity's table without shadow keys or join tables.

**Reference example**: `Address` value object on `UserEntity.PhysicalAddress`, configured via `builder.ComplexProperty(u => u.PhysicalAddress)`.

### 4.4 Transaction Management

All multi-step write operations must use `IUnitOfWork.ExecuteAsync(...)` for atomic transactions with automatic rollback and `ChangeTracker.Clear()` on failure.

---

## 5. Blazor UI Rules

### 5.1 Component Library

This project uses **Microsoft Fluent UI Blazor V5**. All UI components must use V5 APIs.

| Rule | Description |
|------|-------------|
| **V5 appearance enums** | Use `ButtonAppearance.Primary`, `BadgeColor.Brand`, `MessageBarIntent.Success`, etc. Never use V4 string-based parameters. |
| **Dual-generic selects** | `FluentSelect<TOption, TValue>` requires both type parameters. Never omit the value type. |
| **Icons package** | Use `Microsoft.FluentUI.AspNetCore.Components.Icons` for all icon references. |

### 5.2 Reusable Components

| Component | Purpose | Location |
|-----------|---------|----------|
| `<PageHeader>` | Consistent page title/subtitle with action slot | `Components/Common/` |
| `<ConfirmDialog>` | Accessible modal for destructive actions | `Components/Common/` |
| `<EmptyState>` | Centered icon + message for empty views | `Components/Common/` |
| `<FormNavigationGuard>` | Warns on unsaved dirty form edits | `Components/Common/` |
| `<ProjectAuthorizeView>` | Role-gated conditional rendering | `Components/Common/` |
| `<ImpersonationBanner>` | Pinned banner during impersonation | `Components/Common/` |

### 5.3 State Management

| Rule | Description |
|------|-------------|
| **Form drafts** | Use `ILocalStorageFormService` for encrypted auto-save of uncommitted form data. |
| **Circuit persistence** | Use `PersistentStateComponentBase` for state survival across circuit pause/resume. |
| **Session revocation** | `<SessionRevokedModal>` monitors session validity and freezes the UI on revocation. |

### 5.4 Error Handling

| Rule | Description |
|------|-------------|
| **Root error boundary** | `<ObservabilityErrorBoundary>` wraps the router, logging unhandled exceptions with structured tenant/user context. |
| **Page error boundary** | `<AppErrorBoundary>` generates user-facing `ERR-XXXXXXXX` correlation IDs with a recovery button. |
| **Reconnection** | `<ReconnectModal>` provides custom SignalR reconnection UI. Never use the default Blazor reconnection overlay. |

### 5.5 Responsive Design

Use `ILayoutBreakpointService` for programmatic breakpoint detection:
- **Mobile**: < 640px
- **Tablet**: < 1024px
- **Desktop**: ≥ 1024px

---

## 6. Observability & Logging Rules

### 6.1 Serilog Standards

| Rule | Description |
|------|-------------|
| **Two-stage bootstrap** | Serilog initializes with console-only bootstrap logger before host configuration. Production Serilog configures file sink + Application Insights. |
| **Structured logging** | Always use message templates with named parameters: `Log.Information("Processing {OrderId}", orderId)`. Never use string interpolation. |
| **Context enrichment** | Push tenant/user context via `LogContext.PushProperty(...)`. The request logging middleware auto-enriches `ClientIp`, `Host`, `TenantId`, `UserId`. |
| **Span correlation** | `Serilog.Enrichers.Span` attaches `TraceId` and `SpanId` to every log entry for distributed tracing. |
| **Static asset silencing** | Request logging excludes `/_framework`, `/_content`, `/favicon.ico`, and CSS/JS paths. |

### 6.2 Forensic Auditing

| Level | Description | Storage |
|-------|-------------|---------|
| **Level 1** | Row timestamps (`Created`, `CreatedBy`, `Updated`, `UpdatedBy`) | Entity columns |
| **Level 2** | Before/after property diffs with sensitive keyword masking | `audit.AuditRecords` (JSON) |
| **Level 3** | Security events, login activity, impersonation grants | `audit.AuditRecords` + Serilog |

### 6.3 Health Checks

- `/healthz` — Liveness probe (app is running).
- `/health/ready` — Readiness probe (database and dependencies are available).

---

## 7. Background Jobs Rules

| Rule | Description |
|------|-------------|
| **Zero database polling** | Schedulers use `Cronos` + `Task.Delay()` for time-based scheduling. Job events flow through `System.Threading.Channels`. |
| **Tenant context preservation** | `BatchJobQueueListener` restores `ITenantContext` into scoped DI containers before executing handlers. |
| **Retry policy** | 3 attempts with exponential backoff (500ms → 1500ms → 3000ms). Failures are logged as forensic audit records. |
| **Correlation tracking** | Every job event carries `CorrelationId` and `ParentExecutionId`. Serilog pushes `CorrelationId` into log scope. |
| **Job handler discovery** | `IBatchJobHandler<TJobEvent>` implementations are auto-discovered via assembly scanning in `AddBackgroundJobs()`. |

---

## 8. Messaging & Integration Rules

### 8.1 Notification Dispatch

| Rule | Description |
|------|-------------|
| **Non-blocking enqueue** | UI threads enqueue notifications via `NotificationChannelQueue.EnqueueAsync()`. Never call SMTP or HTTP senders inline. |
| **Background delivery** | `NotificationQueueWorker` (`BackgroundService`) drains the bounded channel and dispatches via `EmailNotificationSender` / `TeamsNotificationSender`. |
| **Channel backpressure** | The channel is bounded to 1,000 items with `BoundedChannelFullMode.Wait`. Monitor queue depth in production. |

### 8.2 Inbound Webhook Security

All incoming webhooks must:
1. Read the raw request body.
2. Extract the signature header (e.g., `X-Hub-Signature-256`, `Stripe-Signature`).
3. Validate via `IWebhookSignatureValidator.ValidateHmacSha256(body, header, secret)`.
4. Reject with `401 Unauthorized` if validation fails.

---

## 9. Caching Rules

| Rule | Description |
|------|-------------|
| **Surgical caching** | Cache only high-frequency, read-heavy data (tenant directories, project role lookups). Do not cache everything. |
| **Two-tier architecture** | L1 in-memory (15 min) + L2 distributed Redis (60 min) via `HybridCache`. |
| **Tenant key isolation** | All cache keys prefixed with `t:{tenantId}:`. Tenant-wide invalidation via `tenant:{tenantId}` tags. |
| **Invalidation discipline** | Invalidate cache entries immediately on write operations. Never rely on TTL expiry for correctness. |
| **Redis fallback** | If `ConnectionStrings:Redis` is not configured, the system falls back to `DistributedMemoryCache` for local development. |

---

## 10. Testing & CI/CD Rules

| Rule | Description |
|------|-------------|
| **No AI build commands** | AI assistants must **never** execute `dotnet build`, `dotnet compile`, or `dotnet run`. The developer builds and runs manually. |
| **NuGet CVE scanning** | `Directory.Build.props` enforces NuGet audit. Vulnerable packages break the build. |
| **DAST scanning** | OWASP ZAP baseline scans run weekly via GitHub Actions. |
| **Dependency updates** | Dependabot checks NuGet packages weekly and opens automated PRs. |

---

## 11. Code Review Checklist

Use this checklist when reviewing PRs against this template or any derived project:

- [ ] **Architecture**: New code respects layer boundaries (Core → Persistence → Infrastructure → Jobs → Web).
- [ ] **Tenancy**: New entities implement `ITenantEntity` or `IGlobalEntity`. No unlabeled entities.
- [ ] **Naming**: Entity classes end with `Entity`. Enums are in `DataListTypes/`.
- [ ] **Security**: User inputs pass through `IInputSanitizer`. No raw SQL with user interpolation.
- [ ] **Authorization**: Protected pages extend `AuthorizedPageComponentBase`. Write operations use resource-based AuthZ.
- [ ] **Error handling**: No stack traces or internal details exposed to users. Errors show `ERR-XXXXXXXX` correlation IDs.
- [ ] **EF Core**: Configurations use Fluent API only. No data annotation attributes for EF mapping.
- [ ] **Caching**: Cache keys use `t:{tenantId}:` prefix. Write operations invalidate caches immediately.
- [ ] **Logging**: Structured message templates with named parameters. No string interpolation in log calls.
- [ ] **Audit trail**: Significant operations generate Level 2/3 audit records. Sensitive fields are masked.
- [ ] **Performance**: Large table queries use keyset pagination. Bulk operations use `ExecuteDeleteAsync`/`ExecuteUpdateAsync`.
- [ ] **Secrets**: No secrets committed to source control. Production secrets in Azure Key Vault.

---

## 12. Architectural Paradigm: Middleware vs. MVC Filters vs. Blazor Circuit Handlers

A frequent point of confusion for .NET developers transitioning from traditional MVC/Web APIs to Blazor Interactive Server is whether to use **MVC Filters** (`IActionFilter`, `IResultFilter`, `IExceptionFilter`, `IResourceFilter`).

### Why MVC Controller Filters Do NOT Apply to Blazor Server

| Concept | Scope | Runs during Blazor UI interactions? | How it is implemented in this template |
|---|---|---|---|
| **HTTP Middleware** (`Program.cs`) | Global HTTP request/response pipeline | **Only on initial HTTP page load / assets** (HTML shell, static CSS/JS, health checks) | `UseSecurityHeaders`, `UseSerilogRequestLogging`, `UseRateLimiter`, `UseAntiforgery` |
| **MVC Controller Filters** (`IActionFilter`, etc.) | MVC Controller Action execution pipeline | ❌ **NEVER** — Blazor components do not execute MVC Controller actions | **Excluded** — No legacy MVC controllers in architecture |
| **Blazor Circuit Handlers** (`CircuitHandler`) | Persistent SignalR circuit lifecycle | ✅ **YES** — tracks circuit connection up/down, circuit opened/closed | `BlazorCircuitObservabilityHandler` in `Infrastructure` |
| **Error Boundaries** (`ErrorBoundary`) | Razor component render tree | ✅ **YES** — intercepts unhandled component exceptions in memory | `<ObservabilityErrorBoundary>`, `<AppErrorBoundary>` |
| **Component Role Contracts** | Compile-time component base class | ✅ **YES** — enforces roles on circuit navigation | `AuthorizedPageComponentBase` in `Components/Common/` |
| **Endpoint Filters** (`AddEndpointFilter`) | ASP.NET Core Minimal APIs | ✅ **Only on HTTP API routes** (e.g. webhook listeners) | Used if exposing Minimal API endpoints (`/api/...`) |

### Key Rule for Developers & AI Agents
- **Do NOT implement MVC Action or Result filters** for Blazor UI operations. User clicks, button presses, and form edits travel across the established SignalR WebSocket circuit directly to in-memory component event handlers.
- **For UI Authorization**: Use `AuthorizedPageComponentBase`, `<AuthorizeRouteView>`, and `IAuthorizationService`.
- **For UI Error Containment**: Use `<ObservabilityErrorBoundary>` and `<AppErrorBoundary>`.
- **For UI Observability**: Use `CircuitHandler` (`BlazorCircuitObservabilityHandler`).
- **For HTTP Level Security/Observability**: Use Middleware (`Program.cs`) for global HTTP requests, or Endpoint Filters for Minimal API endpoints.

---

## 13. File & Folder Reference

```
BlazorFluent/
├── BlazorFluent.slnx                           # .NET SLNX solution
├── Directory.Build.props                        # NuGet CVE audit enforcement
├── .github/
│   ├── dependabot.yml                           # Weekly NuGet dependency updates
│   └── workflows/owasp-zap-scan.yml             # Weekly OWASP ZAP DAST scan
│
├── BlazorFluent.Core/                           # Domain layer (zero dependencies)
│   ├── Common/                                  # Result monad, pagination, query filters
│   ├── Constants/                               # Role constants
│   ├── Contracts/                               # All service interfaces
│   ├── DataListTypes/                            # All shared enums
│   ├── Domain/                                  # Entities, Base abstractions, value objects
│   ├── Events/                                  # Job event contracts
│   └── Validation/                              # FluentValidation validators
│
├── BlazorFluent.Persistence/                    # Data access layer
│   ├── Configurations/                          # EF Core Fluent API configurations
│   ├── Context/                                 # AppDbContext
│   ├── Extensions/                              # LINQ query extensions (LeftJoin)
│   ├── Interceptors/                            # Audit + Security SaveChanges interceptors
│   ├── Services/                                # Data services (UnitOfWork, TenantService, etc.)
│   └── PersistenceExtensions.cs                 # DI registration
│
├── BlazorFluent.Infrastructure/                 # Integration & middleware layer
│   ├── Notifications/                           # Email/Teams senders, Channel queue, worker
│   ├── Observability/                           # Circuit lifecycle handler
│   ├── Security/                                # CSP headers, AuthZ handlers, HMAC validator, XSS sanitizer
│   ├── Storage/                                 # ProtectedLocalStorage form service
│   ├── UI/                                      # Responsive breakpoint service
│   └── InfrastructureExtensions.cs              # DI registration
│
├── BlazorFluent.Jobs/                           # Background processing layer
│   ├── Abstractions/                            # IBatchJobHandler, IJobEventQueue
│   ├── Jobs/                                    # Job handlers (Audit/, Catalog/)
│   ├── Listeners/                               # Channel consumer with retry policy
│   ├── Queue/                                   # Channel-based event queue
│   ├── Schedulers/                              # Cronos periodic scheduler
│   ├── Services/                                # JobManagerService
│   └── JobsExtensions.cs                        # DI registration
│
└── BlazorFluent (Web)/                          # Presentation & composition root
    ├── Components/
    │   ├── Common/                              # Reusable components (PageHeader, ConfirmDialog, etc.)
    │   ├── Layout/                              # MainLayout, NavMenu, NotificationBell, TenantSwitcher
    │   └── Pages/                               # Route pages (Home, Login, Error, NotFound)
    ├── Infrastructure/Security/                 # AppCurrentUser, AuthenticationStateProvider
    ├── Modules/                                 # Feature slice pages (Auditing, Catalog, Jobs, Tenancy)
    ├── wwwroot/                                 # Static assets (CSS, JS, favicon)
    ├── Program.cs                               # Composition root
    └── appsettings.json                         # Configuration
```

---

> **Last Updated**: September 2026 | **Target Framework**: .NET 10 | **UI**: Microsoft Fluent UI Blazor V5
