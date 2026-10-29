# Antigravity Ultra-Lean Specifications Catalog

> **Last Generated**: `2026-10-29 12:57:08 -04:00`  
> **Manifest Source**: [specs-manifest.json](./specs-manifest.json)  
> **Regenerate Command**: `pwsh scripts/Generate-Specs.ps1` (or `pwsh scripts/Generate-Specs.ps1 -GenerateHtml` for visual Mermaid HTML explorer)

## Generated Target Specifications

| Spec File (Human) | AI Navigation Index | History by Date | Originating Source | Parent Spec | Category | Unregistered Children | Summary |
| :--- | :--- | :--- | :--- | :--- | :--- | :---: | :--- |
| [**SettingsPage.requirements.md**](./SettingsPage.requirements.md) | [`SettingsPage.index.md`](./index/SettingsPage.index.md) | [`SettingsPage.history.md`](./history/SettingsPage.history.md) | [`BlazorEnterprise.Web/Components/Pages/Settings/SettingsPage.razor`](../../src/BlazorEnterprise.Web/Components/Pages/Settings/SettingsPage.razor) | *(Root)* | Settings UI | [**[ALERT: 8 Review]**](./validation/ComponentRegistrationWarnings.md) | Root Settings shell managing navigation tabs and lazy-loaded administration panels. |
| [**SettingsTenantsTab.requirements.md**](./SettingsTenantsTab.requirements.md) | [`SettingsTenantsTab.index.md`](./index/SettingsTenantsTab.index.md) | [`SettingsTenantsTab.history.md`](./history/SettingsTenantsTab.history.md) | [`BlazorEnterprise.Web/Components/Pages/Settings/SettingsTenantsTab.razor`](../../src/BlazorEnterprise.Web/Components/Pages/Settings/SettingsTenantsTab.razor) | [SettingsPage.requirements.md](./SettingsPage.requirements.md) | Settings UI | [**[ALERT: 2 Review]**](./validation/ComponentRegistrationWarnings.md) | Tenant Directory, Initial Admin Provisioning, User Directory, Programs, and Executive Governance Tasks/Risks. |
| [**SettingsProjectsTab.requirements.md**](./SettingsProjectsTab.requirements.md) | [`SettingsProjectsTab.index.md`](./index/SettingsProjectsTab.index.md) | [`SettingsProjectsTab.history.md`](./history/SettingsProjectsTab.history.md) | [`BlazorEnterprise.Web/Components/Pages/Settings/SettingsProjectsTab.razor`](../../src/BlazorEnterprise.Web/Components/Pages/Settings/SettingsProjectsTab.razor) | [SettingsPage.requirements.md](./SettingsPage.requirements.md) | Settings UI | [**[ALERT: 2 Review]**](./validation/ComponentRegistrationWarnings.md) | Project Workspaces management, Program filtering, and per-project RBAC Role Assignments. |
| [**LoginPage.requirements.md**](./LoginPage.requirements.md) | [`LoginPage.index.md`](./index/LoginPage.index.md) | [`LoginPage.history.md`](./history/LoginPage.history.md) | [`BlazorEnterprise.Web/Components/Pages/LoginPage.razor`](../../src/BlazorEnterprise.Web/Components/Pages/LoginPage.razor) | *(Root)* | Authentication UI | OK (0) | Zero-Trust authentication entry point, local dev email login, and multi-tenant session establishment. |
| [**BatchJobQueueListener.requirements.md**](./BatchJobQueueListener.requirements.md) | [`BatchJobQueueListener.index.md`](./index/BatchJobQueueListener.index.md) | [`BatchJobQueueListener.history.md`](./history/BatchJobQueueListener.history.md) | [`BlazorEnterprise.Jobs/Listeners/BatchJobQueueListener.cs`](../../src/BlazorEnterprise.Jobs/Listeners/BatchJobQueueListener.cs) | *(Root)* | Batch & Background Jobs | OK (0) | Background channel listener that dequeues and executes asynchronous tenant-scoped batch jobs. |

