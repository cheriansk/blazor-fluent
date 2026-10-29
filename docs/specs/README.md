# Antigravity Ultra-Lean Specifications Catalog

> **Last Generated**: `2026-10-29 11:32:57 -04:00`  
> **Manifest Source**: [specs-manifest.json](./specs-manifest.json)  
> **Regenerate Command**: `pwsh scripts/Generate-Specs.ps1` (or `pwsh scripts/Generate-Specs.ps1 -GenerateHtml` for visual Mermaid HTML explorer)

## Generated Target Specifications

| Spec File | Originating Source | Parent Spec | Category | Thinking | Methods / Services | Unregistered Children | Summary |
| :--- | :--- | :--- | :--- | :---: | :---: | :---: | :--- |
| [**SettingsPage.requirements.md**](./SettingsPage.requirements.md) | [`BlazorEnterprise.Web/Components/Pages/Settings/SettingsPage.razor`](../../src/BlazorEnterprise.Web/Components/Pages/Settings/SettingsPage.razor) | *(Root)* | Settings UI | `low` | 2 / 2 | **[ALERT: 8 Review Required]** | Root Settings shell managing navigation tabs and lazy-loaded administration panels. |
| [**SettingsTenantsTab.requirements.md**](./SettingsTenantsTab.requirements.md) | [`BlazorEnterprise.Web/Components/Pages/Settings/SettingsTenantsTab.razor`](../../src/BlazorEnterprise.Web/Components/Pages/Settings/SettingsTenantsTab.razor) | [SettingsPage.requirements.md](./SettingsPage.requirements.md) | Settings UI | `low` | 41 / 21 | **[ALERT: 2 Review Required]** | Tenant Directory, Initial Admin Provisioning, User Directory, Programs, and Executive Governance Tasks/Risks. |
| [**SettingsProjectsTab.requirements.md**](./SettingsProjectsTab.requirements.md) | [`BlazorEnterprise.Web/Components/Pages/Settings/SettingsProjectsTab.razor`](../../src/BlazorEnterprise.Web/Components/Pages/Settings/SettingsProjectsTab.razor) | [SettingsPage.requirements.md](./SettingsPage.requirements.md) | Settings UI | `low` | 24 / 10 | **[ALERT: 2 Review Required]** | Project Workspaces management, Program filtering, and per-project RBAC Role Assignments. |
| [**LoginPage.requirements.md**](./LoginPage.requirements.md) | [`BlazorEnterprise.Web/Components/Pages/LoginPage.razor`](../../src/BlazorEnterprise.Web/Components/Pages/LoginPage.razor) | *(Root)* | Authentication UI | `low` | 8 / 3 | OK (0) | Zero-Trust authentication entry point, local dev email login, and multi-tenant session establishment. |
| [**BatchJobQueueListener.requirements.md**](./BatchJobQueueListener.requirements.md) | [`BlazorEnterprise.Jobs/Listeners/BatchJobQueueListener.cs`](../../src/BlazorEnterprise.Jobs/Listeners/BatchJobQueueListener.cs) | *(Root)* | Batch & Background Jobs | `medium` | 2 / 4 | OK (0) | Background channel listener that dequeues and executes asynchronous tenant-scoped batch jobs. |

---

## How to Write Requirements (Plain English Only!)

You **never** need to write line numbers, control names, or technical syntax. Each `.requirements.md` file is organized into:
1. **Top Half (For YOU - Plain English)**:
   - `## 1. What I Want Changed (Write in Plain English)`: Write normal everyday sentences describing what you want added, removed, or changed.
   - `## 2. Current Page Overview (Plain-English Summary)`: Clean summary of the cards, fields, buttons, and tables currently on the page.
2. **Bottom Half (Collapsed <details> - For the AI Agent Only)**:
   - Contains the auto-generated line numbers, `@bind` mappings, method call chains, and service endpoints so Antigravity knows exactly where to edit the code.

### Example: 4-Step Smart Plan-Then-Execute Lifecycle (Optimal Token & Thinking Usage)
1. **Write Changes in Plain English**: Open `docs/specs/SettingsTenantsTab.requirements.md` and write plain English inside `<!-- BEGIN PENDING CHANGES -->`:
```markdown
<!-- BEGIN PENDING CHANGES -->
1. Remove Programs, Users, Tasks & Risks, and Dates buttons from the tenant row in the Tenant Directory table.
2. When clicking a tenant, open a popup and show all the details in edit mode.
3. Inside the popup, show tabs for Dates, Users, Programs, and Tasks & Risks.
<!-- END PENDING CHANGES -->
```
2. **Phase 1 — Automatic Pre-Flight Sync (-EnsureFresh) & Focused Planning**: Prompt Antigravity:
   > `Implement @[docs/specs/SettingsTenantsTab.requirements.md]`
   - **RULE 0 (-EnsureFresh)**: The agent first runs pwsh -NoProfile -File scripts/Generate-Specs.ps1 -TargetFilter 'SettingsTenantsTab' -EnsureFresh (<50ms SHA-256 check). If external code changes occurred, it refreshes line numbers while keeping your Section 1 text intact.
   - Because the **Agent Code Index** already gives the AI every line number, @bind variable, and service call, the AI spends **0% of its thinking tokens** searching for files and **100% of its thinking tokens** designing a clean **Execution Plan**.
   - The AI presents the concise **Execution Plan** (mapping your plain-English points to exact lines/methods) and **pauses for your approval**.
3. **Phase 2 — Approve & Surgical Execution**: Reply `yes` (or click **Proceed**) to apply the exact surgical code edits.
4. **Automatic Date-Stamped Archival & Refresh**: Once edits are applied, the agent automatically runs:
```powershell
pwsh -NoProfile -File scripts/Generate-Specs.ps1 -TargetFilter 'SettingsTenantsTab' -ArchiveCompleted
```
   This moves your completed items from **Section 1 (What I Want Changed)** into the collapsible **Change History (Completed Requirements by Date)** block stamped with `#### YYYY-MM-DD HH:mm`, resets Section 1 for your next change, and refreshes Section 2 (Page Overview) and the Agent Code Index line numbers.

### Viewing Full Visual Mermaid Sequence Diagrams (Optional)
```powershell
pwsh scripts/Generate-Specs.ps1 -GenerateHtml
```
