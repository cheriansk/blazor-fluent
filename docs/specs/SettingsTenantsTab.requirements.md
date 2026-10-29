<!-- ===========================================================================
[SYSTEM / AGENT RUNTIME - STRICT MINIMAL-TOKEN READING & EXECUTION PROTOCOL]
thinking_strategy: zero_waste_discovery_focused_planning
thinking_level_hint: low (0% wasted on file/line search; 100% focused on planning UI/logic changes)
agent_mode: targeted_surgical_edit
token_budget_policy: minimal_context_reads
source_file: src/BlazorEnterprise.Web/Components/Pages/Settings/SettingsTenantsTab.razor
code_behind: src/BlazorEnterprise.Web/Components/Pages/Settings/SettingsTenantsTab.razor.cs
parent_spec: docs/specs/SettingsPage.requirements.md (src/BlazorEnterprise.Web/Components/Pages/Settings/SettingsPage.razor)
generated_at_utc: 2026-10-29T15:32:56Z
source_hash_sha256: 308111467a85dd31

MANDATORY AGENT RULES (READ BEFORE TAKING ANY ACTION):
  RULE 0 [PRE-FLIGHT FRESHNESS & LINE-NUMBER SYNC]: Before reading source slices or planning, verify the spec's line numbers are in sync with live code by running: pwsh -NoProfile -File scripts/Generate-Specs.ps1 -TargetFilter 'SettingsTenantsTab' -EnsureFresh (exits in <50ms if unchanged, or refreshes line numbers while preserving Section 1 Pending Changes if external edits occurred). If it regenerates, re-read the updated Agent Code Index.
  RULE 1 [WHAT TO READ IN THIS MD FILE]: Read ONLY '## 1. What I Want Changed' (and 'Permanent Rules') plus the 'Agent Code Index' (Sections A, B, C at the bottom). Use '## 2. Current Page Overview' only for quick orientation.
  RULE 2 [WHAT TO SKIP IN THIS MD FILE]: Completely IGNORE the 'Change History (Completed Requirements by Date)' block unless the user explicitly asks about historical changes.
  RULE 3 [SURGICAL LINE-SLICE READS ONLY - NEVER READ FULL SOURCE FILES]: Do NOT call read_file on the entire .razor, .razor.cs, or service .cs files, and do NOT run broad codebase searches. Look up the exact [Lstart-Lend] range in the 'Agent Code Index' below and call read_file ONLY with explicit StartLine and EndLine (+/- 10 lines of context) for the specific section or method being modified.
  RULE 4 [PHASE 1 - PLAN FIRST & STOP FOR APPROVAL]: Map each plain-English requirement in Section 1 to the exact target file, line range, @bind property, and C# method from the Agent Code Index. Output a concise 3-6 bullet 'Execution Plan' in chat and STOP. Wait for the user to reply 'yes' / click Proceed before editing any files.
  RULE 5 [PHASE 2 - SURGICAL EDIT, AUTO-ARCHIVE & FORBIDDEN COMMANDS]: Once approved, apply targeted edit_file calls, then immediately run: pwsh -NoProfile -File scripts/Generate-Specs.ps1 -TargetFilter 'SettingsTenantsTab' -ArchiveCompleted (moves completed items to Change History with date/time and refreshes line numbers). NEVER run: ["dotnet build", "dotnet test"].
============================================================================ -->
# Spec: `SettingsTenantsTab`

## 1. What I Want Changed (Write in Plain English)
<!-- Write your requirements in plain English below (no line numbers or code syntax needed), then prompt: Implement @[docs/specs/SettingsTenantsTab.requirements.md] -->
<!-- BEGIN PENDING CHANGES -->
- [ ] *(Add pending change requests here. Then prompt Antigravity: `Implement the pending changes in @[docs/specs/SettingsTenantsTab.requirements.md]`)*
<!-- END PENDING CHANGES -->

### Permanent Rules (Optional)
<!-- BEGIN CUSTOM BUSINESS RULES -->
- *(Document permanent domain invariants or custom business rules here. Preserved across regenerations.)*
<!-- END CUSTOM BUSINESS RULES -->

## [ALERT] <span style="color:#ef4444">UNREGISTERED CHILD COMPONENTS DETECTED - REVIEW REQUIRED</span>
> [!CAUTION]
> **2 Unregistered Child Component(s) in `SettingsTenantsTab.razor`!** Copy desired entries below into `"children"` of `BlazorEnterprise.Web/Components/Pages/Settings/SettingsTenantsTab.razor` in `docs/specs/specs-manifest.json` (or add to `ignore_components`).

