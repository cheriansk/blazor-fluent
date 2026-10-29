<!-- ===========================================================================
[SYSTEM / AGENT RUNTIME - STRICT MINIMAL-TOKEN READING & EXECUTION PROTOCOL]
thinking_strategy: zero_waste_discovery_focused_planning
thinking_level_hint: low (0% wasted on file/line search; 100% focused on planning UI/logic changes)
agent_mode: targeted_surgical_edit
token_budget_policy: minimal_context_reads
source_file: src/BlazorEnterprise.Web/Components/Pages/Settings/SettingsProjectsTab.razor
code_behind: src/BlazorEnterprise.Web/Components/Pages/Settings/SettingsProjectsTab.razor.cs
parent_spec: docs/specs/SettingsPage.requirements.md (src/BlazorEnterprise.Web/Components/Pages/Settings/SettingsPage.razor)
generated_at_utc: 2026-10-29T15:32:56Z
source_hash_sha256: ee8166bc70e0be6f

MANDATORY AGENT RULES (READ BEFORE TAKING ANY ACTION):
  RULE 0 [PRE-FLIGHT FRESHNESS & LINE-NUMBER SYNC]: Before reading source slices or planning, verify the spec's line numbers are in sync with live code by running: pwsh -NoProfile -File scripts/Generate-Specs.ps1 -TargetFilter 'SettingsProjectsTab' -EnsureFresh (exits in <50ms if unchanged, or refreshes line numbers while preserving Section 1 Pending Changes if external edits occurred). If it regenerates, re-read the updated Agent Code Index.
  RULE 1 [WHAT TO READ IN THIS MD FILE]: Read ONLY '## 1. What I Want Changed' (and 'Permanent Rules') plus the 'Agent Code Index' (Sections A, B, C at the bottom). Use '## 2. Current Page Overview' only for quick orientation.
  RULE 2 [WHAT TO SKIP IN THIS MD FILE]: Completely IGNORE the 'Change History (Completed Requirements by Date)' block unless the user explicitly asks about historical changes.
  RULE 3 [SURGICAL LINE-SLICE READS ONLY - NEVER READ FULL SOURCE FILES]: Do NOT call read_file on the entire .razor, .razor.cs, or service .cs files, and do NOT run broad codebase searches. Look up the exact [Lstart-Lend] range in the 'Agent Code Index' below and call read_file ONLY with explicit StartLine and EndLine (+/- 10 lines of context) for the specific section or method being modified.
  RULE 4 [PHASE 1 - PLAN FIRST & STOP FOR APPROVAL]: Map each plain-English requirement in Section 1 to the exact target file, line range, @bind property, and C# method from the Agent Code Index. Output a concise 3-6 bullet 'Execution Plan' in chat and STOP. Wait for the user to reply 'yes' / click Proceed before editing any files.
  RULE 5 [PHASE 2 - SURGICAL EDIT, AUTO-ARCHIVE & FORBIDDEN COMMANDS]: Once approved, apply targeted edit_file calls, then immediately run: pwsh -NoProfile -File scripts/Generate-Specs.ps1 -TargetFilter 'SettingsProjectsTab' -ArchiveCompleted (moves completed items to Change History with date/time and refreshes line numbers). NEVER run: ["dotnet build", "dotnet test"].
============================================================================ -->
# Spec: `SettingsProjectsTab`

## 1. What I Want Changed (Write in Plain English)
<!-- Write your requirements in plain English below (no line numbers or code syntax needed), then prompt: Implement @[docs/specs/SettingsProjectsTab.requirements.md] -->
<!-- BEGIN PENDING CHANGES -->
- [ ] *(Add pending change requests here. Then prompt Antigravity: `Implement the pending changes in @[docs/specs/SettingsProjectsTab.requirements.md]`)*
<!-- END PENDING CHANGES -->

### Permanent Rules (Optional)
<!-- BEGIN CUSTOM BUSINESS RULES -->
- *(Document permanent domain invariants or custom business rules here. Preserved across regenerations.)*
<!-- END CUSTOM BUSINESS RULES -->

## [ALERT] <span style="color:#ef4444">UNREGISTERED CHILD COMPONENTS DETECTED - REVIEW REQUIRED</span>
> [!CAUTION]
> **2 Unregistered Child Component(s) in `SettingsProjectsTab.razor`!** Copy desired entries below into `"children"` of `BlazorEnterprise.Web/Components/Pages/Settings/SettingsProjectsTab.razor` in `docs/specs/specs-manifest.json` (or add to `ignore_components`).

