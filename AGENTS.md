# AGENTS.md — Universal AI Agent Single Source of Truth

> **CRITICAL RULE FOR ALL AI ASSISTANTS (Gemini, Claude, Cursor, GitHub Copilot)**:
> This document (`AGENTS.md`) is the **authoritative Single Source of Truth** for the entire solution.
> If the user asks to add, update, refine, or remove coding rules, operational constraints, or architectural conventions, **YOU MUST APPLY THE CHANGES DIRECTLY TO THIS FILE (`AGENTS.md`)**.
> Never duplicate or fork rules into assistant-specific config files (`GEMINI.md`, `.cursorrules`, `.github/copilot-instructions.md`). Those files are lightweight pointers to this document.
> For exhaustive deep-dive specifications and code skeletons, consult [`PROJECT_RULES.md`](PROJECT_RULES.md) and [`docs/07-How-To-Extend-and-Developer-Recipes.md`](docs/07-How-To-Extend-and-Developer-Recipes.md).

---

## 🛡️ 1. Operational Guardrails & Command Execution (Cardinal Rules)
- **Direct Implementation on Spec Requests**: Whenever the user requests "Implement `<spec-file>`" (e.g., `Implement @[docs/specs/...requirements.md]`), proceed IMMEDIATELY to applying the code changes directly. Do NOT pause to generate a multi-step execution plan or ask for confirmation, regardless of file count or complexity, unless the user explicitly requested `/plan`. The spec itself is the approved plan.
- **Dotnet Build & Test Restriction**: NEVER run `dotnet build`, `dotnet test`, `dotnet run`, `dotnet ef`, or `msbuild` without explicit confirmation or direct request from the user. When code verification is ready, present the changes and ask the user for permission before running any build or test command.
- **Git Restrictions**: NEVER execute any git commands (`git status`, `git add`, `git commit`, `git checkout`, `git reset`, `git restore`, `git push`, etc.). Leave all changes unstaged in the working tree for the developer.
- **Documentation & Comment Integrity**: Preserve all existing comments, XML docstrings, and architectural notes unrelated to your immediate edit.

---

## 📝 2. Mandatory Observability: Correlation IDs, Structured Logging & Zero Error Leakage

### A. Unified Correlation ID as the Single Incident Reference
- **Single Source of Truth for Tracing**: The active `ICorrelationContext.CorrelationId` is the universal trace identifier across the entire solution (Web, API, Jobs, Database Audit).
- **On-Screen Reference ID**: Whenever a user action fails (saving, retrieving, submitting, or navigating), the UI MUST display only safe, high-level guidance paired with the active **Correlation ID as the Reference ID**:
  > *"Unable to save changes. Please try again or share Reference ID: `{CorrelationId}` with your administrator."*
- **Local Dev & Production Traceability**: Because the on-screen reference ID matches the ambient `CorrelationId`, developers and support can immediately grep local console logs, Serilog files, or Seq/Application Insights for that exact token to see the full causal chain and callstack.
- **Outbound HTTP Calls**: Always attach `X-Correlation-Id` using `CorrelationIdDelegatingHandler` on all typed HTTP clients.

### B. Strict Zero Sensitive Error Leakage Law
- **Zero Raw Exceptions on Screen**: NEVER display raw exception messages (`ex.Message`), stack traces, SQL syntax/foreign key constraints, or internal entity schemas to the user in HTML, toasts, or error cards.
- **Error Boundaries & Feedback**: All error boundary cards (`AppErrorBoundaryComp`), dialogs, and validation bars must render user-friendly text + the active `CorrelationId`.
- **Server-Side Full Fidelity**: The complete exception object (`ex`), callstack, tenant ID, and user ID must be recorded on the server via `Logger.LogError(ex, "Failed to {Operation}. CorrelationId: {CorrelationId}", operation, correlationId)`.
- **REST API Responses**: API error payloads must return standardized RFC 7807 `ProblemDetails` or `Result.Failure(errorMessage)` with sanitized messages and the `TraceId` / `CorrelationId`, never unhandled exception stack traces.

### C. Zero Swallowed Exceptions Law
- **Zero Swallowed Exceptions**: Never write empty `catch { }` or `catch (Exception) { }` blocks. Every catch block MUST either:
  1. Log the exception with appropriate severity via `ILogger<T>` (`Logger.LogError(ex, "...")` or `Logger.LogWarning(ex, "...")`).
  2. Handle intentional cancellation (`catch (OperationCanceledException)`) when unmounting or cancelling async operations.
  3. Re-throw if the operation cannot recover (`throw;`).
