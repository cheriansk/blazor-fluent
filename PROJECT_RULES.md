# BlazorEnterprise — Project Rules & Best Practices

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
- [ ] Delete sample entities (`ProductEntity`, `CatalogDashboardPage`, `CatalogSyncJobHandler`, `WeatherPage.razor`, `CounterPage.razor`) once your domain entities are in place.
- [ ] Update `README.md` with your project's description, setup instructions, and deployment guide.

---

## 1. Architecture Rules

### 1.1 Clean Architecture Layer Boundaries

```
BlazorEnterprise.Core          → Domain entities, contracts, enums, value objects, validation, OpaqueIdUtility
BlazorEnterprise.Persistence   → EF Core DbContext, configurations, interceptors, migrations, data services
BlazorEnterprise.Infrastructure→ S2S token services, outbound senders (email/Teams), security middleware, observability
BlazorEnterprise.Jobs          → Background job handlers, schedulers, event queues (all batch logic resides here)
BlazorEnterprise.Worker        → Thin background console host shell (executes queue listeners & schedulers; 0 batch logic)
BlazorEnterprise.Shared.UI      → Reusable enterprise UI components, icons, theme tokens, and wizard framework RCL
BlazorEnterprise (Web)         → Razor components, pages, layouts, Program.cs composition root
```

| Rule | Description |
|------|-------------|
| **Dependency direction** | Dependencies flow inward only. `Core` has zero internal project references. |
| **Persistence isolation** | `BlazorEnterprise.Persistence` contains 100% database persistence code. No HTTP clients, no SMTP, no file I/O. |
| **Infrastructure isolation** | `BlazorEnterprise.Infrastructure` handles outbound integrations, S2S auth delegation, and browser interop services. Never references `Persistence`. |
| **Worker shell isolation** | `BlazorEnterprise.Worker` is strictly an executable console host shell (~60 lines). Never author batch handlers or business logic directly in `BlazorEnterprise.Worker`. All batch logic must live in `BlazorEnterprise.Jobs`. |
| **No circular references** | `Core` ← `Persistence`, `Core` ← `Infrastructure`, `Core` + `Persistence` ← `Jobs`, All + `Shared.UI` ← `Web`, All except `Web`/`Shared.UI` ← `Worker`. |
| **Abstractions in Core** | Every service contract (`I*Service`, `I*Sender`, `I*Validator`) lives in `BlazorEnterprise.Core/Contracts/`. Implementations live in `Persistence`, `Infrastructure`, `Jobs`, or `Shared.UI`. |

### 1.2 Service Scope Lifetime & Captive Dependency Governance

| Lifetime | Scope Definition | Applicable Service Types |
|----------|------------------|--------------------------|
| **Scoped (`AddScoped`)** | Created once per HTTP request or Blazor circuit scope. | `AppDbContext`, EF Core interceptors, domain services (`ITenantService`, `IAuditService`, `IProjectAuthorizationService`, `IUserSessionService`, `IImpersonationService`), repositories, UI authentication state providers (`ICurrentUser`, `CurrentUserAuthenticationStateProvider`). |
| **Singleton (`AddSingleton`)** | Created once for the host lifetime. Must be thread-safe. | In-memory queues (`NotificationChannelQueue`, `ChannelJobEventQueue`), stateless sanitizers/validators (`HtmlInputSanitizer`, `HmacWebhookSignatureValidator`), time providers (`ConfigurableDateTimeProvider`). |
| **Transient (`AddTransient`)** | Created new every time requested. Stateless. | FluentValidation validators (`AddValidatorsFromAssemblyContaining`), lightweight calculation helpers. |

#### Captive Dependency Rules & Enforcement
1. **Never Inject Scoped into Singleton**: A `Singleton` service MUST NEVER directly inject a `Scoped` service or `AppDbContext`. This creates a captive dependency where the Singleton holds onto a single DbContext instance forever, causing multithreaded concurrency crashes (`"A second operation was started on this context instance"`).
2. **IServiceScopeFactory Pattern for Background Services**: Long-lived background services (`BackgroundService`, `IHostedService`, background job consumers) needing `AppDbContext` or Scoped services MUST inject `IServiceScopeFactory` and create a fresh scope (`using var scope = _scopeFactory.CreateScope()`) for each unit of work or loop iteration.
3. **Fail-Fast Container Validation**: `Program.cs` enforces `options.ValidateScopes = true` and `options.ValidateOnBuild = true` across **ALL** environments (Development, Staging, Production) so captive dependencies trigger immediate startup failures rather than runtime concurrency bugs in production.

### 1.3 Service-Layer Read & Write Authorization Rules
1. **Never Rely on `[Authorize]` Alone**: Authorization must be enforced at both the UI page contract level (`AuthorizedPageComponentBase` with `MinimumVisitRole` / `MinimumEditRole`) AND the domain service layer.
2. **Fail-Closed Service-Layer Gates**: Domain services (`UserTaskService`, `ProjectAuthorizationService`) MUST inject `IProjectAuthorizationService` and call:
   - `await _projectAuth.EnsureCanVisitAsync(projectId, operation)` on all **READ** methods (`GetTasksByProjectAsync`, `GetTaskByIdAsync`, `GetCommentsAsync`).
   - `await _projectAuth.EnsureCanEditAsync(projectId, operation)` on all **WRITE** methods (`CreateTaskAsync`, `UpdateTaskAsync`, `CloseTaskAsync`, `ReopenTaskAsync`, `AddCommentAsync`).
   - Unauthenticated or unauthorized reads log security events into PostgreSQL `audit.AuditRecords` and throw `UnauthorizedAccessException` before querying data.

### 1.4 Global `NoTracking` Query Behavior & Mutation Rule
1. **Default `NoTracking` Behavior**: `AppDbContext` is configured with `options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)` globally in `PersistenceExtensions.cs`. All LINQ queries execute with no tracking by default for maximum read performance and minimal memory footprint.
2. **Explicit `.AsTracking()` for Entity Mutations**: Any domain service method that retrieves an existing entity to modify its properties and call `SaveChangesAsync()` MUST chain `.AsTracking()` onto the query (e.g. `_dbContext.Users.AsTracking().FirstOrDefaultAsync(...)`).

### 1.5 Entity Architectural Standards & Roslyn Analyzer Governance (TG007 - TG012)

Every domain entity and abstract entity base class in `BlazorEnterprise.Core` **must strictly follow the solution entity laws**, enforced automatically at compile time via Roslyn Analyzer rules in `TemplateGuard.Analyzers`:

