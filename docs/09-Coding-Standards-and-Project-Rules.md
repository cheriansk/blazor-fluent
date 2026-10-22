# 09. Coding Standards & Project Rules

> **Target Audience**: Human Developers, Tech Leads, Code Reviewers, and Architects contributing to this codebase.  
> **Key Objective**: Establish the authoritative coding standards, Clean Architecture laws, captive dependency rules, and modern C# 13 / .NET 10 conventions for the engineering team.
>
> *(Note: For AI assistant behavioral guardrails and operational rules, see [10. AI Agent Governance & Operational Rules](file:///c:/Users/cheri/source/repos/BlazorFluent/docs/10-AI-Agent-Governance-and-Operational-Rules.md). This document aligns with the root [`PROJECT_RULES.md`](file:///c:/Users/cheri/source/repos/BlazorFluent/PROJECT_RULES.md)).*

---

## 🏛️ 1. Clean Architecture Laws

1. **Strict Inward Dependency Flow**:
   - `BlazorFluent.Core` has **zero** internal project references. It contains only pure business contracts, entities, and enums.
   - `BlazorFluent.Persistence` references `Core` and `Shared`. It must never reference `Infrastructure` or UI projects.
   - `BlazorFluent.Infrastructure` handles external protocols (SMTP, Teams, SignalR, HTTP Engine Client) and must never reference `Persistence`.
   - `BlazorFluent.Jobs` contains all background queue listeners, batch handlers, and file import pipelines.
   - `BlazorFluent.Web` is the interactive Blazor UI composition root.
   - `BlazorFluent.Api` is the headless REST/minimal API processing engine.
   - `BlazorFluent.Worker` is a thin headless console host shell (.NET 10 Worker SDK). **Exactly zero batch handlers or business logic belong in `BlazorFluent.Worker`**; all job processing code resides in `BlazorFluent.Jobs`.

2. **Persistence Isolation**:
   - 100% of database access belongs in `BlazorFluent.Persistence`.
   - No direct `DbContext` queries inside UI components or Razor pages. Always call an injected domain service interface.

---

## 🔒 2. Service Scope Lifetime & Captive Dependency Governance

| Lifetime | Permitted Registrations | Critical Rules |
| :--- | :--- | :--- |
| **`Scoped`** | `AppDbContext`, domain services (`ITenantService`), EF interceptors, `ICurrentUser`. | Created once per Blazor circuit or HTTP request. **Never inject a Scoped service into a Singleton!** |
| **`Singleton`** | In-memory queues (`ChannelJobEventQueue`), time providers, stateless sanitizers, `ITenantTokenService`. | Must be strictly thread-safe. If a background Singleton needs `AppDbContext`, it **MUST** inject `IServiceScopeFactory` and create an explicit scope: `using var scope = _scopeFactory.CreateScope();`. |
| **`Transient`** | FluentValidation validators, formatting utilities, delegating handlers (`TenantAuthDelegatingHandler`). | Stateless and lightweight. |

### Fail-Fast Container Enforcement
In `Program.cs`, container validation is enabled across all environments (`ValidateScopes = true`, `ValidateOnBuild = true`). Any captive dependency causes an immediate build/startup exception.

---

## 🛡️ 3. Multi-Tenancy & Authorization Rules

1. **Fail-Closed Multi-Tenancy**:
   - All tenant-scoped entities must implement `ITenantScopedEntity`.
   - Never accept `TenantId` from untrusted client form inputs. The service layer must overwrite `entity.TenantId = currentUser.TenantId`.
   - Never bypass EF Core query filters without documented, audited justification.

2. **Dual-Gate Action Authorization**:
   - UI pages enforce visit/edit permissions via `AuthorizedPageComponentBase`.
   - In addition, domain services must call `EnsureCanEditAsync(projectId)` before executing mutations.

3. **Zero-Trust Multi-Tenancy Isolation & S2S Token Security**:
   - **Dual-Token Architecture**:
     - *Interactive Users*: Standard JWT bearer tokens authenticated via the centralized Cloud Master Key.
     - *Background/S2S Daemons*: Ephemeral JWT tokens (5-minute lifetime) authenticated via tenant-specific license keys (`Tenants:{tenantId}:LicenseKey`).
   - **Dynamic Key Resolution**: `BlazorFluent.Api` relies on `IssuerSigningKeyResolver` to dynamically fetch the tenant's symmetric key. Falling back to a shared system key on tenant mismatch is strictly prohibited.
   - **Opaque Reference Slugs & Data Privacy**:
     - Token claims MUST NOT expose raw database tenant IDs or user PII.
     - Use `OpaqueIdUtility.GenerateTenantSlug(tenantId)` for public `tid` claims (`tnt_...`) and `OpaqueIdUtility.MaskGuid` with per-tenant salt for sub/actor masks.
   - **Zero Shared Key Fallback**: If a tenant's license key is missing or unconfigured, the operation must immediately fail-closed with an `InvalidOperationException`.
   - **Cross-Tenant Quarantine Guards**: All background job handlers (`GenericFileImportBatchJobHandler`, `BatchJobQueueListener`) must compound-query by `(JobId, TenantId)` and immediately quarantine records if payload tenant IDs do not match the execution context.

4. **Enterprise Strict Content Security Policy (CSP)**:
   - **Zero `'unsafe-inline'` / `'unsafe-eval'` in `script-src`**: Never weaken `script-src` with inline script execution or eval.
   - **Mandatory Nonce Tagging**: All newly authored `<script>`, `<link rel="stylesheet">`, and `<ImportMap>` tags must carry the per-request cryptographic nonce (`nonce="@nonce"`).
   - **Shadow DOM Style Compliance**: Avoid dynamic runtime injection of un-nonced `<style>` elements into `<head>`. Rely on Microsoft Fluent UI Blazor V5 Web Components and encapsulated shadow root stylesheets (`adoptedStyleSheets`).

---

## 💻 4. Modern C# 13 & .NET 10 Idioms

- **File-Scoped Namespaces**: Always use file-scoped namespaces (`namespace BlazorFluent.Core.Services;`).
- **Primary Constructors**: Use primary constructors for dependency injection:
  ```csharp
  public class CustomerService(AppDbContext db, ICurrentUser currentUser, ILogger<CustomerService> logger) : ICustomerService
  ```
- **Nullability**: Nullable reference types are enabled (`<Nullable>enable</Nullable>`). Avoid null-forgiving operators (`!`) unless operating on EF Core generated keys.
- **Pattern Matching & Expressions**: Prefer modern switch expressions and pattern matching for domain state transitions.

---

## 🏷️ 5. DataList Enum Naming & Governance Law (`*Enum` Suffix)

To maximize cognitive readability and eliminate ambiguity during mental code parsing, all enumeration types must follow strict naming rules:

1. **The `*Enum` Suffix Law**:
   - All newly created enumeration types in `BlazorFluent.Shared.DataListTypes` must be explicitly suffixed with **`Enum`** (e.g., `WorkflowPhaseEnum`, `ContractTierEnum`, `DeliverableStatusEnum`).
   - In code review, seeing `WorkflowPhaseEnum.Draft` instantly signals to the brain that the identifier is an enum, without confusing it with a class, record, or interface.

2. **Prohibited Suffixes**:
   - **`Lst` / `List` is strictly prohibited**: In .NET, `Lst` or `List` signifies a collection (`List<T>`). An enum is a scalar discrete value, never a collection.
   - **`Dtl` / `Detail` is strictly prohibited**: In enterprise architecture, `Dtl` signifies a detail entity, table, or master-detail DTO (e.g. `InvoiceDtl`).

3. **Legacy Grandfathering**:
   - The 38 pre-existing core enums (e.g., `ProjectRole`, `TaskStatus`, `AuditEventType`, `AuditSeverity`) are grandfathered to preserve database schema stability, EF Core mappings, and existing unit tests.

4. **Compile-Time Governance (`TG006`)**:
   - Enforced by the `TemplateGuard.Analyzers` Roslyn package rule **`TG006`**:
     ```
     Error TG006: Enum 'DeliverableStatus' in namespace 'BlazorFluent.Shared.DataListTypes' 
     does not end with 'Enum' (e.g. 'DeliverableStatusEnum'). For cognitive readability, 
     all new enums in BlazorFluent.Shared.DataListTypes must end with the 'Enum' suffix.
     ```
   - Any new enum lacking the `*Enum` suffix fails `dotnet build` with an analyzer compilation error.

---

## ⏰ 6. Temporal & Date/Time Architecture Law: Prohibition of `DateOnly`

1. **The `DateOnly` Prohibition**:
   - `DateOnly` is **strictly forbidden** in all domain entities, DTOs, API contracts, background jobs, and UI models.
   - **The Timezone Flaw**: A civil date like `2026-10-24` begins and ends at different absolute instants across the globe. At `2026-10-24 23:00:00 UTC`, a user in New York is at `7:00 PM` (still working), while Tokyo is already on `Oct 25`. Storing as `DateOnly` causes background jobs to prematurely expire tasks or revoke user access while users in western timezones still have hours remaining in their workday.
   - Furthermore, `DateOnly` cannot support hourly calendar appointments (`BaseCalendarEntity`) or exact SLA cutoff times.

2. **The Dual Temporal Classification**:
   - **Class A: Business & Effective Dates** (`StartDate`, `EndDate`, `DueDate`, `ResolveByUtc`):
     - Stored as `DateTime` (UTC).
     - Compared by `.Date` across rules engines, batch handlers, and validators.
     - Displayed in UI using `Format="yyyy-MM-dd"`.
   - **Class B: Operational & Forensic Timestamps** (`Created`, `Updated`, `DeletedAtUtc`, `StartedAtUtc`, `CompletedAtUtc`, `LastActivityAtUtc`):
     - Stored as `DateTime` (UTC).
     - Compared with full timestamp precision.
     - Displayed in UI using `Format="yyyy-MM-dd HH:mm:ss"` via `IUserTimeZoneService`.

3. **Database & LINQ Query Standard**:
   - Always map to PostgreSQL `timestamp with time zone` (`timestamptz`).
   - In LINQ queries, use half-open range bounds to preserve B-tree index scans:
     ```csharp
     var targetDate = (asOfUtc ?? DateTime.UtcNow).Date;
     var nextDay = targetDate.AddDays(1);
     return query.Where(e => e.StartDate < nextDay && (e.EndDate == null || e.EndDate >= targetDate));
     ```


---

## 🔗 7. Relational Normalization (3NF) & `[NotMapped]` Navigation Law

1. **Minimal Persisted Data Copying (Single Source of Truth)**:
   - Consuming domain entities (`TenantUserEntity`, `ProjectUserRoleEntity`, `ProgramUserRoleEntity`, `KnowledgeArticleEntity`, `KnowledgeArticleReviewerEntity`, `UserTaskCommentEntity`, etc.) must persist **only foreign keys (`Guid`)** to related entities.
   - Never persist redundant copies of mutable attributes (`UserName`, `UserEmail`, `UserType`, `AuthorName`, `AuthorEmail`, `Title`) on consuming tables. Updating a user's `FullName` or `Email` in `UserEntity` must be contained strictly within `identity.Users` with zero cross-table synchronization.

2. **Unlimited `[NotMapped]` Convenience Properties**:
   - Entities may define any number of `[NotMapped]` computed properties that delegate to their EF Core navigation property for UI data binding and DTO projection:
     ```csharp
     [NotMapped]
     public string UserId => UserEntityId.ToString();

     [NotMapped]
     public string UserEmail => User?.Email ?? string.Empty;

     [NotMapped]
     public string UserName => !string.IsNullOrWhiteSpace(User?.FullName) ? User.FullName : (User?.Email ?? string.Empty);
     ```

3. **EF Core Query & Validator Discipline**:
   - **Never use `[NotMapped]` properties in SQL `Where` or `OrderBy` clauses**: Filter and sort using the persisted foreign key (`r.UserEntityId`) or navigation property (`r.User!.Email`, `r.User!.FullName`), and chain `.Include(r => r.User)` when reading entities for UI/DTO consumption.
   - **Validate Persisted FKs Only**: Tier 2 `AbstractValidator<TEntity>` classes must validate `RuleFor(x => x.UserEntityId).NotEmpty()` rather than `[NotMapped]` properties.
   - **Forensic Ledger Exemption**: Only immutable point-in-time forensic logs (`AuditRecordEntity`, `EventPublishTrackerEntity`) and ephemeral circuit sessions (`UserSessionEntity`) snapshot point-in-time strings.