- **Structured Error Logging**: Always pass the `Exception` object as the first parameter to logger methods (`Logger.LogError(ex, "Context message with {TemplateProperty}", value)`), preserving the callstack. Never format message strings using string interpolation (`$""`) in logger calls.
- **Mandatory Logger Injection**: Every Blazor code-behind (`.razor.cs`), Razor component (`.razor`), and backend service handling async operations, event subscriptions, or lifecycle tasks MUST inject `ILogger<TComponent>` or `[Inject] private ILogger<TComponent> Logger { get; set; } = default!;`.

---

## 🏛️ 3. Base Template Preservation & Downstream Extension Law
- **Protected Base Files**: Core entity base classes (`BaseEntity`, `AuditableEntity`, `TenantAuditableEntity`), security pipelines, global interceptors, and core UI wrappers (`Csk*`) represent the architectural foundation.
- **No Direct Modification of Base Files**: NEVER delete, rename, or strip architectural invariants from these protected starter files.
- **The `.Custom.cs` Extension Pattern**: When extending existing domain entities or core services with project-specific logic, ALWAYS author a separate partial class with the `.Custom.cs` naming convention (e.g., `TaskEntity.Custom.cs`) or inherit base types rather than modifying base definitions.

---

## 🏛️ 4. Clean Architecture & Solution Topology (.NET 10, C# 13)
- **Strict Inward Dependency Flow**:
  - `BlazorEnterprise.Shared`: Shared DTOs, contracts, attributes, and `DataListTypes` enums. Zero internal dependencies.
  - `BlazorEnterprise.Core`: Pure domain entities, value objects, FluentValidation validators, and service contracts (`I*Service` in `Core/Contracts/`). Zero persistence or UI dependencies.
  - `BlazorEnterprise.Persistence`: 100% of EF Core `AppDbContext`, `IEntityTypeConfiguration<T>`, interceptors, migrations, and data service implementations. Never reference `Infrastructure` or UI projects.
  - `BlazorEnterprise.Infrastructure`: External integrations (SMTP, Teams, S2S tokens, HTTP Engine client, CSP/security middleware, observability). Never reference `Persistence`.
  - `BlazorEnterprise.RulesEngine`: Pure calculation and business rules engine.
  - `BlazorEnterprise.Jobs`: All background queue listeners, periodic schedulers, batch job handlers, and file import pipelines.
  - `BlazorEnterprise.Worker`: Thin headless console host shell (~60 lines). **Zero batch job implementations belong in `Worker`**; all jobs reside in `BlazorEnterprise.Jobs`.
  - `BlazorEnterprise.Shared.UI`: Reusable Native Material Design 3 (`Csk*`) UI components, SVG icons (`CskIcons`), theme tokens, and `<CskWizardModal>` framework RCL.
  - `BlazorEnterprise.Web` & `BlazorEnterprise.Api`: Interactive Blazor Server UI composition root and REST/Minimal API processing engine.
  - `BlazorEnterprise.AppHost`: .NET Aspire local startup orchestration only. Do not refactor `AppHost` unless explicitly asked.

---

## ⚖️ 5. Domain Entities, Enums, DTOs & Roslyn Analyzer Laws (`TG006` – `TG027`)
All code is governed at compile time by `TemplateGuard.Analyzers`:
- **`TG006` (`*Enum` Suffix Law)**: Every enum in `BlazorEnterprise.Shared.DataListTypes` MUST end with `Enum` (e.g., `TaskLevelEnum`, `ActivityTaskStatusEnum`). Suffixes `Lst`/`List` and `Dtl`/`Detail` are strictly forbidden.
- **`TG007` (`*Entity` Suffix Law)**: Every domain entity and abstract entity base class in `BlazorEnterprise.Core` MUST end with `Entity` (e.g., `TaskEntity`, `ActionItemEntity`).
- **`TG008` (Inheritance & Lifecycle Markers)**:
  1. Inherit `TenantAuditableEntity` (tenant-scoped), `AuditableEntity` (host-wide), or `BaseEntity`.
  2. Implement tenancy boundary: `ITenantEntity` or `IGlobalEntity`.
  3. Implement **exactly one** deletion lifecycle marker: `ISoftDeletableEntity`, `IHardDeletableEntity`, or `IImmutableEntity`.
  4. If implementing `IProjectScopedEntity`, MUST also implement `ITenantEntity`.
