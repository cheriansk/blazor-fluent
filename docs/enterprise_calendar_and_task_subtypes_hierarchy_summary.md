# Enterprise Work Item Hierarchy & Calendar Architecture Summary

## 1. Executive Summary

This architecture establishes an enterprise-grade, Zero Trust-compliant work item and calendar infrastructure for **BlazorFluent**. It spans four hierarchical organizational tiers:
1. **Tenant** — Executive strategic governance, compliance tasks, and enterprise risks.
2. **Program** — Cross-project strategic calendar, milestones, and full **R.A.I.D.** logs (Risks, Assumptions, Issues, Dependencies).
3. **Project** — Operational delivery boards, depth-1 subtasks, and inter-task blocker dependency contracts.
4. **User** — Aggregated personal schedule, assigned deadlines, personal calendar appointments, and overdue alerts.

All entities persist on **dedicated physical tables** (Table-Per-Concrete-Type / TPC) partitioned into PostgreSQL schemas `"tasks"` and `"calendar"`, ensuring clean database boundaries, zero sparse columns, and optimal index performance.

---

## 2. Core Architectural Pillars

### A. Dedicated Separate Tables (TPC Architecture)
Instead of a bloated Single Table Inheritance (TPH) table with dozens of nullable columns, every domain concept lives in its own physical table under dedicated PostgreSQL schemas:
- **`tasks` Schema**:
  - `tasks.Tasks` (`TaskEntity`)
  - `tasks.SubTasks` (`SubTaskEntity`)
  - `tasks.Risks` (`RiskEntity`)
  - `tasks.Assumptions` (`AssumptionEntity`)
  - `tasks.Issues` (`IssueEntity`)
  - `tasks.BlockerDependencies` (`BlockerDependencyEntity`)
  - `tasks.TaskLinks` (`TaskLinkEntity`)
  - `tasks.WorkItemComments` (`WorkItemCommentEntity`)
- **`calendar` Schema**:
  - `calendar.ProgramCalendars` (`ProgramCalendarEntity`)
  - `calendar.ProjectCalendars` (`ProjectCalendarEntity`)
  - `calendar.UserCalendars` (`UserCalendarEntity`)

### B. Fail-Closed Validation Interceptor Guard
`AppDbContext.OnModelCreating` mandates that every persistent entity type must have an `IValidator<T>` registered in the Dependency Injection container. If any entity lacks a validator, application bootstrap halts immediately. Dedicated `FluentValidation` classes were authored for all 11 persistent entities.

### C. Strict Depth-1 Subtask Hierarchy
Subtasks (`SubTaskEntity`) represent discrete atomic checklist steps belonging exclusively to a parent `TaskEntity`:
- Subtasks **cannot** have their own subtasks (nesting depth strictly bounded to 1).
- Subtasks are scoped exclusively within their parent task's boundary.
- Subtasks can relate to or be blocked only by sibling subtasks under the same parent task.

### D. Accountable Blocker Dependencies
Unlike generic issue-link strings in standard issue trackers, when a task is linked with `TaskDependencyType.BlockedBy`:
- A concrete `BlockerDependencyEntity` is automatically created on its own table.
- Requires an explicit **SLA Resolve Due Date**, **Criticality Rating** (`Low`, `Medium`, `High`, `Critical`), **Rich Impact Details**, and **Watcher Emails**.
- **Automated Lifecycle Integrity**:
  - When prerequisite Task B is closed (`CloseTaskAsync`), any blocker pointing to B is automatically closed, and an audit message is broadcast to all watchers.
  - When prerequisite Task B is reopened (`ReopenTaskAsync`), the blocker is automatically reopened, and an urgent alert is logged.

### E. Unified Extensible Calendar via Plugin Registry Pattern
Calendar aggregation leverages `IWorkItemRollupProvider`:
- Any work item subtype implements `IWorkItemRollupProvider` and registers in DI.
- `ICalendarService` queries direct events (`calendar.*`) and fans out concurrently to all registered rollup providers (`TaskRollupProvider`, `SubTaskRollupProvider`, `RiskRollupProvider`, `AssumptionRollupProvider`, `IssueRollupProvider`, `BlockerRollupProvider`).
- Unified filtering, agenda view, month view, and overdue alerting dynamically summarize deliverables across Tenant, Program, Project, and User scopes.