```json
"children": [
    {
      "path": "BlazorEnterprise.Web/Components/Common/EntityHistoryButton.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <EntityHistoryButton /> rendered by SettingsProjectsTab.razor at L182 (Gate: context.Program is not null).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Common/DataListSelect.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <DataListSelect /> rendered by SettingsProjectsTab.razor at L300 (Gate: !_isEditingDetails).",
      "enabled": true
    }
]
```

## 2. Current Page Overview (Plain-English Summary)
- **Purpose**: Project Workspaces management, Program filtering, and per-project RBAC Role Assignments.
- **Tenant Filter Toolbar**: Buttons: **All Programs**, **Clear Filter**, **Refresh**
- **Projects Data Grid Card**: Fields: *Filter projects by name, code, location...* | Buttons: `SelectProjectAsync` | Table (Code, Project Name, Program, Status, Location, Timeline, Actions)
- **Selected Project Sub-Tabs Panel (Details & Roles)**: Buttons: **Close Panel**
- **Tab 1: Project Details**: Buttons: **Edit Details**
- **Edit Project Details Form**: Fields: *Project Name*, *Project Short Code*, *Location*, *Status*, *Start Date*, *Due / Target Completion Date*, `_editScopeSummary` | Buttons: **Save Changes**, **Cancel**
- **Tab 2: Project Roles & Access Management**: Buttons: **Assign User**, **Refresh**
- **Role Assignment Form (Collapsible Card)**: Fields: *Project Role* | Buttons: **Close**, **Save Assignment**, **Cancel**
- **Impersonation Form (Collapsible Card)**: Fields: *Justification / Incident Ticket (Required)* | Buttons: **Cancel**, `submit`
- **Members Grid**: Fields: *Filter members by name, email, or role...* | Buttons: **Edit**, **Revoke**, **Impersonate** | Table (Member, Email, Type, Role, Actions)

<details>
<summary><b>Change History (Completed Requirements by Date)</b></summary>

<!-- BEGIN CHANGE HISTORY -->
*(No archived changes yet. Completed requirements are automatically moved here with a date/time stamp when `-ArchiveCompleted` is run.)*
<!-- END CHANGE HISTORY -->
</details>

<details>
<summary><b>Agent Code Index (Auto-Generated Line Numbers, Bindings & Service Map - For AI Only)</b></summary>