---

## Clean 4-Tier Architecture (docs/specs/)

1. **docs/specs/<Page>.requirements.md (For YOU — Pure Requirements & Business Context)**:
   - Contains **only** ## 1. Pending Changes (Write in Plain English), ## 2. Permanent & Implementation Rules, and ## 3. Business Context (~40 lines total).
   - **Zero clutter**: No UI inventory duplication, no technical line-number tables, no validation warnings, and no old history logs inside .requirements.md!
2. **docs/specs/index/<Page>.index.md (Single Source of Truth for AI Navigation Coordinates)**:
   - Auto-generated compact **1. UI Map**, **2. Method Map**, **3. Service Map**, and **4. Validation Rules** (grouped by ### Service.Method) with exact [Lstart-Lend] line coordinates.
3. **docs/specs/history/<Page>.history.md (Completed History Archive by Date)**:
   - Stores all completed requirements under ### YYYY-MM-DD HH:mm date stamps so historical logs consume **0 tokens** during active code generation.
4. **docs/specs/validation/ComponentRegistrationWarnings.md (Unregistered Child Component Validation)**:
   - Holds any unregistered child Razor component alerts and ready-to-paste "children" JSON outside of .requirements.md.

### Example: 4-Step Smart Plan-Then-Execute Lifecycle (Optimal Token & Thinking Usage)
1. **Write Changes in Plain English (with Optional Tags & Acceptance Criteria for Complex Tasks)**: Open `docs/specs/SettingsTenantsTab.requirements.md` and write inside `<!-- BEGIN PENDING CHANGES -->`:
```markdown
<!-- BEGIN PENDING CHANGES -->
1. [UI] Remove Programs, Users, Tasks & Risks, and Dates buttons from the tenant row in the Tenant Directory table.
2. [UI] When clicking a tenant, open a popup and show all the details in edit mode.
   Acceptance Criteria:
   - Slug, Name, and Domains remain read-only; Start Date and End Date are editable.
   - Inside the popup, show tabs for Dates, Users, Programs, and Tasks & Risks.
<!-- END PENDING CHANGES -->
```
2. **Phase 1 — Automatic Pre-Flight Sync (-EnsureFresh) & Focused Planning**: Prompt Antigravity:
   > `Implement @[docs/specs/SettingsTenantsTab.requirements.md]`
   - **RULE 0 (-EnsureFresh)**: The agent first runs pwsh -NoProfile -File scripts/Generate-Specs.ps1 -TargetFilter 'SettingsTenantsTab' -EnsureFresh (<50ms SHA-256 check). If external code changes occurred, it refreshes index/SettingsTenantsTab.index.md while keeping your Section 1 text intact.
   - **RULE 1–4**: The agent reads docs/specs/index/SettingsTenantsTab.index.md, reads only the exact [Lstart-Lend] slices needed, applies **Reuse-First** & **No-New-Architecture** rules, outputs a concise **Execution Plan**, and **pauses for your approval**.
3. **Phase 2 — Approve & Surgical Execution**: Reply `yes` (or click **Proceed**) to apply the exact surgical code edits.
4. **Automatic Date-Stamped Archival & Output Contract**: Once edits are applied, the agent automatically runs:
```powershell
pwsh -NoProfile -File scripts/Generate-Specs.ps1 -TargetFilter 'SettingsTenantsTab' -ArchiveCompleted
```
   This moves your completed items from **Section 1** into [docs/specs/history/SettingsTenantsTab.history.md](./history/SettingsTenantsTab.history.md) stamped with `### YYYY-MM-DD HH:mm`, resets Section 1, refreshes the Navigation Index line numbers, and outputs the standardized **Output Contract** (Files Modified, Change Summary, Validation Performed, Risks).

### Viewing Full Visual Mermaid Sequence Diagrams (Optional)
```powershell
pwsh scripts/Generate-Specs.ps1 -GenerateHtml
```