- **`TG009` (Companion Validator Law)**: Every concrete entity must have a companion `AbstractValidator<TEntity>` in `BlazorEnterprise.Core` enforcing single-entity invariants (never inject `DbContext` into validators).
- **`TG010` (Companion EF Configuration Law)**: Every mapped concrete entity must have a companion `IEntityTypeConfiguration<TEntity>` in `BlazorEnterprise.Persistence/Configurations/` using EF Core Fluent API only (no EF mapping attributes on entities).
- **`TG011` (`DateOnly` Strictly Prohibited)**: Never use `DateOnly` or `DateOnly?` in entities, DTOs, services, or UI. Always use UTC `DateTime` (`timestamptz`). Compare business dates using `.Date` or half-open intervals (`< nextDay && >= targetDate`).
- **`TG012` (3NF Normalization & `[NotMapped]` Law)**: Persist only foreign key `Guid` properties (`UserEntityId`, `ProjectId`). Never persist duplicate mutable strings (`UserName`, `UserEmail`, `AuthorName`). Expose convenience forwarders with `[NotMapped]` and `.Include(...)` the navigation property in queries.
- **`TG013` – `TG015` (DataList Enum Taxonomy & Extensions)**:
  - Decorate every enum in `DataListTypes` with `[LifecycleEnum]` (requiring `AllowedTransitions` in a companion `*Definitions` class) or `[ClassificationEnum]`, and decorate every member with `[Display(Name = "...")]`.
  - Direct calls to `Enum.Parse`, `Enum.TryParse`, `Enum.GetValues`, or `Enum.GetNames` are **forbidden (`TG013`)**. Always use `DataListExtensions` (`.ToEnum<T>()`, `.TryToEnum<T>()`, `.GetDisplayName()`, `.GetCode()`, `DataListExtensions.GetDataListItems<TEnum>()`).
- **`TG027` (Zero Hardcoding Law Across Entities, DTOs, Models & Records)**:
  - Governs all classes inheriting `BaseEntity`, all types ending in `Dto`, `Request`, `Response`, `Model`, `ViewModel`, `Record`, and all C# `record` types.
  - **Permitted Initializers (Structural & Null-Safety only)**:
    - Empty strings: `""` or `string.Empty`
    - Empty collections: `[]`, `new()`, `new List<...>()`, `new HashSet<...>()`, `new Dictionary<...>()`
    - Sequential UUIDv7 keys: `Guid.CreateVersion7()`
    - Null suppression: `null!`
  - **Strictly Prohibited Initializers (Compile Error)**:
    - Enum defaults (e.g., `= TaskStatusEnum.Open`, `= TaskPriorityEnum.Medium`)
    - Magic numbers (e.g., `= 0`, `= 42`, `= 100`)
    - Magic strings (e.g., `= "USD"`, `= "Active"`, `= "Default"`)
    - Booleans (e.g., `= true`, `= false`)
    - Dates (e.g., `= DateTime.UtcNow`)
  - **UI Form Defaulting**: Default values belong in component lifecycle initialization (e.g., `OnInitialized()`), never baked into contracts or entity definitions.

---

## ⚡ 6. Blazor Server, EF Core & Concurrency Rules
- **Zero Direct `AppDbContext` Injection**: NEVER inject `AppDbContext` directly into Blazor components, pages, or domain services. Always inject `IDbContextFactory<AppDbContext>` and create a per-operation context: `await using var db = await _dbFactory.CreateDbContextAsync(ct);`.
- **No `Task.WhenAll` or `Parallel.*` (`TG026`)**: Always `await` async database/service calls sequentially in Blazor lifecycle methods (`OnInitializedAsync`, `OnParametersSetAsync`) and persistence services.
- **Circuit Boundary Protection**: All async event handlers (such as `HandleProjectChanged` or event subscriptions) MUST wrap operations in a structured `try / catch` block with `Logger.LogError(ex, ...)`. Fire-and-forget `_ = InvokeAsync(...)` without error handling is strictly forbidden.
- **Timer Lifecycle**: Periodic timers (`PeriodicTimer`) must pass a `CancellationToken` linked to component disposal so disconnected circuits terminate background polling cleanly.
- **Constant-Time $O(1)$ Batch Querying**: Never execute database queries inside iterative loops (`foreach`). Pre-aggregate child records using batch `GroupBy` projections.
- **Split Queries on Sibling Collections**: Append `.AsSplitQuery()` when eagerly loading multiple 1:N collections to eliminate SQL Cartesian cross-join multiplication.
- **Global `NoTracking` Default**: `AppDbContext` defaults to `QueryTrackingBehavior.NoTracking`. Chain `.AsTracking()` explicitly whenever loading an entity to mutate and call `SaveChangesAsync()`.
- **Captive Dependency Prevention**: Never inject `Scoped` services into `Singleton` services; use `IServiceScopeFactory` inside background workers/singletons.
- **Multi-Tenancy & Dual-Gate AuthZ**: Never default or assume `TenantId` (resolve via `ITenantContext`). Enforce authorization at both the page level (`AuthorizedPageComponentBase`) and service layer (`IProjectAuthorizationService.EnsureCanVisitAsync` / `EnsureCanEditAsync`). Sanitize user HTML/text via `IInputSanitizer.SanitizeHtml()`.