### A. Technical UI Layout & Binding Tree (`SettingsProjectsTab.razor`)
- **Injected**: `TenantContext:ITenantContext`, `TenantService:ITenantService`, `ProgramService:IProgramService`, `CurrentUser:ICurrentUser`, `AuditService:IAuditService`, `ProjectAuth:IProjectAuthorizationService`, `ImpersonationService:IImpersonationService`, `AuthStateProvider:CurrentUserAuthenticationStateProvider`, `NavigationManager:NavigationManager`, `UnsavedChanges:IUnsavedChangesStateService`, `TimeZoneService:IUserTimeZoneService`
- **UI Sections, Inputs (@bind), Buttons (OnClick) & DataGrids**:
  - `[L1-L29]` **Header & Top-Level Controls**
    - **Inner Gates**: `L23: @if (!string.IsNullOrWhiteSpace(_statusMessage))`
  - `[L30-L88]` **Tenant Filter Toolbar**
    - **Inner Gates**: `L45: @if (!string.IsNullOrWhiteSpace(_selectedTenantId))`, `L47: @if (_availablePrograms.Count > 1)`, `L60: @if (!string.IsNullOrWhiteSpace(_selectedProgramId))`, `L70: @if (_availableTenants.Count > 1)`, `L87: @if (string.IsNullOrWhiteSpace(_selectedTenantId))`
    - **Inputs**: `CskSelect()@L33`, `CskSelect()@L49`
    - **Buttons**: `Btn("All Programs" -> ClearProgramFilter)@L62`, `Btn("Clear Filter" -> ClearTenantFilter)@L72`, `Btn("Refresh" -> LoadProjectsForTenantAsync)@L78`
  - `[L101-L188]` **Projects Data Grid Card**
    - **Inner Gates**: `L120: @if (_isLoading)`, `L126: @if (_projects.Count == 0)`, `L139: @if (context.Program is not null)`
    - **Inputs**: `CskTextInput("Filter projects by name, ...": _projectSearchText)@L110`
    - **Buttons**: `Btn(SelectProjectAsync)@L173`
    - **DataGrids**: `CskDataGrid(Items=FilteredProjects, Cols=[Code, Project Name, Program, Status, Location, Timeline, Actions])@L135`
  - `[L189-L226]` **Selected Project Sub-Tabs Panel (Details & Roles)** — Gate: `@if (_selectedProject is not null)`
    - **Inner Gates**: `L197: @if (!string.IsNullOrWhiteSpace(_selectedProject.ShortCode))`
    - **Buttons**: `Btn("Close Panel" -> _selectedProject = null)@L221`
  - `[L227-L287]` **Tab 1: Project Details**
    - **Tabs**: `Tab[tab-details] @L228`
    - **Inner Gates**: `L235: @if (!_isEditingDetails)`
    - **Buttons**: `Btn("Edit Details" -> StartEditDetails)@L280`
  - `[L288-L342]` **Edit Project Details Form**
    - **Inputs**: `CskTextInput("Project Name": _editName)@L293`, `CskTextInput("Project Short Code": _editShortCode)@L294`, `CskTextInput("Location": _editLocation)@L298`, `DataListSelect("Status": _editStatus)@L300`, `CskSelect()@L306`, `CskDatePicker("Start Date": _editStartDate)@L317`, `CskDatePicker("Due / Target Completion Date": _editEndDate)@L318`, `CskTextArea(_editScopeSummary)@L323`
    - **Buttons**: `Btn("Save Changes" -> SaveProjectDetailsAsync)@L327`, `Btn("Cancel" -> CancelEditDetails)@L332`
  - `[L343-L367]` **Tab 2: Project Roles & Access Management**
    - **Tabs**: `Tab[tab-roles] @L344`
    - **Buttons**: `Btn("Assign User" -> OpenAddRoleModal)@L359`, `Btn("Refresh" -> LoadProjectMembersAsync)@L362`
  - `[L368-L444]` **Role Assignment Form (Collapsible Card)** — Gate: `@if (_showAssignRoleModal)`
    - **Inner Gates**: `L376: @if (!_isEditingRole && _tenantUsers.Count == 0)`, `L388: @if (!_isEditingRole)`, `L401: @if (!string.IsNullOrWhiteSpace(_formEmail))`, `L407: @if (_formUserType == UserTypeEnum.CompanyUser)`
    - **Inputs**: `CskSelect()@L390`, `DataListSelect("Project Role": _formRole)@L427`
    - **Buttons**: `Btn("Close" -> _showAssignRoleModal = false)@L381`, `Btn("Save Assignment" -> SaveAssignmentAsync)@L431`, `Btn("Cancel" -> _showAssignRoleModal = false)@L436`
  - `[L445-L483]` **Impersonation Form (Collapsible Card)** — Gate: `@if (_showImpersonateModal)`
    - **Inputs**: `CskTextInput("Justification / Incident ...": _impersonateReason)@L459`
    - **Buttons**: `Btn("Cancel" -> _showImpersonateModal = false)@L470`, `Btn(submit)@L473`
  - `[L484-L606]` **Members Grid** — Gate: `@if (_isLoadingMembers)`
    - **Inner Gates**: `L491: @if (_members.Count == 0)`, `L517: @if (BlazorEnterprise.Core.Utilities.SystemIdentityUtility.IsSystemDaemon(context.UserEmail))`, `L528: @if (context.UserType == UserTypeEnum.CompanyUser)`, `L565: @if (BlazorEnterprise.Core.Utilities.SystemIdentityUtility.IsSystemDaemon(context.UserEmail))`, `L580: @if (CurrentUser.IsRootAdmin && context.UserId != CurrentUser.UserId)`
    - **Inputs**: `CskTextInput("Filter members by name, e...": _memberFilterText)@L500`
    - **Buttons**: `Btn("Edit" -> EditAssignment)@L573`, `Btn("Revoke" -> RevokeAssignmentAsync)@L576`, `Btn("Impersonate" -> PromptImpersonation)@L582`
    - **DataGrids**: `CskDataGrid(Items=FilteredMembers, Cols=[Member, Email, Type, Role, Actions])@L513`

