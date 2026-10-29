<!-- ===========================================================================
[SYSTEM / AGENT RUNTIME - STRICT MINIMAL-TOKEN READING & EXECUTION PROTOCOL]
thinking_strategy: zero_waste_discovery_focused_planning
thinking_level_hint: low (0% wasted on file/line search; 100% focused on planning UI/logic changes)
agent_mode: targeted_surgical_edit
token_budget_policy: minimal_context_reads
source_file: src/BlazorEnterprise.Web/Components/Pages/LoginPage.razor
code_behind: src/BlazorEnterprise.Web/Components/Pages/LoginPage.razor.cs
generated_at_utc: 2026-10-29T15:32:56Z
source_hash_sha256: 1d8e8a35b3137345

MANDATORY AGENT RULES (READ BEFORE TAKING ANY ACTION):
  RULE 0 [PRE-FLIGHT FRESHNESS & LINE-NUMBER SYNC]: Before reading source slices or planning, verify the spec's line numbers are in sync with live code by running: pwsh -NoProfile -File scripts/Generate-Specs.ps1 -TargetFilter 'LoginPage' -EnsureFresh (exits in <50ms if unchanged, or refreshes line numbers while preserving Section 1 Pending Changes if external edits occurred). If it regenerates, re-read the updated Agent Code Index.
  RULE 1 [WHAT TO READ IN THIS MD FILE]: Read ONLY '## 1. What I Want Changed' (and 'Permanent Rules') plus the 'Agent Code Index' (Sections A, B, C at the bottom). Use '## 2. Current Page Overview' only for quick orientation.
  RULE 2 [WHAT TO SKIP IN THIS MD FILE]: Completely IGNORE the 'Change History (Completed Requirements by Date)' block unless the user explicitly asks about historical changes.
  RULE 3 [SURGICAL LINE-SLICE READS ONLY - NEVER READ FULL SOURCE FILES]: Do NOT call read_file on the entire .razor, .razor.cs, or service .cs files, and do NOT run broad codebase searches. Look up the exact [Lstart-Lend] range in the 'Agent Code Index' below and call read_file ONLY with explicit StartLine and EndLine (+/- 10 lines of context) for the specific section or method being modified.
  RULE 4 [PHASE 1 - PLAN FIRST & STOP FOR APPROVAL]: Map each plain-English requirement in Section 1 to the exact target file, line range, @bind property, and C# method from the Agent Code Index. Output a concise 3-6 bullet 'Execution Plan' in chat and STOP. Wait for the user to reply 'yes' / click Proceed before editing any files.
  RULE 5 [PHASE 2 - SURGICAL EDIT, AUTO-ARCHIVE & FORBIDDEN COMMANDS]: Once approved, apply targeted edit_file calls, then immediately run: pwsh -NoProfile -File scripts/Generate-Specs.ps1 -TargetFilter 'LoginPage' -ArchiveCompleted (moves completed items to Change History with date/time and refreshes line numbers). NEVER run: ["dotnet build", "dotnet test"].
============================================================================ -->
# Spec: `LoginPage`

## 1. What I Want Changed (Write in Plain English)
<!-- Write your requirements in plain English below (no line numbers or code syntax needed), then prompt: Implement @[docs/specs/LoginPage.requirements.md] -->
<!-- BEGIN PENDING CHANGES -->
- [ ] *(Add pending change requests here. Then prompt Antigravity: `Implement the pending changes in @[docs/specs/LoginPage.requirements.md]`)*
<!-- END PENDING CHANGES -->

### Permanent Rules (Optional)
<!-- BEGIN CUSTOM BUSINESS RULES -->
- *(Document permanent domain invariants or custom business rules here. Preserved across regenerations.)*
<!-- END CUSTOM BUSINESS RULES -->

## 2. Current Page Overview (Plain-English Summary)
- **Purpose**: Zero-Trust authentication entry point, local dev email login, and multi-tenant session establishment.
- **Page Route**: `/login`
- **Tenant Selection Step (Immediately after verified credentials)**: Buttons: `submit`, **Sign in with a different account**
- **Primary Sign In Button**: Fields: *Email Address (Local Dev Only)* | Buttons: `submit`, `submit`

<details>
<summary><b>Change History (Completed Requirements by Date)</b></summary>

<!-- BEGIN CHANGE HISTORY -->
*(No archived changes yet. Completed requirements are automatically moved here with a date/time stamp when `-ArchiveCompleted` is run.)*
<!-- END CHANGE HISTORY -->
</details>

<details>
<summary><b>Agent Code Index (Auto-Generated Line Numbers, Bindings & Service Map - For AI Only)</b></summary>