```json
"children": [
    {
      "path": "BlazorEnterprise.Web/Components/Common/EntityHistoryButton.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <EntityHistoryButton /> rendered by SettingsTenantsTab.razor at L201 (Gate: _managingUsersTenant is not null).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Common/DataListSelect.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <DataListSelect /> rendered by SettingsTenantsTab.razor at L468 (Gate: _showAddProgramForm).",
      "enabled": true
    }
]
```

## 2. Current Page Overview (Plain-English Summary)
- **Purpose**: Tenant Directory, Initial Admin Provisioning, User Directory, Programs, and Executive Governance Tasks/Risks.
- **Header & Top-Level Controls**: Buttons: **Add Tenant**
- **Step 1: Provision New Tenant Form**: Fields: *Tenant Slug (Unique, URL-safe)*, *Display Name*
- **Internal Email Domains (1-5)**: Fields: *e.g. @@microsoft.com* | Buttons: **+ Add Domain**
- **External Email Domains (1-5)**: Fields: *e.g. @@salesforce.com*, *Start Date*, *End Date* | Buttons: **+ Add Domain**
- **Initial Mandatory Tenant Administrator**: Fields: *Admin Full Name*, *Admin Email* | Buttons: **Review & Confirm**, **Cancel**
- **Step 2: Mandatory Permanent Configuration Confirmation Modal**: Buttons: **Confirm & Provision Tenant**, **Back to Edit**
- **Edit Dates Modal (Slug, Name, and Domains are strictly locked)**: Fields: *Start Date*, *End Date* | Buttons: **Save Dates**, **Cancel**
- **Tenant User Management Panel**: Buttons: **Add User**, **Close**
- **Automated Background Processing Kill Switch Card**: Fields: `_isDaemonActive`
- **Add User Sub-Card**: Fields: *Full Name*, *Email Address*, `GetUserTypeDisplay()`, *Start Date*, *End Date*, *Active* | Buttons: **Save User**, **Cancel**
- **Edit User Sub-Card**: Fields: *Full Name*, *Email Address*, `GetEditUserTypeDisplay()`, *Start Date*, *End Date*, `_editUserIsActive` | Buttons: **Update User**, **Cancel**
- **Search & Filter Toolbar for Users**: Fields: *Filter users by name or email...* | Buttons: **Edit User** | Table (Name, Email, Type, Start Date, End Date, Status, Actions, Edit User)
- **Tenant Program Management Panel**: Buttons: **Add Program**, **Close**
- **Add / Edit Program Sub-Card**: Fields: *Program Code*, *Program Name*, *Start Date*, *Target Completion Date*, *Status*, *Brief summary of program scope and governance deliverables...* | Buttons: `SaveTenantProgramAsync`, **Cancel**
- **Search & Filter Toolbar for Programs**: Fields: *Filter programs by name, code...* | Buttons: **Edit Program** | Table (Code, Name, Status, Start Date, Target Date, Actions, Edit Program)
- **Manage Tenant Tasks & Risks Card**: Buttons: **Close**
- **Quick Add Task**: Fields: *Task Title*, *Description*, *Priority*, *Due Date* | Buttons: **Add Task**, `ToggleTenantTaskStatusAsync` | Table (Task Title, Priority, Status, Due Date, Owner, Actions)
- **Quick Add Risk**: Fields: *Risk Title*, *Description*, *Probability*, *Impact* | Buttons: **Add Risk** | Table (Risk Title, Probability, Impact, Exposure Score, Status, Reported By)
- **Search & Filter Toolbar for Tenants**: Fields: *Filter tenants by name, code, slug, or domain...*
- **Tenants Table**: Fields: `context.IsActive` | Buttons: **Manage Programs**, **Manage Users**, **Tenant Tasks & Risks**, **Edit Dates** | Table (Slug, Name, Internal Domains, External Domains, Status, Start Date, End Date, Actions, Manage Programs, Manage Users, Tenant Tasks & Risks, Edit Dates)

<details>
<summary><b>Change History (Completed Requirements by Date)</b></summary>

<!-- BEGIN CHANGE HISTORY -->
*(No archived changes yet. Completed requirements are automatically moved here with a date/time stamp when `-ArchiveCompleted` is run.)*
<!-- END CHANGE HISTORY -->
</details>

<details>
<summary><b>Agent Code Index (Auto-Generated Line Numbers, Bindings & Service Map - For AI Only)</b></summary>

