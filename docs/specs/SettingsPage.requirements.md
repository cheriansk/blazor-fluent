<!-- ===========================================================================
[SYSTEM / AGENT RUNTIME - STRICT MINIMAL-TOKEN READING & EXECUTION PROTOCOL]
thinking_strategy: zero_waste_discovery_focused_planning
thinking_level_hint: low (0% wasted on file/line search; 100% focused on planning UI/logic changes)
agent_mode: targeted_surgical_edit
token_budget_policy: minimal_context_reads
source_file: src/BlazorEnterprise.Web/Components/Pages/Settings/SettingsPage.razor
code_behind: src/BlazorEnterprise.Web/Components/Pages/Settings/SettingsPage.razor.cs
generated_at_utc: 2026-10-29T15:32:55Z
source_hash_sha256: e4482aa5bd4e0df0

MANDATORY AGENT RULES (READ BEFORE TAKING ANY ACTION):
  RULE 0 [PRE-FLIGHT FRESHNESS & LINE-NUMBER SYNC]: Before reading source slices or planning, verify the spec's line numbers are in sync with live code by running: pwsh -NoProfile -File scripts/Generate-Specs.ps1 -TargetFilter 'SettingsPage' -EnsureFresh (exits in <50ms if unchanged, or refreshes line numbers while preserving Section 1 Pending Changes if external edits occurred). If it regenerates, re-read the updated Agent Code Index.
  RULE 1 [WHAT TO READ IN THIS MD FILE]: Read ONLY '## 1. What I Want Changed' (and 'Permanent Rules') plus the 'Agent Code Index' (Sections A, B, C at the bottom). Use '## 2. Current Page Overview' only for quick orientation.
  RULE 2 [WHAT TO SKIP IN THIS MD FILE]: Completely IGNORE the 'Change History (Completed Requirements by Date)' block unless the user explicitly asks about historical changes.
  RULE 3 [SURGICAL LINE-SLICE READS ONLY - NEVER READ FULL SOURCE FILES]: Do NOT call read_file on the entire .razor, .razor.cs, or service .cs files, and do NOT run broad codebase searches. Look up the exact [Lstart-Lend] range in the 'Agent Code Index' below and call read_file ONLY with explicit StartLine and EndLine (+/- 10 lines of context) for the specific section or method being modified.
  RULE 4 [PHASE 1 - PLAN FIRST & STOP FOR APPROVAL]: Map each plain-English requirement in Section 1 to the exact target file, line range, @bind property, and C# method from the Agent Code Index. Output a concise 3-6 bullet 'Execution Plan' in chat and STOP. Wait for the user to reply 'yes' / click Proceed before editing any files.
  RULE 5 [PHASE 2 - SURGICAL EDIT, AUTO-ARCHIVE & FORBIDDEN COMMANDS]: Once approved, apply targeted edit_file calls, then immediately run: pwsh -NoProfile -File scripts/Generate-Specs.ps1 -TargetFilter 'SettingsPage' -ArchiveCompleted (moves completed items to Change History with date/time and refreshes line numbers). NEVER run: ["dotnet build", "dotnet test"].
============================================================================ -->
# Spec: `SettingsPage`

## 1. What I Want Changed (Write in Plain English)
<!-- Write your requirements in plain English below (no line numbers or code syntax needed), then prompt: Implement @[docs/specs/SettingsPage.requirements.md] -->
<!-- BEGIN PENDING CHANGES -->
- [ ] *(Add pending change requests here. Then prompt Antigravity: `Implement the pending changes in @[docs/specs/SettingsPage.requirements.md]`)*
<!-- END PENDING CHANGES -->

### Permanent Rules (Optional)
<!-- BEGIN CUSTOM BUSINESS RULES -->
- *(Document permanent domain invariants or custom business rules here. Preserved across regenerations.)*
<!-- END CUSTOM BUSINESS RULES -->