| Rule Code | Law & Requirement | Allowed Approach & Enforcement |
|-----------|-------------------|--------------------------------|
| **`TG007`** | **Entity Naming Law** | Every domain entity class and entity base class must end with the `Entity` suffix (e.g. `TaskEntity`, `TenantEntity`). Enforced on all types inheriting `BaseEntity` or declared in domain namespaces. |
| **`TG008`** | **Inheritance & Marker Interfaces Law** | (1) Must inherit `BaseEntity` (directly or via `AuditableEntity` / `TenantAuditableEntity`);<br/>(2) Must implement tenancy boundary: `ITenantEntity` (tenant-scoped) or `IGlobalEntity` (host-wide);<br/>(3) Must implement exactly ONE deletion lifecycle marker: `ISoftDeletableEntity`, `IHardDeletableEntity`, or `IImmutableEntity`;<br/>(4) If implementing `IProjectScopedEntity`, MUST also implement `ITenantEntity`. |
| **`TG009`** | **Companion Validator Law** | Every concrete domain entity must declare a companion validator class in `BlazorEnterprise.Core` implementing `AbstractValidator<TEntity>` (e.g. `class TaskEntityValidator : AbstractValidator<TaskEntity>`). |
| **`TG010`** | **Companion EF Core Configuration Law** | Every concrete domain entity mapped in the database must declare a companion configuration class in `BlazorEnterprise.Persistence` implementing `IEntityTypeConfiguration<TEntity>` (e.g. `class TaskEntityConfiguration : IEntityTypeConfiguration<TaskEntity>`). |
| **`TG011`** | **Strict Prohibition of `DateOnly`** | Domain entities must never declare properties of type `DateOnly` or `DateOnly?`. All temporal fields must be declared as UTC `DateTime`. |
| **`TG012`** | **Relational Normalization & Navigation Law** | Domain entities must persist foreign keys (`Guid`) only. Never persist mutable user attributes (`UserEmail`, `UserName`, `AuthorEmail`, `AuthorName`); convenience forwarders must be decorated with `[NotMapped]`. |

| ✅ Correct | ❌ Wrong |
|-----------|---------|
| `TaskEntity` | `Task` |
| `TenantAuditableEntity` | `TenantAuditable` |
| `UserSessionEntity` | `UserSession` |

### 1.2 Entity Architecture & Developer Decision Guide

When creating a new entity in `BlazorEnterprise.Core/Domain/`, follow this 3-step decision framework:

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
| **Scheduled Work & Delivery Tracking** | `IScheduledWorkEntity` | Inherits `IEffectiveDatedEntity`; declares `DueDate`, `RevisedDueDate?`, `ActualStartDate?`, `ActualEndDate?`, `ScheduleVarianceDays`. Interceptor auto-stamps actuals on status transitions and enforces dual-gate role governance on baseline schedule adjustments |
| **Revision / Version Tracking** | `IVersionedEntity` | Adds `VerNum` and `SetVersionNumber(newVer)`. Interceptor validates `VerNum >= 1`. Versions are explicitly controlled via domain logic, not blind auto-increments |
| **Skip Audit Diff Logging** | `IAuditExemptEntity` | Prevents recursive diff logging in `audit.AuditRecords` |
| **Skip Mandatory Validation** | `IValidationExemptEntity` | Explicitly exempts internal logging/system tables from mandatory `IValidator<TEntity>` checks |

#### Common Entity Scenarios Quick Reference Table
| Entity Scenario | Base Class | Marker Interfaces |
|---|---|---|
| **Standard Business Entity** (Products, Customers, Orders) | `TenantAuditableEntity` | `ISoftDeletableEntity` |
| **Project Workspace Entity** (Tasks, Documents, Features) | `TenantAuditableEntity` | `IProjectScopedEntity`, `ISoftDeletableEntity` |
| **Effective-Dated Contract / Pricing** | `TenantAuditableEntity` | `IEffectiveDatedEntity`, `IVersionedEntity`, `ISoftDeletableEntity` |
| **Company Tenant Entity** | `AuditableEntity` | `IGlobalEntity`, `IEffectiveDatedEntity` |
| **User Account Entity** | `AuditableEntity` | `IGlobalEntity`, `ISoftDeletableEntity` |
| **System Audit Record / Job Execution** | `AuditableEntity` / `TenantAuditableEntity` | `IAuditExemptEntity`, `IValidationExemptEntity` |

### 1.3 DataListTypes Enum Standard

All shared domain enums live in `BlazorEnterprise.Shared/DataListTypes/`. Never declare shared enums in `Contracts/`, `Common/`, or `Constants/`.

| Standard | Rule & Implementation |
|---|---|
| **`*Enum` Suffix Law** | All enums in `BlazorEnterprise.Shared.DataListTypes` **MUST** end with the `Enum` suffix (e.g. `WorkflowPhaseEnum`, `ContractTierEnum`, `DependencyNodeTypeEnum`, `JobTriggerSourceEnum`) for immediate cognitive recognition in code review. Suffixes `Lst`/`List` (misleading collection indicator) and `Dtl`/`Detail` (misleading DTO/entity indicator) are strictly prohibited. Enforced at compile-time by Roslyn Analyzer rule **`TG006`**. |
| **Enum Taxonomy Law (`TG014`)** | Every enum in `BlazorEnterprise.Shared.DataListTypes` must be explicitly decorated with either `[LifecycleEnum]` (state machine / progression lifecycle) or `[ClassificationEnum]` (descriptive attribute, category, or role). Enforced at compile-time by Roslyn Analyzer rule **`TG014`**. |
| **Category-Scoped Lifecycle Transition Law (`TG015`)** | Every enum decorated with `[LifecycleEnum]` must declare companion definitions containing an `AllowedTransitions` dictionary, scoped by Category where multiple entity workflows share an enum (e.g. `AllowedTransitions[Categories.Task]` vs `AllowedTransitions[Categories.Risk]`). State mutations must execute through entity domain transition methods (`entity.TransitionTo()`, `task.StartWork()`, `task.Complete()`). Enforced at compile-time by Roslyn Analyzer rule **`TG015`**. |
| **Mandatory Extension Method Law (`TG013`)** | Direct calls to `Enum.TryParse`, `Enum.Parse`, `Enum.GetValues`, or `Enum.GetNames` are strictly prohibited across all code. All enum parsing, conversion, and metadata lookups must use `DataListExtensions` methods (`.ToEnum<T>()`, `.TryToEnum<T>()`, `.ToEnumOrNull<T>()`, `.GetDisplayName()`, `.GetCode()`, `.ToInt()`). Enforced at compile-time by Roslyn Analyzer rule **`TG013`**. |
| **Multi-Category Membership** | `[DataListCategory]` supports `AllowMultiple = true`. Enums shared across multiple entity subtypes (e.g. `Open` and `Closed` shared by Tasks, SubTasks, and Risks) declare multiple category tags using strongly-typed companion constants (e.g. `[DataListCategory(Categories.Task)]`). |
| **Strongly-Typed `<DataListSelect>` UI Standard** | Never author raw `<FluentSelect>` with manual `.ToString()` / `v.ToEnum(...)` for domain enums. Use `<DataListSelect TEnum="..." TValue="..." Label="..." @bind-Value="..." />`. When passed `Category="..."` or `CurrentState="..."`, it automatically filters dropdown choices to valid moves in the transition matrix. |
| **Clean Code Identifiers** | Enum entries use alphanumeric codes with zero spaces or symbols (e.g. `ClientUser`, `CompanyUser`, `Admin`). This code is used in logic and database persistence. |
| **Mandatory Display & Optional Description** | Every enum member, Category, and Filter declares `[Display(Name = "...", Description = "...")]` with mandatory `Name` and optional `Description`. Never use display names for logic comparisons. Retrieve via `.GetDisplayName()` and `.GetDescription()`. |
| **Companion Definition Classes** | Enums define structured companion classes in the same file (e.g. `{EnumName}Definitions.Categories`, `{EnumName}Definitions.Filters`) declaring `const string` codes with `[Display]`. |
| **Code-Only Attribute Signature** | `[DataListCategory(Definitions.Categories.Code)]` and `[DataListFilterCriterias(Definitions.Filters.Code)]` accept **only the constant code**, eliminating string repetition across members. |
| **Rich Metadata Resolution** | `DataListExtensions` inspects companion definition classes via cached reflection ($O(1)$) to link each enum member to its `CategoryInfo` and `FilterInfo` records (resolving `Code`, `DisplayName`, and `Description`). |
| **UI Projection & Filtering** | Use `DataListExtensions.GetDataListItems<TEnum>()` (or `enumVal.GetDataListItems()`) for unfiltered items, and `GetFilteredDataListItems<TEnum>(criteria)` (or `enumVal.GetFilteredDataListItems(criteria)`) for filtered items. In `<FluentSelect>`, bind `TValue="string"` with `OptionText="@(i => i.DisplayName)"` and `OptionValue="@(i => i.Code)"` to display user-friendly names while persisting raw enum codes. |