### A. Technical UI Layout & Binding Tree (`SettingsTenantsTab.razor`)
- **Injected**: `TenantService:ITenantService`, `ProgramService:IProgramService`, `HierarchyAuth:IHierarchyAuthorizationService`, `TenantContext:ITenantContext`, `CurrentUser:ICurrentUser`, `UnsavedChanges:IUnsavedChangesStateService`, `TimeZoneService:IUserTimeZoneService`, `TaskService:IUserTaskService`
- **UI Sections, Inputs (@bind), Buttons (OnClick) & DataGrids**:
  - `[L1-L37]` **Header & Top-Level Controls**
    - **Inner Gates**: `L16: @if (IsRootAdmin)`, `L24: @if (!_canEditTenants && !IsRootAdmin)`, `L31: @if (!string.IsNullOrWhiteSpace(_statusMessage))`
    - **Buttons**: `Btn("Add Tenant" -> StartAddTenant)@L18`
  - `[L38-L47]` **Step 1: Provision New Tenant Form** — Gate: `@if (_showAddModal && !_showConfirmCreate)`
    - **Inputs**: `CskTextInput("Tenant Slug (Unique, URL-...": _newSlug)@L44`, `CskTextInput("Display Name": _newName)@L46`
  - `[L48-L75]` **Internal Email Domains (1-5)**
    - **Inner Gates**: `L65: @if (_newInternalDomains.Count < 5)`
    - **Inputs**: `CskTextInput("e.g. @@microsoft.com": _tempInternalDomain)@L68`
    - **Buttons**: `Btn("+ Add Domain" -> AddInternalDomain)@L69`
  - `[L76-L108]` **External Email Domains (1-5)**
    - **Inner Gates**: `L93: @if (_newExternalDomains.Count < 5)`
    - **Inputs**: `CskTextInput("e.g. @@salesforce.com": _tempExternalDomain)@L96`, `CskDatePicker("Start Date": _newStartDate)@L105`, `CskDatePicker("End Date": _newEndDate)@L106`
    - **Buttons**: `Btn("+ Add Domain" -> AddExternalDomain)@L97`
  - `[L109-L138]` **Initial Mandatory Tenant Administrator**
    - **Inputs**: `CskTextInput("Admin Full Name": _newAdminFullName)@L119`, `CskTextInput("Admin Email": _newAdminEmail)@L122`
    - **Buttons**: `Btn("Review & Confirm" -> PromptConfirmCreate)@L132`, `Btn("Cancel" -> CancelAdd)@L133`
  - `[L139-L169]` **Step 2: Mandatory Permanent Configuration Confirmation Modal** — Gate: `@if (_showConfirmCreate)`
    - **Buttons**: `Btn("Confirm & Provision Tenant" -> CreateTenantAsync)@L160`, `Btn("Back to Edit" -> _showConfirmCreate = false)@L163`
  - `[L170-L190]` **Edit Dates Modal (Slug, Name, and Domains are strictly locked)** — Gate: `@if (_editingTenant is not null)`
    - **Inputs**: `CskDatePicker("Start Date": _editStartDate)@L180`, `CskDatePicker("End Date": _editEndDate)@L181`
    - **Buttons**: `Btn("Save Dates" -> SaveDatesAsync)@L184`, `Btn("Cancel" -> CancelEditDates)@L185`
  - `[L191-L213]` **Tenant User Management Panel** — Gate: `@if (_managingUsersTenant is not null)`
    - **Buttons**: `Btn("Add User" -> OpenAddUserForm)@L205`, `Btn("Close" -> CloseUserManagement)@L208`
  - `[L214-L238]` **Automated Background Processing Kill Switch Card**
    - **Inner Gates**: `L223: @if (IsRootAdmin || _canEditTenants)`
    - **Inputs**: `CskSwitch("@(_isDaemonActive ? ": _isDaemonActive -> ToggleDaemonStatusAsync)@L225`
  - `[L239-L290]` **Add User Sub-Card** — Gate: `@if (_showAddUserForm)`
    - **Inner Gates**: `L262: @if (!string.IsNullOrWhiteSpace(_userDomainError))`
    - **Inputs**: `CskTextInput("Full Name": _userFullName)@L245`, `CskTextInput("Email Address": _userEmail -> OnUserEmailChanged)@L249`, `CskTextInput(GetUserTypeDisplay())@L258`, `CskDatePicker("Start Date": _userStartDate)@L270`, `CskDatePicker("End Date": _userEndDate)@L271`, `CskSwitch("Active": _userIsActive)@L273`
    - **Buttons**: `Btn("Save User" -> CreateTenantUserAsync)@L278`, `Btn("Cancel" -> _showAddUserForm = false)@L283`
  - `[L291-L344]` **Edit User Sub-Card** — Gate: `@if (_editingUser is not null)`
    - **Inner Gates**: `L316: @if (!string.IsNullOrWhiteSpace(_editUserDomainError))`
    - **Inputs**: `CskTextInput("Full Name": _editUserFullName)@L300`, `CskTextInput("Email Address": _editUserEmail -> OnEditUserEmailChanged)@L304`, `CskTextInput(GetEditUserTypeDisplay())@L312`, `CskDatePicker("Start Date": _editUserStartDate)@L324`, `CskDatePicker("End Date": _editUserEndDate)@L325`, `CskSwitch("@(_editUserIsActive ? ": _editUserIsActive)@L327`
    - **Buttons**: `Btn("Update User" -> SaveEditUserAsync)@L332`, `Btn("Cancel" -> _editingUser = null)@L337`
  - `[L345-L426]` **Search & Filter Toolbar for Users**
    - **Inner Gates**: `L360: @if (_isLoadingUsers)`, `L364: @if (!_tenantUsers.Any())`, `L376: @if (context.UserKind == UserKindEnum.SystemDaemon)`, `L380: @if (context.UserType == UserTypeEnum.CompanyUser)`, `L403: @if (context.UserKind != UserKindEnum.SystemDaemon)`
    - **Inputs**: `CskTextInput("Filter users by name or e...": _userSearchText)@L347`
    - **Buttons**: `Btn("Edit User" -> StartEditUser)@L405`
    - **DataGrids**: `CskDataGrid(Items=FilteredTenantUsers, Cols=[Name, Email, Type, Start Date, End Date, Status, Actions, Edit User])@L372`
  - `[L427-L450]` **Tenant Program Management Panel** — Gate: `@if (_managingProgramsTenant is not null)`
    - **Inner Gates**: `L439: @if (IsRootAdmin || _canEditTenants)`
    - **Buttons**: `Btn("Add Program" -> OpenAddProgramForm)@L441`, `Btn("Close" -> CloseProgramManagement)@L445`
  - `[L451-L490]` **Add / Edit Program Sub-Card** — Gate: `@if (_showAddProgramForm)`
    - **Inputs**: `CskTextInput("Program Code": _progCode)@L460`, `CskTextInput("Program Name": _progName)@L461`, `CskDatePicker("Start Date": _progStartDate)@L465`, `CskDatePicker("Target Completion Date": _progEndDate)@L466`, `DataListSelect("Status": _progStatus)@L468`, `CskTextArea("Brief summary of program ...": _progDescription)@L474`
    - **Buttons**: `Btn(SaveTenantProgramAsync)@L478`, `Btn("Cancel" -> CloseProgramForm)@L483`
  - `[L491-L566]` **Search & Filter Toolbar for Programs**
    - **Inner Gates**: `L506: @if (_isLoadingPrograms)`, `L510: @if (!_tenantPrograms.Any())`, `L549: @if (IsRootAdmin || _canEditTenants)`
    - **Inputs**: `CskTextInput("Filter programs by name, ...": _programSearchText)@L493`
    - **Buttons**: `Btn("Edit Program" -> StartEditProgram)@L551`
    - **DataGrids**: `CskDataGrid(Items=FilteredTenantPrograms, Cols=[Code, Name, Status, Start Date, Target Date, Actions, Edit Program])@L518`
  - `[L567-L587]` **Manage Tenant Tasks & Risks Card** — Gate: `@if (_managingTasksTenant is not null)`
    - **Tabs**: `Tab[tasks] @L586`
    - **Buttons**: `Btn("Close" -> CloseTenantTasksManagement)@L580`
  - `[L588-L646]` **Quick Add Task**
    - **Tabs**: `Tab[risks] @L645`
    - **Inner Gates**: `L603: @if (_loadingTenantWorkItems)`, `L607: @if (_tenantTasks.Count == 0)`
    - **Inputs**: `CskTextInput("Task Title": _newTenantTaskTitle)@L591`, `CskTextInput("Description": _newTenantTaskDesc)@L592`, `DataListSelect("Priority": _newTenantTaskPriority)@L594`, `CskDatePicker("Due Date": _newTenantTaskDueDate)@L596`
    - **Buttons**: `Btn("Add Task" -> CreateTenantTaskAsync)@L597`, `Btn(ToggleTenantTaskStatusAsync)@L634`
    - **DataGrids**: `CskDataGrid(Items=_tenantTasks.AsQueryable(), Cols=[Task Title, Priority, Status, Due Date, Owner, Actions])@L615`
  - `[L647-L704]` **Quick Add Risk**
    - **Inner Gates**: `L664: @if (_loadingTenantWorkItems)`, `L668: @if (_tenantRisks.Count == 0)`
    - **Inputs**: `CskTextInput("Risk Title": _newTenantRiskTitle)@L650`, `CskTextInput("Description": _newTenantRiskDesc)@L651`, `DataListSelect("Probability": _newTenantRiskProb)@L653`, `DataListSelect("Impact": _newTenantRiskImpact)@L656`
    - **Buttons**: `Btn("Add Risk" -> CreateTenantRiskAsync)@L658`
    - **DataGrids**: `CskDataGrid(Items=_tenantRisks.AsQueryable(), Cols=[Risk Title, Probability, Impact, Exposure Score, Status, Reported By])@L676`
  - `[L705-L719]` **Search & Filter Toolbar for Tenants**
    - **Inputs**: `CskTextInput("Filter tenants by name, c...": _tenantSearchText)@L707`
  - `[L720-L785]` **Tenants Table** — Gate: `@if (_isLoading)`
    - **Inner Gates**: `L743: @if (IsRootAdmin || _canEditTenants)`, `L771: @if (IsRootAdmin || _canEditTenants)`
    - **Inputs**: `CskSwitch(context.IsActive -> ToggleStatusAsync)@L745`
    - **Buttons**: `Btn("Manage Programs" -> OpenProgramManagement)@L765`, `Btn("Manage Users" -> OpenUserManagement)@L768`, `Btn("Tenant Tasks & Risks" -> OpenTenantTasksManagement)@L773`, `Btn("Edit Dates" -> StartEdit)@L776`
    - **DataGrids**: `CskDataGrid(Items=FilteredTenants, Cols=[Slug, Name, Internal Domains, External Domains, Status, Start Date, End Date, Actions, Manage Programs, Manage Users, Tenant Tasks & Risks, Edit Dates])@L727`