## [ALERT] <span style="color:#ef4444">UNREGISTERED CHILD COMPONENTS DETECTED - REVIEW REQUIRED</span>
> [!CAUTION]
> **8 Unregistered Child Component(s) in `SettingsPage.razor`!** Copy desired entries below into `"children"` of `BlazorEnterprise.Web/Components/Pages/Settings/SettingsPage.razor` in `docs/specs/specs-manifest.json` (or add to `ignore_components`).

```json
"children": [
    {
      "path": "BlazorEnterprise.Web/Components/Pages/Settings/SettingsProgramsTab.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <SettingsProgramsTab /> rendered by SettingsPage.razor at L99 (Gate: _activeTabId == "programs" && _visitedTabs.Contains("programs")).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Pages/Settings/SettingsJobsTab.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <SettingsJobsTab /> rendered by SettingsPage.razor at L107 (Gate: _activeTabId == "jobs" && _visitedTabs.Contains("jobs")).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Pages/Settings/SettingsEventsTab.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <SettingsEventsTab /> rendered by SettingsPage.razor at L111 (Gate: _activeTabId == "events" && _visitedTabs.Contains("events")).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Pages/Settings/SettingsAuditTab.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <SettingsAuditTab /> rendered by SettingsPage.razor at L115 (Gate: _activeTabId == "auditing" && _visitedTabs.Contains("auditing")).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Pages/Settings/SettingsSessionsTab.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <SettingsSessionsTab /> rendered by SettingsPage.razor at L119 (Gate: _activeTabId == "sessions" && _visitedTabs.Contains("sessions")).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Pages/Settings/SettingsUserTab.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <SettingsUserTab /> rendered by SettingsPage.razor at L123 (Gate: _activeTabId == "user" && _visitedTabs.Contains("user")).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Pages/Settings/SettingsUserCalendarTab.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <SettingsUserCalendarTab /> rendered by SettingsPage.razor at L127 (Gate: _activeTabId == "calendar" && _visitedTabs.Contains("calendar")).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Pages/Settings/SettingsNotificationsTab.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <SettingsNotificationsTab /> rendered by SettingsPage.razor at L131 (Gate: _activeTabId == "notifications" && _visitedTabs.Contains("notifications")).",
      "enabled": true
    }
]
```

## 2. Current Page Overview (Plain-English Summary)
- **Purpose**: Root Settings shell managing navigation tabs and lazy-loaded administration panels.
- **Page Route**: `/settings`
- **Child Tabs / Components**: [`<SettingsTenantsTab />`](./SettingsTenantsTab.requirements.md), [`<SettingsProjectsTab />`](./SettingsProjectsTab.requirements.md)
- **Modern Native CSK Tabs Navigation Bar**: Buttons: **Tenants**, **Programs**, **Projects**, **Jobs**, **Events**, **Audit Trail**, **Sessions**, **User**, **My Calendar**, **Notifications**

<details>
<summary><b>Change History (Completed Requirements by Date)</b></summary>

<!-- BEGIN CHANGE HISTORY -->
*(No archived changes yet. Completed requirements are automatically moved here with a date/time stamp when `-ArchiveCompleted` is run.)*
<!-- END CHANGE HISTORY -->
</details>

<details>
<summary><b>Agent Code Index (Auto-Generated Line Numbers, Bindings & Service Map - For AI Only)</b></summary>

### A. Technical UI Layout & Binding Tree (`SettingsPage.razor`)
- **Auth**: `Authorize`
- **Injected**: `SettingsNav:ISettingsNavigationState`, `TenantContext:ITenantContext`, `CurrentUser:ICurrentUser`, `HierarchyAuth:IHierarchyAuthorizationService`
- **Registered Child Components**:
  - `<SettingsTenantsTab />` @ `L95` | Gate: `_activeTabId == "tenants" && _visitedTabs.Contains("tenants")` | Params: `None` | Spec: [SettingsTenantsTab.requirements.md](./SettingsTenantsTab.requirements.md)
  - `<SettingsProjectsTab />` @ `L103` | Gate: `_activeTabId == "projects" && _visitedTabs.Contains("projects")` | Params: `None` | Spec: [SettingsProjectsTab.requirements.md](./SettingsProjectsTab.requirements.md)