### B. Method Map & End-to-End Call Chains (`BlazorEnterprise.Web/Components/Pages/Settings/SettingsProjectsTab.razor.cs`)
| Method | Lines | Role / Trigger | End-to-End Call Chain ([PreGates] -> Service.Method[Gates](DB/Audit)) |
| :--- | :--- | :--- | :--- |
| `TenantFilterOption` | `L26-L26` | [HELPER] | Local UI State |
| `ProgramFilterOption` | `L27-L27` | [HELPER] | Local UI State |
| `OnInitializedAsync` | `L123-L127` | [INIT] | `local:LoadAvailableTenantsAsync()` -> `TenantService.GetAllTenantsAsync[IsRootAdmin](DB[R]:Tenants)` |
| `LoadAvailableTenantsAsync` | `L129-L154` | [HELPER] | `[IsRootAdmin]` -> `local:TenantFilterOption()` -> `local:OnTenantSelected()` -> `TenantService.GetAllTenantsAsync[IsRootAdmin](DB[R]:Tenants)` |
| `OnProgramSelected` | `L156-L159` | ValueChanged@L55 | Local UI State |
| `ClearProgramFilter` | `L161-L164` | OnClick@L64 | Local UI State |
| `OnTenantSelected` | `L166-L184` | ValueChanged@L39 | `local:LoadProjectsForTenantAsync()` -> `TenantService.GetProjectsByTenantIdAsync[IsRootAdmin,TenantBoundaryGate](DB[R]:Projects)` -> `ProgramService.GetProgramsAsync(DB[R]:Tenants,Programs)` |
| `ClearTenantFilter` | `L186-L196` | OnClick@L74 | Local UI State |
| `LoadProjectsForTenantAsync` | `L198-L236` | OnClick@L80 | `local:ProgramFilterOption()` -> `local:SelectProjectAsync()` -> `TenantService.GetProjectsByTenantIdAsync[IsRootAdmin,TenantBoundaryGate](DB[R]:Projects)` -> `ProgramService.GetProgramsAsync(DB[R]:Tenants,Programs)` |
| `SelectProjectAsync` | `L238-L245` | OnClick@L176 | `local:LoadTenantUsersForProjectAsync()` -> `local:LoadProjectMembersAsync()` -> `TenantService.GetTenantUsersByIdAsync[IsRootAdmin,TenantBoundaryGate](DB[R]:TenantUsers)` -> `ProjectAuth.GetProjectMembersAsync(DB[R]:ProjectUserRoles)` |
| `StartEditDetails` | `L247-L260` | OnClick@L280 | `UnsavedChanges.RegisterChange` |
| `CancelEditDetails` | `L262-L266` | OnClick@L332 | `UnsavedChanges.ClearChange` |
| `SaveProjectDetailsAsync` | `L268-L300` | OnClick@L329 | `local:LoadProjectsForTenantAsync()` -> `TenantService.UpdateTenantProjectAsync(DB[W]:Projects; Audit:LogUserActivityAsync,LogSecurityEventAsync)` -> `TenantService.GetProjectsByTenantIdAsync[IsRootAdmin,TenantBoundaryGate](DB[R]:Projects)` -> `ProgramService.GetProgramsAsync(DB[R]:Tenants,Programs)` -> `UnsavedChanges.ClearChange` |
| `LoadTenantUsersForProjectAsync` | `L302-L314` | [HELPER] | `TenantService.GetTenantUsersByIdAsync[IsRootAdmin,TenantBoundaryGate](DB[R]:TenantUsers)` |
| `LoadProjectMembersAsync` | `L316-L335` | OnClick@L362 | `ProjectAuth.GetProjectMembersAsync(DB[R]:ProjectUserRoles)` |
| `OpenAddRoleModal` | `L337-L355` | OnClick@L359 | `local:OnTenantUserSelected()` |
| `EditAssignment` | `L357-L367` | OnClick@L573 | Local UI State |
| `OnTenantUserSelected` | `L369-L386` | ValueChanged@L396 | Local UI State |
| `SaveAssignmentAsync` | `L388-L427` | OnClick@L433 | `local:LoadProjectMembersAsync()` -> `ProjectAuth.AssignUserRoleAsync(DB[W]:Users,TenantUsers,ProjectUserRoles; Audit:LogUserActivityAsync)` -> `ProjectAuth.GetProjectMembersAsync(DB[R]:ProjectUserRoles)` |
| `RevokeAssignmentAsync` | `L429-L453` | OnClick@L576 | `local:LoadProjectMembersAsync()` -> `ProjectAuth.RevokeUserRoleAsync(DB[W]:ProjectUserRoles; Audit:LogUserActivityAsync)` -> `ProjectAuth.GetProjectMembersAsync(DB[R]:ProjectUserRoles)` |
| `PromptImpersonation` | `L455-L462` | OnClick@L582 | Local UI State |
| `StartImpersonationAsync` | `L464-L490` | [HELPER] | `ImpersonationService.StartImpersonationAsync()` -> `AuthStateProvider.NotifyUserChanged()` -> `NavigationManager.NavigateTo()` |
| `HandleTimeZoneChanged` | `L492-L495` | [HELPER] | Local UI State |
| `Dispose` | `L497-L501` | [HELPER] | `UnsavedChanges.ClearChange` |