### B. Method Map & End-to-End Call Chains (`BlazorEnterprise.Web/Components/Pages/Settings/SettingsTenantsTab.razor.cs`)
| Method | Lines | Role / Trigger | End-to-End Call Chain ([PreGates] -> Service.Method[Gates](DB/Audit)) |
| :--- | :--- | :--- | :--- |
| `OnInitializedAsync` | `L115-L120` | [INIT] | `[IsRootAdmin,TenantBoundaryGate,TenantAdminGate]` -> `local:LoadTenantsAsync()` -> `TenantService.GetAllTenantsAsync[IsRootAdmin](DB[R]:Tenants)` -> `TenantService.GetTenantByIdentifierAsync[TenantBoundaryGate]` -> `HierarchyAuth.CanEditTenantSettingsAsync[TenantAdminGate]` |
| `LoadTenantsAsync` | `L122-L146` | [HELPER] | `[IsRootAdmin,TenantBoundaryGate]` -> `TenantService.GetAllTenantsAsync[IsRootAdmin](DB[R]:Tenants)` -> `TenantService.GetTenantByIdentifierAsync[TenantBoundaryGate]` |
| `NormalizeDomain` | `L148-L152` | [HELPER] | Local UI State |
| `AddInternalDomain` | `L154-L184` | OnClick@L69 | `local:NormalizeDomain()` |
| `RemoveInternalDomain` | `L186-L189` | [HELPER] | Local UI State |
| `AddExternalDomain` | `L191-L221` | OnClick@L97 | `local:NormalizeDomain()` |
| `RemoveExternalDomain` | `L223-L226` | [HELPER] | Local UI State |
| `PromptConfirmCreate` | `L228-L301` | OnClick@L132 | Local UI State |
| `CreateTenantAsync` | `L303-L330` | OnClick@L160 | `[IsRootAdmin]` -> `local:LoadTenantsAsync()` -> `local:CancelAdd()` -> `TenantService.CreateTenantWithAdminAsync[IsRootAdmin,HmacRowSignature](DB[W]:Users,TenantUsers; Audit:LogUserActivityAsync)` -> `TenantService.GetAllTenantsAsync[IsRootAdmin](DB[R]:Tenants)` -> `TenantService.GetTenantByIdentifierAsync[TenantBoundaryGate]` -> `UnsavedChanges.ClearChange` |
| `StartAddTenant` | `L332-L337` | OnClick@L18 | `UnsavedChanges.RegisterChange` |
| `CancelAdd` | `L339-L354` | OnClick@L133 | `UnsavedChanges.ClearChange` |
| `ToggleStatusAsync` | `L356-L372` | ValueChanged@L746 | `[IsRootAdmin]` -> `TenantService.UpdateTenantStatusAsync[IsRootAdmin](DB[W]:Tenants; Audit:LogSecurityEventAsync)` |
| `StartEdit` | `L374-L382` | OnClick@L776 | `[IsRootAdmin]` -> `UnsavedChanges.RegisterChange` |
| `CancelEditDates` | `L384-L388` | OnClick@L185 | `UnsavedChanges.ClearChange` |
| `SaveDatesAsync` | `L390-L423` | OnClick@L184 | `[IsRootAdmin]` -> `TenantService.UpdateTenantDatesAsync[IsRootAdmin](DB[W]:Tenants; Audit:LogUserActivityAsync)` -> `UnsavedChanges.ClearChange` |
| `OpenUserManagement` | `L426-L443` | OnClick@L768 | `[IsRootAdmin]` -> `local:LoadTenantUsersAsync()` -> `TenantService.GetTenantUsersAsync[TenantBoundaryGate](DB[R]:TenantUsers)` -> `TenantService.IsTenantDaemonActiveAsync` |
| `OpenAddUserForm` | `L445-L449` | OnClick@L205 | Local UI State |
| `CloseUserManagement` | `L451-L457` | OnClick@L208 | Local UI State |
| `LoadTenantUsersAsync` | `L461-L478` | [HELPER] | `TenantService.GetTenantUsersAsync[TenantBoundaryGate](DB[R]:TenantUsers)` -> `TenantService.IsTenantDaemonActiveAsync` |
| `ToggleDaemonStatusAsync` | `L480-L497` | ValueChanged@L226 | `[IsRootAdmin]` -> `TenantService.SetTenantDaemonActiveAsync[IsRootAdmin,HmacRowSignature](DB[W]:TenantUsers; Audit:LogUserActivityAsync)` |
| `OnUserEmailChanged` | `L499-L528` | ValueChanged@L251 | Local UI State |
| `GetUserTypeDisplay` | `L530-L538` | [HELPER] | Local UI State |
| `CreateTenantUserAsync` | `L548-L576` | OnClick@L280 | `[IsRootAdmin]` -> `local:LoadTenantUsersAsync()` -> `TenantService.AddTenantUserAsync[TenantBoundaryGate,HmacRowSignature](DB[W]:TenantUsers,Users; Audit:LogUserActivityAsync)` -> `TenantService.GetTenantUsersAsync[TenantBoundaryGate](DB[R]:TenantUsers)` -> `TenantService.IsTenantDaemonActiveAsync` |
| `StartEditUser` | `L579-L590` | OnClick@L409 | Local UI State |
| `OnEditUserEmailChanged` | `L592-L621` | ValueChanged@L306 | Local UI State |
| `GetEditUserTypeDisplay` | `L623-L631` | [HELPER] | Local UI State |
| `SaveEditUserAsync` | `L642-L667` | OnClick@L334 | `[IsRootAdmin]` -> `local:LoadTenantUsersAsync()` -> `TenantService.UpdateTenantUserAsync[TenantBoundaryGate,HmacRowSignature](DB[W]:TenantUsers,Users; Audit:LogUserActivityAsync)` -> `TenantService.GetTenantUsersAsync[TenantBoundaryGate](DB[R]:TenantUsers)` -> `TenantService.IsTenantDaemonActiveAsync` |
| `OpenProgramManagement` | `L707-L715` | OnClick@L765 | `local:LoadTenantProgramsAsync()` -> `ProgramService.GetProgramsAsync(DB[R]:Tenants,Programs)` |
| `CloseProgramManagement` | `L717-L723` | OnClick@L445 | Local UI State |
| `LoadTenantProgramsAsync` | `L725-L738` | [HELPER] | `ProgramService.GetProgramsAsync(DB[R]:Tenants,Programs)` |
| `OpenAddProgramForm` | `L740-L750` | OnClick@L441 | Local UI State |
| `StartEditProgram` | `L752-L762` | OnClick@L555 | Local UI State |
| `CloseProgramForm` | `L764-L768` | OnClick@L483 | Local UI State |
| `SaveTenantProgramAsync` | `L770-L823` | OnClick@L480 | `local:LoadTenantProgramsAsync()` -> `local:CloseProgramForm()` -> `ProgramService.CreateProgramAsync[TenantAdminGate](DB[W]:Tenants,Programs; Audit:LogUserActivityAsync)` -> `ProgramService.UpdateProgramAsync[ProgramAccessGate](DB[W]:Programs; Audit:LogUserActivityAsync)` -> `ProgramService.GetProgramsAsync(DB[R]:Tenants,Programs)` |
| `OpenTenantTasksManagement` | `L846-L852` | OnClick@L773 | `local:LoadTenantWorkItemsAsync()` -> `TaskService.GetTenantTasksAsync(DB[R]:Tasks)` -> `TaskService.GetTenantRisksAsync(DB[R]:Risks)` |
| `CloseTenantTasksManagement` | `L854-L859` | OnClick@L580 | Local UI State |
| `LoadTenantWorkItemsAsync` | `L861-L879` | [HELPER] | `TaskService.GetTenantTasksAsync(DB[R]:Tasks)` -> `TaskService.GetTenantRisksAsync(DB[R]:Risks)` |
| `CreateTenantTaskAsync` | `L881-L917` | OnClick@L597 | `local:LoadTenantWorkItemsAsync()` -> `TaskService.CreateTaskAsync(DB[W]:Tasks)` -> `TaskService.GetTenantTasksAsync(DB[R]:Tasks)` -> `TaskService.GetTenantRisksAsync(DB[R]:Risks)` |
| `CreateTenantRiskAsync` | `L919-L957` | OnClick@L658 | `local:LoadTenantWorkItemsAsync()` -> `TaskService.CreateRiskAsync(DB[W]:Risks)` -> `TaskService.GetTenantTasksAsync(DB[R]:Tasks)` -> `TaskService.GetTenantRisksAsync(DB[R]:Risks)` |
| `ToggleTenantTaskStatusAsync` | `L959-L969` | OnClick@L636 | `TaskService.UpdateTaskAsync(DB[W]:Tasks)` |
| `Dispose` | `L971-L974` | [HELPER] | `UnsavedChanges.ClearChange` |