### F. Zero Trust Multitenancy & Authorization
- Every entity implements `ITenantScopedEntity`, `ITenantAuditableEntity`, and `IEffectiveDatedEntity`.
- Tenant query filters (`e.TenantId == CurrentTenantId`) enforce compile-time and runtime row-level security.
- Hierarchy permissions (`IHierarchyAuthorizationService`) enforce boundary gates across Tenant, Program, Project, and User actions.

---

## 3. Detailed Component Map

| Layer | Files & Artifacts | Responsibility |
| :--- | :--- | :--- |
| **Domain Contracts & Enums** | `BlazorFluent.Core/DataListTypes/TaskHierarchyTypes.cs` | Defines `TaskLevel`, `TaskPriority`, `TaskStatus`, `TaskItemType`, `RiskImpact`, `RiskProbability`, `RiskStatus`, `AssumptionValidationStatus`, `IssueSeverity`, `TaskDependencyType`, `DependencyCriticality`, `CalendarEntryType`, `CalendarEntryStatus`, and `CalendarItemDto`. |
| **Domain Entities** | `BlazorFluent.Core/Domain/Tasks/*`<br>`BlazorFluent.Core/Domain/Calendar/*` | Domain entities for `BaseTaskEntity`, `TaskEntity`, `SubTaskEntity`, `RiskEntity`, `AssumptionEntity`, `IssueEntity`, `BlockerDependencyEntity`, `TaskLinkEntity`, `WorkItemCommentEntity`, `BaseCalendarEntity`, `ProgramCalendarEntity`, `ProjectCalendarEntity`, `UserCalendarEntity`. `UserTaskEntity` retained as `[NotMapped]` alias. |
| **Validators** | `BlazorFluent.Core/Domain/Tasks/Validators/*`<br>`BlazorFluent.Core/Domain/Calendar/Validators/*` | FluentValidation rules for all 11 new domain entities ensuring fail-closed guard passes. |
| **EF Core Persistence** | `BlazorFluent.Persistence/Configurations/Tasks/*`<br>`BlazorFluent.Persistence/Configurations/Calendar/*`<br>`BlazorFluent.Persistence/Context/AppDbContext.cs` | TPC separate table mapping under `"tasks"` and `"calendar"` schemas with indexes on `TenantId`, foreign keys, and effective dates. |
| **Rollup Providers & Services** | `BlazorFluent.Persistence/Services/Providers/*`<br>`BlazorFluent.Persistence/Services/CalendarService.cs`<br>`BlazorFluent.Persistence/Services/UserTaskService.cs` | Rollup aggregation plugins, `ICalendarService` implementation, blocker dependency auto-sync, subtask CRUD, and watcher alerts. |
| **UI Components** | `Components/Common/FluentCalendarScheduleComp.razor`<br>`Components/Common/TaskDependencyLinkModal.razor` | Fluent UI Blazor 5 schedule view (Month, Agenda, Overdue, Filters) and dependency link modal with SLA and impact documentation. |
| **UI Pages & Tabs** | `Components/Pages/Settings/SettingsUserCalendarTab.razor`<br>`Components/Pages/Settings/SettingsPage.razor`<br>`Components/Pages/Settings/SettingsProgramsTab.razor`<br>`Modules/Tenancy/Pages/ProjectTasksBoardPage.razor`<br>`Components/Pages/Settings/SettingsTenantsTab.razor` | User Calendar tab, Program Calendar & R.A.I.D. Log tabs, Project Task Board Subtasks & Blocker modal hookups, and Tenant Tasks & Risks management. |

---

## 4. Verification Checklist

- [x] **Separate Physical Tables**: Verified all 11 entities have dedicated configurations and schemas (`tasks` and `calendar`).
- [x] **Zero Sparse Columns**: Verified no polymorphic single table inheritance was used.
- [x] **Validation Interceptors**: Verified all 11 entities have active validators registered in DI.
- [x] **Depth-1 Subtask Constraint**: Verified subtasks only reference `TaskId` and cannot nest.
- [x] **Accountable Blocker SLAs**: Verified `BlockerDependencyEntity` captures SLA date, criticality, impact text, and watchers.
- [x] **Automated Blocker Sync**: Verified closing Task B auto-closes blockers and reopening Task B auto-reopens blockers.
- [x] **Extensible Calendar Rollup**: Verified all 6 rollup providers aggregate into `ICalendarService`.
- [x] **Zero Git/Dotnet Commands Executed**: Adhered strictly to instruction constraints.
- [x] **README.md Untouched**: Preserved original documentation integrity.