### C. Downstream Service Endpoints & Validation Rules
| Service Method | Source Location | Security Gates | DB & Audit | Result.Failure Conditions |
| :--- | :--- | :--- | :--- | :--- |
| `TenantService.GetAllTenantsAsync` | `BlazorEnterprise.Persistence/Services/TenantService.cs#L94-L116` | `IsRootAdmin` | `DB[R]: Tenants` | - |
| `TenantService.GetProjectsByTenantIdAsync` | `BlazorEnterprise.Persistence/Services/TenantService.cs#L1083-L1106` | `IsRootAdmin, TenantBoundaryGate` | `DB[R]: Projects` | - |
| `ProgramService.GetProgramsAsync` | `BlazorEnterprise.Persistence/Services/ProgramService.cs#L41-L70` | - | `DB[R]: Tenants,Programs` | - |
| `TenantService.GetTenantUsersByIdAsync` | `BlazorEnterprise.Persistence/Services/TenantService.cs#L1066-L1081` | `IsRootAdmin, TenantBoundaryGate` | `DB[R]: TenantUsers` | - |
| `ProjectAuthorizationService.GetProjectMembersAsync` | `BlazorEnterprise.Persistence/Services/ProjectAuthorizationService.cs#L130-L140` | - | `DB[R]: ProjectUserRoles` | - |
| `UnsavedChangesStateService.RegisterChange` | `BlazorEnterprise.Persistence/Services/UnsavedChangesStateService.cs#L18-L24` | - | In-Memory | - |
| `UnsavedChangesStateService.ClearChange` | `BlazorEnterprise.Persistence/Services/UnsavedChangesStateService.cs#L26-L34` | - | In-Memory | - |
| `TenantService.UpdateTenantProjectAsync` | `BlazorEnterprise.Persistence/Services/TenantService.cs#L856-L938` | - | `DB[W]: Projects` `Audit: LogUserActivityAsync,LogSecurityEventAsync` | `"Project Name is required."`; `"Project Short Code is required."`; `"End date cannot precede start date."`; `$"Tenant '{tenantSlug}' was not found."`; `$"Project was not found in tenant '{tenantSlug}'."`; `$"Another project with name '{trimmedName}' or short code '{trimmedCode}' alr..."` |
| `ProjectAuthorizationService.AssignUserRoleAsync` | `BlazorEnterprise.Persistence/Services/ProjectAuthorizationService.cs#L142-L223` | - | `DB[W]: Users,TenantUsers,ProjectUserRoles` `Audit: LogUserActivityAsync` | `"Only Project Admins, parent Program Admins, or Tenant Admins can assign or c..."`; `$"User '{userEmail}' could not be resolved in the user directory."`; `$"Zero-Trust Boundary Violation: User '{userEmail}' is not an active member o..."` |
| `ProjectAuthorizationService.RevokeUserRoleAsync` | `BlazorEnterprise.Persistence/Services/ProjectAuthorizationService.cs#L225-L262` | - | `DB[W]: ProjectUserRoles` `Audit: LogUserActivityAsync` | `"Only Project Admins, parent Program Admins, or Tenant Admins can revoke proj..."`; `"No active role assignment found for the specified user and project."` |

</details>