### A. Technical UI Layout & Binding Tree (`LoginPage.razor`)
- **Auth**: `AllowAnonymous``, ``Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("login")`
- **Injected**: `AuthVerificationService:IInternalAuthenticationService`, `Configuration:IConfiguration`, `HostEnvironment:IWebHostEnvironment`, `DevLoginOptions:IOptions<DevLoginOptions>`, `TenantContext:ITenantContext`, `CurrentUser:ICurrentUser`, `RootAdminService:IRootAdminService`, `SessionService:IUserSessionService`, `AuditService:IAuditService`, `SessionStorage:ProtectedSessionStorage`, `AuthStateProvider:CurrentUserAuthenticationStateProvider`, `TimeZoneService:IUserTimeZoneService`, `NavigationManager:NavigationManager`, `Logger:ILogger<LoginPage>`
- **UI Sections, Inputs (@bind), Buttons (OnClick) & DataGrids**:
  - `[L1-L23]` **Header & Top-Level Controls**
    - **Inner Gates**: `L22: @if (_isSelectingTenant && _pendingAuth != null)`
  - `[L24-L84]` **Tenant Selection Step (Immediately after verified credentials)**
    - **Inner Gates**: `L61: @if (_isLoading)`
    - **Inputs**: `CskSelect()@L47`
    - **Buttons**: `Btn(submit)@L57`, `Btn("Sign in with a different account" -> submit)@L73`
  - `[L116-L177]` **Primary Sign In Button**
    - **Inner Gates**: `L121: @if (_isLoading)`, `L133: @if (!string.IsNullOrEmpty(_infoMessage))`, `L140: @if (!string.IsNullOrEmpty(_errorMessage))`, `L147: @if (HostEnvironment.IsDevelopment() && DevLoginOptions.Value.Enabled)`, `L160: @if (_isLoading)`
    - **Inputs**: `CskTextInput("Email Address (Local Dev ...": _localLoginEmail)@L152`
    - **Buttons**: `Btn(submit)@L117`, `Btn(submit)@L156`

### B. Method Map & End-to-End Call Chains (`BlazorEnterprise.Web/Components/Pages/LoginPage.razor.cs`)
| Method | Lines | Role / Trigger | End-to-End Call Chain ([PreGates] -> Service.Method[Gates](DB/Audit)) |
| :--- | :--- | :--- | :--- |
| `OnInitialized` | `L49-L71` | [INIT] | `HostEnvironment.IsDevelopment()` |
| `HandleMicrosoftSignIn` | `L73-L85` | [HELPER] | `NavigationManager.NavigateTo()` |
| `HandleLocalEmailSignInAsync` | `L87-L98` | [HELPER] | `local:TestSignInAsync()` -> `AuthVerificationService.VerifyLoginCredentialsAsync()` -> `NavigationManager.NavigateTo()` |
| `TestSignInAsync` | `L100-L132` | [HELPER] | `local:ProcessAuthSuccessAsync()` -> `AuthVerificationService.VerifyLoginCredentialsAsync()` -> `NavigationManager.NavigateTo()` |
| `ProcessAuthSuccessAsync` | `L134-L159` | [HELPER] | `local:CompleteLoginAsync()` -> `TenantContext.Initialize()` -> `SessionService.CreateSessionAsync[TenantBoundaryGate](DB[W]:UserSessions)` -> `AuditService.LogUserActivityAsync` -> `SessionStorage.SetAsync()` -> `AuthStateProvider.NotifyUserChanged()` -> `TimeZoneService.ApplyUserTimeZone` -> `NavigationManager.NavigateTo()` |
| `HandleConfirmTenantSelectionAsync` | `L161-L169` | [HELPER] | `local:CompleteLoginAsync()` -> `TenantContext.Initialize()` -> `SessionService.CreateSessionAsync[TenantBoundaryGate](DB[W]:UserSessions)` -> `AuditService.LogUserActivityAsync` -> `SessionStorage.SetAsync()` -> `AuthStateProvider.NotifyUserChanged()` -> `TimeZoneService.ApplyUserTimeZone` -> `NavigationManager.NavigateTo()` |
| `CancelTenantSelection` | `L171-L177` | [HELPER] | Local UI State |
| `CompleteLoginAsync` | `L179-L254` | [HELPER] | `[IsRootAdmin]` -> `TenantContext.Initialize()` -> `SessionService.CreateSessionAsync[TenantBoundaryGate](DB[W]:UserSessions)` -> `AuditService.LogUserActivityAsync` -> `SessionStorage.SetAsync()` -> `AuthStateProvider.NotifyUserChanged()` -> `TimeZoneService.ApplyUserTimeZone` -> `NavigationManager.NavigateTo()` |

### C. Downstream Service Endpoints & Validation Rules
| Service Method | Source Location | Security Gates | DB & Audit | Result.Failure Conditions |
| :--- | :--- | :--- | :--- | :--- |
| `UserSessionService.CreateSessionAsync` | `BlazorEnterprise.Persistence/Services/UserSessionService.cs#L37-L85` | `TenantBoundaryGate` | `DB[W]: UserSessions` | - |
| `AuditService.LogUserActivityAsync` | `BlazorEnterprise.Persistence/Services/AuditService.cs#L241-L303` | - | In-Memory | - |
| `UserTimeZoneService.ApplyUserTimeZone` | `BlazorEnterprise.Web/Services/UserTimeZoneService.cs#L51-L57` | - | In-Memory | - |

</details>