### 1.4 Primary Keys

All entities use **sequential UUIDv7** via `Guid.CreateVersion7()` in `BaseEntity`. Never use auto-increment integers or random GUIDs.

### 1.5 Consolidated 2-Tier Validation Architecture & Fail-Closed Guard

Validation is strictly organized into two distinct, intuitive tiers to prevent confusion and folder scattering:

| Tier | Purpose | Location | Target Type | How it Fires |
|---|---|---|---|---|
| **Tier 1: Page / Feature Form** | Instant UI field-level red error feedback | `BlazorEnterprise.Core/Validation/<Module>/` | Request / Form DTO | Automatically via Blazor `<EditForm>` with `<FluentValidationValidator />` |
| **Tier 2: Entity Invariant Shield** | Fail-closed database integrity guard | `BlazorEnterprise.Core/Domain/<Module>/` | Domain Entity POCO | Automatically via EF Core `EntityValidationInterceptor` during `SaveChangesAsync` |

#### Architectural Invariant Rules:
1. **The Single-Entity Invariant Rule**: A Tier 2 Entity Validator must **only inspect properties of its own entity**. It is strictly **forbidden** to inject `DbContext`, call repositories, or reference properties of external entities.
2. **Fail-Closed Startup Safety**: At application launch, `AppDbContext` inspects all mapped entities. If any concrete entity lacks an `IValidator<TEntity>` and does **not** implement `IValidationExemptEntity`, startup terminates immediately with an `InvalidOperationException` detailing the missing validator.
3. **Zero-Reflection Performance**: `EntityValidationInterceptor` caches closed `IValidator<TEntity>` types in a static `ConcurrentDictionary`, resolving validators in under 1 microsecond.
4. **Batch Error Aggregation**: If any entities fail validation during `SaveChangesAsync()`, all errors across all entities in the commit batch are aggregated into a structured `EntityValidationException`, and the transaction is aborted before any SQL is sent to PostgreSQL.

### 1.6 Project Task Management & Reusable Swimlane Architecture

The Task Management subsystem implements project-scoped task tracking, Jira-style comments, automated notifications, and Microsoft Teams daily summaries:

1. **Entity Models & Scoping**:
   - `UserTaskEntity` inherits `TenantAuditableEntity`, implementing `IProjectScopedEntity` and `ISoftDeletableEntity`. It stores `Title`, `Description`, `Priority` (`TaskPriority`), `Status` (`TaskStatus`), `DueDate`, `AssigneeEmails` (semicolon-separated), `Labels` (comma-separated), `IsClosed`, `ClosedAtUtc`, and child `Comments`.
   - `UserTaskCommentEntity` inherits `TenantAuditableEntity`, capturing discussion threads with `AuthorUserEntityId` (FK to `UserEntity`), `CommentText`, and `[NotMapped]` convenience forwarders (`AuthorUserId`, `AuthorName`, `AuthorEmail`).
   - Both entities use PostgreSQL schema `tasks` (`tasks.Tasks`, `tasks.TaskComments`) and implement Tier 2 single-entity validators (`UserTaskEntityValidator`, `TaskCommentEntityValidator`).
2. **Reusable Generic Swimlane Component**:
   - `<FluentSwimlaneBoardComp<TItem>>` (`Components/Common/FluentSwimlaneBoardComp.razor`) provides a generic multi-column kanban/swimlane view configured via `SwimlaneColumn<TItem>` definitions.
   - Accepts custom card render fragments (`@CardTemplate`), column badge counters, and click callbacks (`OnItemClick`), with zero drag-and-drop fragility.
3. **Automated Assignee Notifications**:
   - When a task is created, `ITaskService.CreateTaskAsync` dispatches in-app notifications (`INotificationSender.SendAsync`) to all assigned emails.
4. **Daily Microsoft Teams Channel Summary**:
   - `DailyTaskSummaryJobHandler` executes daily (at 18:00 UTC via `PeriodicBatchScheduler`).
   - Gathers tasks across all active projects grouped into Past Due, Due Today, Due Tomorrow, and Due in 2 Days.
   - Formats a Markdown summary table and dispatches it to the project's configured webhook (`ProjectEntity.TeamsWebhookUrl`) or global fallback.

### 1.7 File Import, Client-Side Encryption & Modular Batch Processing Pipeline

The File Ingestion & Batch Processing subsystem implements enterprise-grade, asynchronous file processing:

1. **Defense-in-Depth Cryptography**:
   - File streams are encrypted client-side in C# using hardware-accelerated **AES-256-GCM** prior to network egress or storage.
   - Unique 256-bit AES keys per tenant are dynamically derived using **HKDF-SHA256** (`ITenantEncryptionService`), guaranteeing zero-knowledge confidentiality.
2. **Hierarchical Batch Storage**:
   - `ITenantBlobStorageService` organizes batch blobs into a dedicated folder per batch: `tenants/{tenantId}/projects/{projectId}/imports/{yyyy}/{MM}/batch-{importId}/{fileId}{extension}`.
   - Production connects to Azure Blob Storage; development seamlessly falls back to isolated local encrypted directories.
3. **Atomic Multi-File Batch Ingestion**:
   - Single or bulk files are uploaded with a shared `ImportId` (Batch GUID).
   - If any file fails upload or encryption, the entire batch transaction aborts and uploaded blobs are purged (fail-closed rollback).
   - Once all files in the batch are persisted to `imports.ImportFiles`, `FileImportBatchJobEvent(ImportId, ProjectId, TenantId)` is emitted.
4. **Generic Batch Worker & 2-Phase Ingestion**:
   - `GenericFileImportBatchJobHandler` listens for `FileImportBatchJobEvent`, retrieves all files for `ImportId`, and coordinates two discrete phases per file via auto-discovered handlers:
     1. **Validate & Stage (`ICanonicalBatchStager`)**: `GenericTabularJsonBatchStager` decrypts the stream, parses the payload against declarative `FileTemplateConfig` rules (fail-closed), and inserts 1,000-row canonical JSON chunks (`PayloadJson` as `jsonb`) into the universal staging table (`staging.StagedImports` / `StagedImportEntity`) tagged with `ImportId`. Multi-sheet workbooks iterate across all configured sheets in `config.Sheets`, fail-closed if unknown worksheets exist, and chunk rows per-sheet with `SheetName` and independent `ChunkIndex` sequences. If validation fails, status becomes `ValidationFailed`, error diagnostics are recorded, and staging halts with 0 rows staged.
     2. **Domain Promotion (`IEngineClient.ProcessStagedBatchAsync`)**: Compresses staged JSON chunks from `staging.StagedImports`, processes them through the transform engine strictly in sheet declaration order followed by `ChunkIndex`, and atomically commits live domain entities (e.g. `TaskEntity` linked via `ISourceTrackedEntity.SourceRowId`). On completion, status transitions to `Completed`.
5. **Zero-Migration Declarative Extensibility (`config/file-templates/*.json`)**:
   - Each import format is defined declaratively via `FileTemplateConfig` (`JsonFileTemplateConfigRepository`) and staged into the universal `staging.StagedImports` table (`StagedImportEntity`), requiring zero per-feature staging tables and zero database migrations.



   - Handlers are auto-discovered and registered in DI via assembly scanning (`JobsExtensions.cs`). Adding a new filetype requires zero DI modifications and zero runner changes.

### 1.8 Temporal & Date/Time Architecture: Strict Prohibition of `DateOnly`

| Standard | Rule & Architectural Law |
|---|---|
| **`DateOnly` is Strictly Prohibited** | Never use `DateOnly` in entities, DTOs, domain services, background jobs, or UI models. `DateOnly` lacks timezone offsets, making it ambiguous across global multi-tenant timezones and causing premature expiration bugs in background workers. |
| **Mandatory UTC `DateTime` Storage** | All dates and timestamps MUST be declared as `DateTime` and stored in UTC (`DateTimeKind.Utc` / PostgreSQL `timestamptz`). |
| **Business / Effective Date Comparisons (`.Date`)** | For business validity windows, effective dates, task due dates, and user access windows (`StartDate`, `EndDate`, `DueDate`, `ResolveByUtc`), rules engines, batch jobs, and validation logic **must compare only the date portion** (`.Date` in memory, or half-open interval `< nextDay && >= targetDate` in LINQ). An entity ending today remains active throughout the entire calendar day. |
| **Operational & Forensic Timestamps** | Exact time-of-day (`HH:mm:ss`) is reserved for operational telemetry (`Created`, `Updated`, `DeletedAtUtc`, `StartedAtUtc`, `CompletedAtUtc`, `LastActivityAtUtc`). |
| **UI Localization** | The server emits UTC; UI components convert to user-local timezone using `IUserTimeZoneService` (`FormatDate` with `"yyyy-MM-dd"` for business dates, `FormatDateTime` with `"yyyy-MM-dd HH:mm:ss"` for audit timestamps). |

### 1.9 Entity Deletion Taxonomy: Primary Nodes, RAID Work Items & Universal Dependency Architecture

| Architectural Standard | Rule & Implementation |
|---|---|
| **Primary Entities are Soft-Deletable** | All core domain nodes (TaskEntity, ProjectCalendarEntity, ProgramCalendarEntity, ProgramEntity, ProjectEntity, UserEntity, TenantEntity) must implement ISoftDeletableEntity. Calling _context.Remove() marks IsDeleted = true, DeletedAtUtc, and DeletedBy via AuditableEntityInterceptor, preserving full historical traceability and query filtering via QueryFilters.SoftDelete. |
| **Universal Enterprise Dependency Intermediate Table (WorkDependencyEntity)** | All dependencies across the enterprise (intra-project, cross-project, cross-program, task-to-task, gate-to-gate, task-to-gate, and manual external vendor dependencies) are consolidated into a single unified table 	asks.WorkDependencies modeled by WorkDependencyEntity : BaseTaskEntity. Each row represents an atomic pair B -> A (B depends on A) with a 3-type endpoint discriminator (CalendarGate, TaskItem, ManualDependency), zero-trust soft-ID snapshot caching (DependsOnSnapshotTitle, DependsOnSnapshotStatus, DependsOnSnapshotDueDate), and proactive schedule conflict detection (IsScheduleConflict). |
| **Concrete RAID Log Items are Soft-Deletable** | The 4 enterprise RAID log items (R=RiskEntity, A=AssumptionEntity, I=IssueEntity, D=WorkDependencyEntity) are concrete accountable work items inheriting from BaseTaskEntity and implement ISoftDeletableEntity with full lifecycle states (Closed, InProgress), assignees, and audit tracking. |
| **Multi-Tier Audit Retention for Universal Dependencies** | Forensic traceability is preserved across two audit layers: (1) **Level 2 Entity Change Ledger**: AuditableEntityInterceptor automatically captures all property mutations into udit.AuditRecords; (2) **Level 3 User Activity Log**: Domain services (UserTaskService, ProjectMilestoneService) explicitly emit human-readable audit events via IAuditService.LogUserActivityAsync capturing project, task/milestone names, dependency types, and deadlines upon creation, promotion, or deletion. |
### 1.10 Relational Normalization & `[NotMapped]` Navigation Law