### C. Downstream Service Endpoints & Validation Rules
| Service Method | Source Location | Security Gates | DB & Audit | Result.Failure Conditions |
| :--- | :--- | :--- | :--- | :--- |
| `TenantService.GetAllTenantsAsync` | `BlazorEnterprise.Persistence/Services/TenantService.cs#L94-L116` | `IsRootAdmin` | `DB[R]: Tenants` | - |
| `TenantService.GetTenantByIdentifierAsync` | `BlazorEnterprise.Persistence/Services/TenantService.cs#L118-L131` | `TenantBoundaryGate` | In-Memory | - |
| `HierarchyAuthorizationService.CanEditTenantSettingsAsync` | `BlazorEnterprise.Persistence/Services/HierarchyAuthorizationService.cs#L426-L434` | `TenantAdminGate` | In-Memory | - |
| `TenantService.CreateTenantWithAdminAsync` | `BlazorEnterprise.Persistence/Services/TenantService.cs#L261-L365` | `IsRootAdmin, HmacRowSignature` | `DB[W]: Users,TenantUsers` `Audit: LogUserActivityAsync` | `"Access Denied: Only designated root super-administrators can provision new t..."`; `"Administrator Full Name is required."`; `"Administrator Email is required."`; `"Reserved background system daemon identities cannot be provisioned as user a..."`; `$"Administrator email '{normalizedAdminEmail}' does not match any configured ..."` |
| `UnsavedChangesStateService.ClearChange` | `BlazorEnterprise.Persistence/Services/UnsavedChangesStateService.cs#L26-L34` | - | In-Memory | - |
| `UnsavedChangesStateService.RegisterChange` | `BlazorEnterprise.Persistence/Services/UnsavedChangesStateService.cs#L18-L24` | - | In-Memory | - |
| `TenantService.UpdateTenantStatusAsync` | `BlazorEnterprise.Persistence/Services/TenantService.cs#L680-L715` | `IsRootAdmin` | `DB[W]: Tenants` `Audit: LogSecurityEventAsync` | `"Access Denied: Only designated root super-administrators can modify tenants."`; `"Tenant not found."`; `"The default system root anchor tenant cannot be deactivated or modified."` |
| `TenantService.UpdateTenantDatesAsync` | `BlazorEnterprise.Persistence/Services/TenantService.cs#L717-L749` | `IsRootAdmin` | `DB[W]: Tenants` `Audit: LogUserActivityAsync` | `"Access Denied: Only designated root super-administrators can modify tenants."`; `"End date cannot precede start date."`; `"Tenant not found."`; `"The default system root anchor tenant cannot be modified."` |
| `TenantService.GetTenantUsersAsync` | `BlazorEnterprise.Persistence/Services/TenantService.cs#L379-L399` | `TenantBoundaryGate` | `DB[R]: TenantUsers` | - |
| `TenantService.IsTenantDaemonActiveAsync` | `BlazorEnterprise.Persistence/Services/TenantService.cs#L988-L996` | - | In-Memory | `"Tenant daemon user not found."` |
| `TenantService.SetTenantDaemonActiveAsync` | `BlazorEnterprise.Persistence/Services/TenantService.cs#L998-L1048` | `IsRootAdmin, HmacRowSignature` | `DB[W]: TenantUsers` `Audit: LogUserActivityAsync` | `"Access Denied: Only designated root super-administrators can modify root dae..."`; `"Access Denied: Tenant administrator privileges required."`; `"Tenant daemon user not found."` |
| `TenantService.AddTenantUserAsync` | `BlazorEnterprise.Persistence/Services/TenantService.cs#L401-L513` | `TenantBoundaryGate, HmacRowSignature` | `DB[W]: TenantUsers,Users` `Audit: LogUserActivityAsync` | `$"Tenant '{tenantSlug}' was not found."`; `"Access Denied: You can only manage users within your active tenant."`; `"Full Name is required."`; `"Email address is required."`; `"End date cannot precede start date."`; `"Reserved background system daemon identities cannot be provisioned as user a..."`; `"The default system root tenant cannot exceed a maximum of 3 administrator ac..."`; `$"Email '{normalizedEmail}' does not match any configured Internal domain ({s..."`; `$"A user with email '{normalizedEmail}' already exists."` |
| `TenantService.UpdateTenantUserAsync` | `BlazorEnterprise.Persistence/Services/TenantService.cs#L515-L626` | `TenantBoundaryGate, HmacRowSignature` | `DB[W]: TenantUsers,Users` `Audit: LogUserActivityAsync` | `$"Tenant '{tenantSlug}' was not found."`; `"Access Denied: You can only manage users within your active tenant."`; `"Full Name is required."`; `"Email address is required."`; `"End date cannot precede start date."`; `"Reserved background system daemon identities cannot be provisioned as user a..."`; `$"User was not found in tenant '{tenantSlug}'."`; `"System daemon accounts cannot be modified through the tenant user directory."`; `$"Email '{normalizedEmail}' does not match any configured Internal domain ({s..."`; `$"A user with email '{normalizedEmail}' already exists."` |
| `ProgramService.GetProgramsAsync` | `BlazorEnterprise.Persistence/Services/ProgramService.cs#L41-L70` | - | `DB[R]: Tenants,Programs` | - |
| `ProgramService.CreateProgramAsync` | `BlazorEnterprise.Persistence/Services/ProgramService.cs#L94-L158` | `TenantAdminGate` | `DB[W]: Tenants,Programs` `Audit: LogUserActivityAsync` | `"Tenant slug is required."`; `"Program name is required."`; `"Program code is required."`; `"Access Denied: Only Tenant Admins can create new programs."`; `$"Tenant '{tenantSlug}' was not found."`; `$"A program with code '{code}' already exists in this tenant."` |
| `ProgramService.UpdateProgramAsync` | `BlazorEnterprise.Persistence/Services/ProgramService.cs#L160-L218` | `ProgramAccessGate` | `DB[W]: Programs` `Audit: LogUserActivityAsync` | `"Invalid Program ID."`; `"Program name is required."`; `"Program code is required."`; `"Access Denied: You do not have administrative permissions to edit this progr..."`; `"Program not found."`; `$"A program with code '{code}' already exists in this tenant."` |
| `UserTaskService.GetTenantTasksAsync` | `BlazorEnterprise.Persistence/Services/UserTaskService.cs#L994-L1001` | - | `DB[R]: Tasks` | - |
| `UserTaskService.GetTenantRisksAsync` | `BlazorEnterprise.Persistence/Services/UserTaskService.cs#L1003-L1010` | - | `DB[R]: Risks` | - |
| `UserTaskService.CreateTaskAsync` | `BlazorEnterprise.Persistence/Services/UserTaskService.cs#L159-L195` | - | `DB[W]: Tasks` | `"Authenticated user email is required to create a task."`; `$"Failed to create task: {ex.Message}"` |
| `UserTaskService.CreateRiskAsync` | `BlazorEnterprise.Persistence/Services/UserTaskService.cs#L1012-L1025` | - | `DB[W]: Risks` | `ex.Message` |
| `UserTaskService.UpdateTaskAsync` | `BlazorEnterprise.Persistence/Services/UserTaskService.cs#L197-L241` | - | `DB[W]: Tasks` | `"Task not found in the specified project."`; `"Cross-project task reassignment is forbidden. Tasks cannot be moved between ..."`; `$"Failed to update task: {ex.Message}"` |

</details>