- **UI Sections, Inputs (@bind), Buttons (OnClick) & DataGrids**:
  - `[L16-L90]` **Modern Native CSK Tabs Navigation Bar**
    - **Inner Gates**: `L18: @if (CanViewTenantTab)`, `L27: @if (CanViewProgramTab)`, `L54: @if (CanViewAuditTrail)`, `L63: @if (CurrentUser.IsRootAdmin)`
    - **Buttons**: `Btn("Tenants" -> SelectTab)@L20`, `Btn("Programs" -> SelectTab)@L29`, `Btn("Projects" -> SelectTab)@L36`, `Btn("Jobs" -> SelectTab)@L42`, `Btn("Events" -> SelectTab)@L48`, `Btn("Audit Trail" -> SelectTab)@L56`, `Btn("Sessions" -> SelectTab)@L65`, `Btn("User" -> SelectTab)@L72`, `Btn("My Calendar" -> SelectTab)@L78`, `Btn("Notifications" -> SelectTab)@L84`
  - `[L91-L134]` **Active Tab Content Area with Lazy Load Preservation**
    - **Inner Gates**: `L93: @if (_activeTabId == "tenants" && _visitedTabs.Contains("tenants"))`, `L97: @if (_activeTabId == "programs" && _visitedTabs.Contains("programs"))`, `L101: @if (_activeTabId == "projects" && _visitedTabs.Contains("projects"))`, `L105: @if (_activeTabId == "jobs" && _visitedTabs.Contains("jobs"))`, `L109: @if (_activeTabId == "events" && _visitedTabs.Contains("events"))`, `L113: @if (_activeTabId == "auditing" && _visitedTabs.Contains("auditing"))`, `L117: @if (_activeTabId == "sessions" && _visitedTabs.Contains("sessions"))`, `L121: @if (_activeTabId == "user" && _visitedTabs.Contains("user"))`, `L125: @if (_activeTabId == "calendar" && _visitedTabs.Contains("calendar"))`, `L129: @if (_activeTabId == "notifications" && _visitedTabs.Contains("notifications"))`

### B. Method Map & End-to-End Call Chains (`BlazorEnterprise.Web/Components/Pages/Settings/SettingsPage.razor.cs`)
| Method | Lines | Role / Trigger | End-to-End Call Chain ([PreGates] -> Service.Method[Gates](DB/Audit)) |
| :--- | :--- | :--- | :--- |
| `OnInitializedAsync` | `L26-L74` | [INIT] | `[IsRootAdmin,TenantBoundaryGate]` -> `HierarchyAuth.CanViewTenantSettingsAsync(DB[R]:Tenants)` -> `HierarchyAuth.GetAccessibleProgramIdsAsync(DB[R]:Tenants)` |
| `SelectTab` | `L76-L80` | OnClick@L22, OnClick@L31, OnClick@L38, OnClick@L44, OnClick@L50, OnClick@L58, OnClick@L67, OnClick@L74, OnClick@L80, OnClick@L86 | Local UI State |

### C. Downstream Service Endpoints & Validation Rules
| Service Method | Source Location | Security Gates | DB & Audit | Result.Failure Conditions |
| :--- | :--- | :--- | :--- | :--- |
| `HierarchyAuthorizationService.CanViewTenantSettingsAsync` | `BlazorEnterprise.Persistence/Services/HierarchyAuthorizationService.cs#L394-L410` | - | `DB[R]: Tenants` | - |
| `HierarchyAuthorizationService.GetAccessibleProgramIdsAsync` | `BlazorEnterprise.Persistence/Services/HierarchyAuthorizationService.cs#L822-L838` | - | `DB[R]: Tenants` | - |

</details>