| Architectural Standard | Rule & Implementation |
|---|---|
| **Minimal Persisted Data Copying (3NF)** | Domain entities must persist **only foreign keys (`Guid`)** when referencing related entities (such as `UserEntity`, `ProjectEntity`, `ProgramEntity`, `TaskEntity`). Never persist duplicate copies of mutable attributes (`UserName`, `UserEmail`, `UserType`, `AuthorName`, `AuthorEmail`, `Title`, `Name`) across consuming tables. Updating a related entity (e.g. editing a user's `FullName` or `Email` in `UserEntity`) must be 100% contained within that entity and immediately reflected everywhere via relational joins. |
| **Unlimited `[NotMapped]` Convenience Properties** | Entities may declare any number of `[NotMapped]` read-only computed properties that forward directly to their EF Core navigation properties (e.g. `[NotMapped] public string UserName => !string.IsNullOrWhiteSpace(User?.FullName) ? User.FullName : (User?.Email ?? string.Empty);`). This preserves ergonomic UI binding and DTO projection without database column duplication. |
| **EF Core `.Include(...)` & SQL Filter Discipline** | Because `[NotMapped]` properties exist only in C# memory and cannot be translated to SQL by EF Core: (1) Queries returning entities that expose `[NotMapped]` navigation forwarders must `.Include(...)` the underlying navigation property (e.g. `.Include(r => r.User)`); (2) All LINQ `Where`, `OrderBy`, and `Select` clauses executed against `AppDbContext` must reference the persisted foreign key (`r.UserEntityId`) or the navigation property (`r.User!.Email`, `r.User!.FullName`). |
| **Tier 2 Validator Discipline** | Tier 2 Entity Validators (`IValidator<TEntity>`) execute inside `EntityValidationInterceptor` prior to database persistence, when navigation properties may not be loaded in memory. Validators must validate the persisted `Guid` foreign key (`RuleFor(x => x.UserEntityId).NotEmpty()`) and must **never** validate `[NotMapped]` navigation forwarders. |
| **Forensic Point-in-Time Exception** | Immutable point-in-time forensic ledgers (`IImmutableEntity` such as `AuditRecordEntity` and `EventPublishTrackerEntity`) and ephemeral circuit session logs (`UserSessionEntity`) are explicitly exempt and must snapshot point-in-time identity strings (`UserEmail`, `IpAddress`) so historical audit logs remain immutable even if a user changes their email years later. |

---

## 2. Multi-Tenancy Rules

| Rule | Description |
|------|-------------|
| **Zero Tenant Assumption Law** | `TenantId` is never defaulted or assumed. There is no `DefaultTenantId` on `UserEntity`. For interactive user sessions, tenant scope is resolved exclusively from the authenticated selection, verified strictly against active, cryptographically sealed `TenantUserEntity` records (`tenancy.tenant_users`). For services and background operations, `TenantId` must be explicitly supplied via `ITenantContext`. |
| **Guid TenantId Relational Integrity Law** | `TenantId` across all entities, relational foreign keys, database indexes, background jobs, and context interfaces is strictly sequential UUIDv7 `Guid`. `TenantEntity.Slug` is exclusively reserved for human-facing vanity URLs and routing; it is strictly prohibited as a relational foreign key. |
| **Fail-closed guard** | Every EF Core entity must implement either `ITenantEntity` (tenant-scoped) or `IGlobalEntity` (host-wide). Unlabeled entities cause a startup exception. |
| **Automatic query filters** | EF Core 10 named query filters (`QueryFilters.Tenant`, `QueryFilters.SoftDelete`) are applied automatically in `AppDbContext.OnModelCreating`. Never bypass filters without explicit `IgnoreQueryFilters()`. Native 16-byte UUID comparison (`e.TenantId == _tenantContext.TenantId.Value`) ensures maximum B-Tree index seek performance in PostgreSQL. |
| **TenantId immutability** | `AuditableEntityInterceptor` blocks any attempt to change `TenantId` after initial creation. |
| **Cache isolation** | All cached values use tenant-prefixed keys: `t:{tenantId}:{key}`. Eviction uses `tenant:{tenantId}` tags. |
| **Host-only queries** | Only host-level admin operations may use `IgnoreQueryFilters()`. Scope these queries clearly and audit them. |
| **Separation of Administrative Duties** | • **App Admin**: Exclusively appoints / modifies / revokes `TenantAdmin` privileges; cannot manage regular tenant users and has zero automatic access to tenant business data.<br/>• **Tenant Admin**: Sole owner of the Tenant User Roster (`TenantMember`); cannot appoint other Tenant Admins.<br/>• **Program & Project Admin**: Pick users strictly from the Tenant User Roster for program and project assignments. |
| **Zero Cross-Tenant Directory Leakage** | In multi-tenant environments with 1,000+ users, Project and Program Admins must NEVER query the global user directory. They discover and select users strictly from active members in `tenancy.tenant_users` for their tenant. |
| **App Admin Business Data Isolation** | App Admins (Root Admins) have zero blind bypass on tenant business data (Projects, Tasks, Calendars, RAID, Workflows) and cannot view or edit project resources unless explicitly granted an assigned role. |
| **Cryptographic HMAC Sealing on ANY Change** | All role and membership entities (`TenantUserEntity`, `ProgramUserRoleEntity`, `ProjectUserRoleEntity`, `UserEntity`) implement `IHmacSignableEntity`. `AuditableEntityInterceptor` recalculates `RowSignature` on every insert or update using `Security:IntegritySecret`. Direct SQL tampering results in immediate fail-closed denial and high-severity security alerts. |
| **Tenant-Scoped System Daemon Law** | Automated background processing (batch jobs, scheduled notifications, cadence monitors) executes strictly under a dedicated, tenant-scoped `UserEntity` (`daemon@{tenant.Slug}.internal`, `UserKind = UserKindEnum.SystemDaemon`). System daemons possess sequential UUIDv7 `Id`, satisfy relational foreign keys (e.g. `AuthorUserEntityId`), and enforce standard EF Core tenant query filters. |
| **Dedicated Per-Tenant Background Automation Kill Switch Law** | Each tenant's background processing can be suspended or resumed independently by updating the tenant daemon's `IsActive` flag. When inactive, background workers gracefully skip execution and log informational audit records without failing or raising false-positive error alarms. Root Tenant daemon cannot be deactivated by tenant admins. |
| **Zero Non-Interactive Login Law** | Accounts with `UserKind != UserKindEnum.Human` (such as `SystemDaemon` and `ApiIntegrationBot`) are non-interactive service identities. Interactive credential verification and session creation are strictly barred and emit critical security alerts on illicit login attempts. |

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

- `Content-Security-Policy`: Enterprise-grade strict policy with **complete eradication of `'unsafe-inline'` and `'unsafe-eval'` in scripts**. Employs per-request 128-bit cryptographic nonces (`'nonce-{random}'`) and `'strict-dynamic'`. Configurable via `Security:Csp` (`PureStrict` vs. `Level3Isolated`).
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

### 3.7 Zero-Trust Multi-Tenancy Isolation & S2S Security

| Rule | Description |
|------|-------------|
| **Zero Shared Keys in Client Nodes** | Client WebApp and Worker nodes never possess or store Azure Key Vault master keys. WebApp and Worker nodes sign S2S tokens exclusively using `Tenants:{tenantId}:LicenseKey`. |
| **Masked Opaque Claims (GDPR / ASVS)** | Ephemeral S2S tokens must **never** transmit raw user emails, display names, or database primary keys across network boundaries. Tokens use opaque masked claims: `tid` (`tnt_...`), `tenant_id`, `cid` (correlation ID), and `act` (`BatchWorker` or `SystemDaemon`). |
| **Dynamic Key Resolution** | `BlazorEnterprise.Api` resolves JWT signing keys dynamically via `IssuerSigningKeyResolver`, authenticating tenant-signed S2S tokens against `Tenants:{tenantId}:LicenseKey` while admitting the Cloud Master Key for user tokens. Missing tenant keys fail closed. |
| **Ephemeral Token Expiry** | S2S tokens minted by `ITenantTokenService` have a strict 5-minute lifespan (`DateTime.UtcNow.AddMinutes(5)`), preventing replay attacks. |
| **Compound Query Tenant Scoping** | Even with EF Core global query filters active, all batch job handlers and background listeners must enforce explicit compound LINQ query scoping: `f.TenantId == jobEvent.TenantId && ...`. |
| **Cross-Tenant Quarantine Guard** | Background handlers processing multi-file manifests must verify that every file entity's `TenantId` matches `jobEvent.TenantId`. Any mismatch immediately throws a security violation and halts execution. |

---

## 4. EF Core & Database Rules

### 4.1 Configuration Standards

| Rule | Description |
|------|-------------|
| **Fluent API only** | All entity configurations use `IEntityTypeConfiguration<T>` in `BlazorEnterprise.Persistence/Configurations/`. Never use data annotation attributes for EF mapping. |
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

Value objects use EF Core 10 `[ComplexType]` and are declared as C# `record` types in `BlazorEnterprise.Core/Domain/ValueObjects/`. They embed inline as columns in the host entity's table without shadow keys or join tables.

**Reference example**: `Address` value object on `UserEntity.PhysicalAddress`, configured via `builder.ComplexProperty(u => u.PhysicalAddress)`.

### 4.4 Transaction Management

All multi-step write operations must use `IUnitOfWork.ExecuteAsync(...)` for atomic transactions with automatic rollback and `ChangeTracker.Clear()` on failure.

---

## 5. Blazor UI Rules

### 5.1 Razor File & Component Naming Taxonomy (Mandatory)

All current and future `.razor` files must adhere strictly to this 4-tier naming taxonomy:

| Suffix | Scope & Architectural Rule | Examples | Consuming Tag |
|---|---|---|---|
| **`*Page.razor`** | **Routable Screen**: Any file declaring an `@page` directive at the top (top-level routable screen with a URL path). | `HomePage.razor`, `ProjectsDashboardPage.razor`, `LoginPage.razor`, `NotFoundPage.razor` | Loaded by Blazor Router |
| **`*Comp.razor`** | **Embedded Component**: Any reusable component embedded inside a page, layout, or another component. | `PageHeaderComp.razor`, `EmptyStateComp.razor`, `FluentSwimlaneBoardComp.razor`, `NavMenuComp.razor` | `<PageHeaderComp>`, `<EmptyStateComp>` |
| **`*Modal.razor`** | **Modal Screen / Dialog**: Any component whose whole purpose is a modal dialog / backdrop overlay screen. | `ConfirmDialogModal.razor`, `SessionRevokedModal.razor`, `ReconnectModal.razor` | `<ConfirmDialogModal>` |
| **`*Popup.razor`** | **Interactive Popup / Flyout**: Any component whose whole purpose is an interactive flyout, dropdown, or pop-up tray. | `NotificationBellPopup.razor` | `<NotificationBellPopup>` |
| **`*Layout.razor`** | **Custom Sub-Layout**: Any custom shell layout (e.g. for auth, admin, print) providing structural wrapping. | `AdminLayout.razor`, `AuthLayout.razor`, `EmptyLayout.razor` | `@layout AdminLayout` |

> **Framework Roots Exception**: Framework-mandated files retain their standard Blazor roles: `App.razor` (root HTML shell), `Routes.razor` (router shell), `MainLayout.razor` (master default layout), `_Imports.razor` (compiler directives).
>
> **Code-Behind & Scoped CSS Co-Location**:
> - Code-behind files must exactly match the full component file name: `*Page.razor.cs`, `*Comp.razor.cs`, `*Modal.razor.cs`, `*Popup.razor.cs`, `*Layout.razor.cs`.
> - Scoped CSS files must exactly match: `*Page.razor.css`, `*Comp.razor.css`, `*Modal.razor.css`, `*Popup.razor.css`, `*Layout.razor.css`.

### 5.2 Component Library

This project uses **Microsoft Fluent UI Blazor V5**. All UI components must use V5 APIs.

| Rule | Description |
|------|-------------|
| **V5 appearance enums** | Use `ButtonAppearance.Primary`, `BadgeColor.Brand`, `MessageBarIntent.Success`, etc. Never use V4 string-based parameters. |
| **Dual-generic selects** | `FluentSelect<TOption, TValue>` requires both type parameters (`TOption="T" TValue="string"`). Never omit the value type. |
| **Icons package** | Use `Microsoft.FluentUI.AspNetCore.Components.Icons` for all icon references. |
| **Native Fluent components only** | Never substitute raw HTML input elements (`<select>`, raw checkboxes, raw buttons) when native Fluent UI components exist (`<FluentSelect>`, `<FluentCheckbox>`, `<FluentButton>`). |

### 5.2.1 Fluent UI Blazor MCP Server Governance & V5 Exclusivity Standard
All developers and AI assistants working on UI components **MUST ALWAYS** consult the `fluent-ui-blazor` MCP server before implementing, refactoring, or referencing components:
1. **Tool Verification**: Use `search_components`, `get_component_details`, `get_component_enums`, `get_component_migration`, and `get_icon_usage` to verify exact parameter names, types, and supported appearance enums.
2. **Strict V5 Exclusivity**: Legacy V4 components and patterns are strictly forbidden. The build enforces `<WarningsAsErrors>CS0618</WarningsAsErrors>` to reject obsolete components at compile time.
3. **Repository Verification**: All PRs and UI changes must pass `./scripts/verify-fluent-v5.ps1`.
4. **No Assumptions**: Never guess parameter signatures or event callbacks—verify them via MCP tool calls first.

#### Mandatory V4 → V5 Component Migration Matrix

| Legacy / V4 Component | Status | Mandatory V5 Replacement |
| :--- | :--- | :--- |
| `<FluentProgressRing ... />` | **Obsolete / Renamed** | `<FluentSpinner Size="@SpinnerSize.*" ... />` |
| `<FluentSearch ... />` | **Removed** | `<FluentTextInput ...><StartTemplate><FluentIcon Value="@(new Icons.Regular.Size16.Search())" /></StartTemplate></FluentTextInput>` |
| `<FluentTextField ... />` | **Removed** | `<FluentTextInput ... />` |
| `<FluentNumberField ... />` | **Removed** | `<FluentNumberInput ... />` |
| `<FluentAnchor ... />` | **Removed** | `<FluentLink ... />` |
| `<FluentDialogProvider />` (isolated) | **Legacy** | `<FluentProviders />` (umbrella provider for dialogs, toasts, tooltips, message bars) |
| `<FluentBreadcrumb>` / Item | **Removed** | Custom hierarchy using `FluentLink` |
| `<FluentToolbar ... />` | **Removed** | `<FluentStack Orientation="Orientation.Horizontal">` |
| `<FluentSplitter ... />` | **Removed** | `<FluentMultiSplitter ... />` |
| `<FluentNavMenu>` / Group / Link | **Removed** | `<FluentNav>`, `<FluentNavItem>`, `<FluentNavCategory>` |
| `<FluentValidationMessage ... />` | **Removed** | `<FluentField ... MessageState="..." />` |
| `<FluentSelect TOption>` (single generic) | **Breaking Change** | `<FluentSelect TOption="..." TValue="...">` (dual generics strictly required) |
| `FluentInputAppearance` | **Removed** | `TextInputAppearance` or `ListAppearance` |

### 5.3 Reusable Components

| Component | Purpose | Location |
|-----------|---------|----------|
| `<PageHeaderComp>` | Consistent page title/subtitle with action slot | `Components/Common/` |
| `<ConfirmDialogModal>` | Accessible modal for destructive actions | `Components/Common/` |
| `<EmptyStateComp>` | Centered icon + message for empty views | `Components/Common/` |
| `<FormNavigationGuardComp>` | Warns on unsaved dirty form edits | `Components/Common/` |
| `<ProjectAuthorizeViewComp>` | Role-gated conditional rendering | `Components/Common/` |
| `<ImpersonationBannerComp>` | Pinned banner during impersonation | `Components/Common/` |
| `<FluentSwimlaneBoardComp<TItem>>` | Generic multi-column swimlane/kanban board | `Components/Common/` |
| `<NotificationBellPopup>` | Header notification tray popup flyout | `Components/Layout/` |
| `<NavMenuComp>` | Sidebar navigation links | `Components/Layout/` |
| `<TenantSwitcherComp>` | Header tenant switching dropdown | `Components/Layout/` |
| `<AppErrorBoundaryComp>` | User-facing correlation ID error boundary | `Components/Common/` |
| `<ObservabilityErrorBoundaryComp>` | Circuit logging telemetry error boundary | `Components/Common/` |
| `<SessionRevokedModal>` | Watchdog freezing circuit upon session revocation | `Components/Common/` |
| `<ReconnectModal>` | Custom SignalR reconnection overlay | `Components/Layout/` |
| `<ImportErrorsModal>` | Line-by-line schema validation diagnostics modal | `Modules/Imports/Components/` |

### 5.4 State Management

| Rule | Description |
|------|-------------|
| **Form drafts** | Use `ILocalStorageFormService` for encrypted auto-save of uncommitted form data. |
| **Circuit persistence** | Use `PersistentStateComponentBase` for state survival across circuit pause/resume. |
| **Session revocation** | `<SessionRevokedModal>` monitors session validity and freezes the UI on revocation. |

### 5.5 Error Handling

| Rule | Description |
|------|-------------|
| **Root error boundary** | `<ObservabilityErrorBoundaryComp>` wraps the router, logging unhandled exceptions with structured tenant/user context. |
| **Page error boundary** | `<AppErrorBoundaryComp>` generates user-facing `ERR-XXXXXXXX` correlation IDs with a recovery button. |
| **Reconnection** | `<ReconnectModal>` provides custom SignalR reconnection UI. Never use the default Blazor reconnection overlay. |

### 5.6 Responsive Design

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
BlazorEnterprise/
├── BlazorEnterprise.slnx                           # .NET SLNX solution
├── Directory.Build.props                        # NuGet CVE audit enforcement
├── .github/
│   ├── dependabot.yml                           # Weekly NuGet dependency updates
│   └── workflows/owasp-zap-scan.yml             # Weekly OWASP ZAP DAST scan
│
├── BlazorEnterprise.Core/                           # Domain layer (zero dependencies)
│   ├── Common/                                  # Result monad, pagination, query filters
│   ├── Constants/                               # Role constants
│   ├── Contracts/                               # All service interfaces
│   ├── DataListTypes/                            # All shared enums
│   ├── Domain/                                  # Entities, Base abstractions, value objects
│   ├── Events/                                  # Job event contracts
│   └── Validation/                              # FluentValidation validators
│
├── BlazorEnterprise.Persistence/                    # Data access layer
│   ├── Configurations/                          # EF Core Fluent API configurations
│   ├── Context/                                 # AppDbContext
│   ├── Extensions/                              # LINQ query extensions (LeftJoin)
│   ├── Interceptors/                            # Audit + Security SaveChanges interceptors
│   ├── Services/                                # Data services (UnitOfWork, TenantService, etc.)
│   └── PersistenceExtensions.cs                 # DI registration
│
├── BlazorEnterprise.Infrastructure/                 # Integration & middleware layer
│   ├── Notifications/                           # Email/Teams senders, Channel queue, worker
│   ├── Observability/                           # Circuit lifecycle handler
│   ├── Security/                                # CSP headers, AuthZ handlers, HMAC validator, XSS sanitizer
│   ├── Storage/                                 # ProtectedLocalStorage form service
│   ├── UI/                                      # Responsive breakpoint service
│   └── InfrastructureExtensions.cs              # DI registration
│
├── BlazorEnterprise.Jobs/                           # Background processing layer
│   ├── Abstractions/                            # IBatchJobHandler, IJobEventQueue
│   ├── Jobs/                                    # Job handlers (Audit/, Catalog/)
│   ├── Listeners/                               # Channel consumer with retry policy
│   ├── Queue/                                   # Channel-based event queue
│   ├── Schedulers/                              # Cronos periodic scheduler
│   ├── Services/                                # JobManagerService
│   └── JobsExtensions.cs                        # DI registration
│
└── BlazorEnterprise (Web)/                          # Presentation & composition root
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

## 13. Git Workflow & Staging Governance

1. **Never Auto-Stage Changed Files**: AI coding assistants and automated tools MUST NEVER automatically stage files using `git add` or `git add -A`.
2. **Keep All Modifications Unstaged**: All modified, newly created, or refactored files must remain unstaged in the working directory (`??` and ` M`) so the developer can review diffs, test locally, and selectively stage files for commits.
3. **Commit Integrity**: The developer retains sole discretion over staging, committing, and pushing changes.

---

## 14. Enterprise UI Wizards & Sequential Workflows Governance

### 14.1 Enterprise Wizards Framework (`BlazorEnterprise.Shared.UI`)

| Architectural Requirement | Implementation Rule |
|---------------------------|---------------------|
| **Decoupled RCL Package** | Reusable wizard templates reside in `BlazorEnterprise.Shared.UI` (NuGet-ready Razor Class Library), keeping presentation patterns decoupled from specific business domain logic. |
| **Declarative Composition** | Developers compose multi-step wizards declaratively using `<FluentWizardModal TState="...">` and child `<WizardStep TState="...">` tags. |
| **In-Memory State Preservation** | State is held in-memory via reference in generic `WizardContext<TState>` across all step transitions. Form inputs bind directly to state properties without intermediate database round-trips. |
| **Accidental Dismissal Lock** | Backdrop clicks are locked from dismissing the modal. The cancel/close action evaluates `Context.IsDirty`; if dirty, a confirmation modal prompts before discarding user input. |
| **Per-Step Async Validation** | Step advancement triggers `OnValidateAsync(Context)`. Advancing is blocked and errors are mapped into `Context.StepErrors` if validation fails. |

### 14.2 ServiceNow-Style Hierarchical Sequential Workflows (`IWorkflowService`)

| Architectural Requirement | Implementation Rule |
|---------------------------|---------------------|
| **Constant Workflow Owner** | The workflow container (`WorkflowTaskEntity`) is assigned to a single, constant `WorkflowOwnerEmail` who retains overarching accountability from kickoff to sign-off, monitoring progress and preventing pipeline stalls. |
| **Steps as Genuine `TaskEntity`** | Each workflow step is a genuine `TaskEntity` record (`ICollection<TaskEntity> Steps`), inheriting formal task tracking, task numbers, descriptions, deadlines, comments, and status. |
| **1-to-Many Delegated Subtasks** | Step owners can decompose any workflow step into discrete child `SubTaskEntity` items, assigning individual team members (`AssigneeEmails`) to perform specific work. |
| **Multi-Level Tenancy Scoping** | Workflows can be declared at `Tenant` (cross-cutting governance), `Program` (strategic portfolio), or `Project` (delivery track) level with strict tenant isolation and compound indexes (`IX_WorkflowTasks_TenantId_Level_ProgramId_ProjectId`). |
| **Zero Trust Server-Side Authorization** | Step completion (`CompleteStepAsync`), step reassignment (`ReassignStepAsync`), and subtask addition (`AddSubTaskToStepAsync`) strictly verify that the caller is the assigned Step Owner, the constant Workflow Owner, or a Tenant/Root Admin. |
| **Forward-Only Sequential Gates** | Steps transition sequentially (Step 1 -> Step 2 -> ... -> Completed). Completing Step K activates Step K+1 while preserving the constant `WorkflowOwnerEmail`. |
| **Multi-Party Notification Broadcast** | On step transitions, the next step owner receives an urgent "Action Required" notification, while the constant workflow owner and watchers receive progress milestone alerts via `INotifyService`. |
| **Dedicated Workflows Hub** | Centralized dashboard at `/workflows` (`WorkflowsDashboardPage`) allowing cross-scope filtering (Tenant / Program / Project), KPI metrics, launch wizards, and step management. |
| **100% Fluent UI Blazor V5 Compliance** | All workflow UI components (`WorkflowStepperComp`, `CreateWorkflowWizardModal`, `WorkflowsDashboardPage`) strictly utilize V5 components (`FluentTextInput`, `FluentTextArea`, `FluentDatePicker`, `FluentBadge`, `FluentIcon`, `FluentStack`, `FluentNav`). |

---

## 15. Complete Documentation Suite Index

For deep-dive architectural specifications, security walkthroughs, and code recipes, refer to the following guides:

- [**`docs/00-Overview-and-Glossary.md`**](file:///c:/Users/cheri/source/repos/BlazorEnterprise/docs/00-Overview-and-Glossary.md) — Domain mental model, personas, feature map, and glossary.
- [**`docs/01-Getting-Started.md`**](file:///c:/Users/cheri/source/repos/BlazorEnterprise/docs/01-Getting-Started.md) — Setup, `Rename-Project.ps1`, configuration dictionary, and migrations.
- [**`docs/02-Architecture.md`**](file:///c:/Users/cheri/source/repos/BlazorEnterprise/docs/02-Architecture.md) — Clean Architecture 6-project topology, DI rules, interceptors, and background channels.
- [**`docs/03-Security-Model.md`**](file:///c:/Users/cheri/source/repos/BlazorEnterprise/docs/03-Security-Model.md) — Defense-in-depth, HMAC seals, tenant isolation, and session management.
- [**`docs/04-Task-Hierarchy-and-Workflows.md`**](file:///c:/Users/cheri/source/repos/BlazorEnterprise/docs/04-Task-Hierarchy-and-Workflows.md) — Task scoping, sequential workflows, step owners, and `<FluentWizardModal>`.
- [**`docs/05-UI-Conventions-Fluent-V5.md`**](file:///c:/Users/cheri/source/repos/BlazorEnterprise/docs/05-UI-Conventions-Fluent-V5.md) — Component taxonomy (`*Page/*Comp/*Modal`), catalog, and V5 pitfall patterns.
- [**`docs/06-How-To-Extend.md`**](file:///c:/Users/cheri/source/repos/BlazorEnterprise/docs/06-How-To-Extend.md) — Step-by-step copy-paste recipes for extending features.
- [**`docs/07-AI-Agent-Rules.md`**](file:///c:/Users/cheri/source/repos/BlazorEnterprise/docs/07-AI-Agent-Rules.md) — Operational guardrails and instructions for AI coding assistants.

---

> **Last Updated**: October 2026 | **Target Framework**: .NET 10 | **UI**: Microsoft Fluent UI Blazor V5