---

## 🎨 7. UI, Native Material Design 3 (`Csk*`) & Strict CSP Rules
- **RCL Component Boundary**: Reusable domain-agnostic UI controls (`Csk*`), inputs, editors, and theme tokens live strictly in `BlazorEnterprise.Shared.UI` with collocated static assets (`_content/BlazorEnterprise.Shared.UI/...`).
- **Domain UI Locality**: Business-specific pages, workflows, and modals bound to application services reside in `BlazorEnterprise.Web`.
- **Razor File Naming Taxonomy**: Every `.razor` file (except framework roots) must end with `*Page.razor` (routable `@page`), `*Comp.razor` (embedded component), `*Modal.razor` (dialog), `*Popup.razor` (flyout), or `*Layout.razor`.
- **Code-Behind Threshold (30 Lines)**: If `@code` exceeds 30 lines or injects services, split it into a companion `.razor.cs` partial class using `[Inject]` properties.
- **Native Material Design 3 (`Csk*`) Exclusivity**:
  - Strictly use the native **`Csk*`** components from `BlazorEnterprise.Shared.UI` (`<CskButton>`, `<CskTextInput>`, `<CskTextArea>`, `<CskSelect TOption="..." TValue="...">`, `<CskOption>`, `<CskDatePicker>`, `<CskCheckbox>`, `<CskSwitch>`, `<CskRadioGroup>`, `<CskDataGrid>`, `<CskDialog>`, `<CskCard>`, `<CskBadge>`, `<CskMessageBar>`, `<CskSpinner>`, `<CskTabs>`, `<CskTooltip>`, `<CskWizardModal TState="...">`, and `<CskIcon Icon="CskIcons.*" />`).
  - Use `<DataListSelect TEnum="..." TValue="..." />` (which wraps `<CskSelect>`) for domain `DataListTypes` enums.
  - Never introduce third-party UI libraries or external CSS frameworks (Fluent UI V5, MudBlazor, Bootstrap, Tailwind).
- **Strict Content Security Policy (CSP)**:
  - Never generate inline `<script>` tags, inline event attributes (`onclick`, `onload`), or `eval()`.
  - Zero inline `style="..."` attributes; use scoped CSS (`.razor.css`), Material 3 design tokens, or stylesheet classes.
  - Isolate JS interop in dedicated `.razor.js` ES modules loaded via `IJSRuntime`.

---

## 🧠 8. Modular Skills Library Reference
Always consult the 11 modular engineering skills before designing, writing, or refactoring code:
- Directory locations: `.gemini/skills/`, `.cursor/skills/`, `.claude/skills/`, and `.github/skills/`.
- To sync skills across tools after editing, run: `pwsh scripts/sync-ai-skills.ps1`.
- Available skills:
  1. `aspnet-core`: ASP.NET Core idioms, middleware, dependency injection.
  2. `author-component`: Native Material 3 Razor component authoring conventions.
  3. `blazor`: Blazor Server state management, rendering lifecycle, and event handling.
  4. `collect-user-input`: Native Material 3 form design, data binding, and FluentValidation integration.
  5. `coordinate-components`: EventCallbacks, cascaded parameters, and cross-component messaging.
  6. `entity-framework-core`: Entity configuration, migrations, navigations, and auditable tenancy.
  7. `fetch-and-send-data`: Delegating handlers, `X-Correlation-Id`, HTTP clients.
  8. `optimizing-ef-core-queries`: $O(1)$ batch queries, `.AsSplitQuery()`, projection optimization.
  9. `plan-ui-change`: Native Material 3 layout planning and UX hierarchy.
  10. `support-prerendering`: Circuit restoration and prerender-safe initialization.
  11. `use-js-interop`: CSP-compliant isolated ES modules via `IJSRuntime`.
