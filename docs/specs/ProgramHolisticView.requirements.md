<!-- ===========================================================================
[SYSTEM / AGENT RUNTIME - STRICT MINIMAL-TOKEN READING & EXECUTION PROTOCOL]
thinking_strategy: zero_waste_discovery_focused_planning
thinking_level_hint: low (0% wasted on file/line search; 100% focused on planning UI/logic changes)
agent_mode: targeted_surgical_edit
token_budget_policy: minimal_context_reads
navigation_index: docs/specs/index/ProgramHolisticView.index.md
change_history: docs/specs/history/ProgramHolisticView.history.md
source_file: src/BlazorEnterprise.Web/Components/Pages/ProgramHolisticView.razor
code_behind: src/BlazorEnterprise.Web/Components/Pages/ProgramHolisticView.razor.cs

MANDATORY AGENT RULES (READ BEFORE TAKING ANY ACTION):
  RULE 0 [PRE-FLIGHT FRESHNESS & LINE-NUMBER SYNC]: Before reading source slices or planning, verify the navigation index line numbers are in sync with live code by running: pwsh -NoProfile -File scripts/Generate-Specs.ps1 -TargetFilter 'ProgramHolisticView' -EnsureFresh (exits in <50ms if unchanged, or refreshes docs/specs/index/ProgramHolisticView.index.md while preserving Section 1 Pending Changes if external edits occurred).
  RULE 1 [READ THE NAVIGATION INDEX]: Read 'docs/specs/index/ProgramHolisticView.index.md' to get the UI Map [Lstart-Lend], Method Map, Service Map, and Validation Rules. Completely IGNORE 'docs/specs/history/ProgramHolisticView.history.md' and 'docs/specs/validation/' unless explicitly asked.
  RULE 2 [SURGICAL LINE-SLICE READS ONLY - NEVER READ FULL SOURCE FILES]: Do NOT call read_file on the entire .razor, .razor.cs, or service .cs files, and do NOT run broad codebase searches. Look up the exact [Lstart-Lend] range in 'docs/specs/index/ProgramHolisticView.index.md' and call read_file ONLY with explicit StartLine and EndLine (+/- 10 lines of context) for the specific section or method being modified.
  RULE 3 [REUSE-FIRST, NO-NEW-ARCHITECTURE & CHANGE BOUNDARY]: Reuse existing components (Csk*, DataListSelect, EntityHistoryButton), dialogs/modals, DTOs, validators, and services first. Do NOT introduce new services, repositories, or abstractions unless required. Modify ONLY referenced files and methods — do NOT refactor unrelated code, rename symbols, or perform opportunistic cleanup.
  RULE 4 [CONDITIONAL PLANNING & COMPLEXITY CLASSIFICATION]:
    - Small / Medium Change (<= 3 files modified, UI/validation updates, existing method/service updates): Implement immediately without waiting for plan approval.
    - Large Change (> 3 files modified, DB schema changes, security model changes, new architecture, or new services/repositories): Output a concise 3-6 bullet 'Execution Plan' first and STOP for user approval ('yes' / Proceed).
  RULE 5 [SURGICAL EDIT, AUTO-ARCHIVE & OUTPUT CONTRACT]: After applying edit_file calls, immediately run: pwsh -NoProfile -File scripts/Generate-Specs.ps1 -TargetFilter 'ProgramHolisticView' -ArchiveCompleted (moves completed items to docs/specs/history/ProgramHolisticView.history.md with date/time and refreshes line numbers). NEVER run: ["dotnet build", "dotnet test"]. Return the standardized Output Contract: (1) Files Modified, (2) Change Summary, (3) Validation Performed, (4) Risks.
============================================================================ -->
# Spec: `ProgramHolisticView`

## 1. Pending Changes (Write in Plain English)
<!-- Write plain English below. For complex items, optionally prefix with [UI], [DATA], [SECURITY], [VALIDATION], [BUGFIX], or [PERFORMANCE] and add 'Acceptance Criteria:' bullets. Then prompt: Implement @[docs/specs/ProgramHolisticView.requirements.md] -->
<!-- BEGIN PENDING CHANGES -->
- [UI] Create a new "Program Holistic View" page that displays a comprehensive, multi-tab view of a selected program.
  Acceptance Criteria:
    - Accessible from left navigation after Home
    - Uses MudBlazor Tab component (consistent with existing SettingsPage pattern)
    - Contains 4 tabs: Home (Executive Summary), Timeline, Milestone, Raid Log
    - Create a reusable TabContainerComp component in the Shared project for consistent tab styling/behavior
    - The "Home" tab displays an executive summary dashboard with key program metrics and KPIs
    - Timeline tab shows program events/milestones in chronological order
    - Milestone tab displays all program milestones with status indicators
    - Raid Log tab displays program risks, assumptions, issues, and decisions
    
- [UI] Create a reusable TabContainerComp component in src/BlazorEnterprise.Web/Components/Shared/ with:
  Acceptance Criteria:
    - Accepts tab list as parameter (name, icon, content)
    - Uses MudTabs for rendering
    - Lazy-loads tab content on selection for performance
    - Maintains consistent styling with MudBlazor theme
    - Supports optional badge/notification indicators on tabs
    
- [UI] Update NavMenuComp to include navigation link to Program Holistic View after Home.
  Acceptance Criteria:
    - Link visible only when user has active tenant and program session
    - Uses appropriate icon (e.g., chart/dashboard icon)
    - Responsive to collapsed/expanded navigation state
<!-- END PENDING CHANGES -->

## 2. Permanent & Implementation Rules
<!-- BEGIN CUSTOM BUSINESS RULES -->
- *(Permanent domain invariants preserved across regenerations)*
- **Program Holistic View** is a dashboard-style page providing executives and program managers with a single unified view of program health, timeline, milestones, and risk/issue tracking
- **Tab Component Reusability**: The TabContainerComp should be general-purpose and reusable for other multi-tab views in the application (Settings, Program View, etc.)
- **Performance**: Tabs should be lazy-loaded to avoid loading all tab content simultaneously
- **Consistency**: Follow existing MudBlazor patterns from SettingsPage.razor for tab management and styling
<!-- END CUSTOM BUSINESS RULES -->
- **Reuse-First**: Reuse existing UI components (MudBlazor), modals, DTOs, validators, and services before creating new code. Leverage existing Program and ProjectContext for data access.
- **No New Architecture**: Prefer extending existing methods; do not introduce new services, repositories, or abstractions unless explicitly required by data needs.
- **Change Boundary**: Modify only referenced files and methods. Do not refactor unrelated code, rename symbols, or perform opportunistic cleanup.
- **Conditional Planning**: Implement Small/Medium changes (<= 3 files, UI/validation/existing method updates) immediately; generate a plan and wait for approval only on Large changes (> 3 files, DB schema, security model, or new services/architecture).

## 3. Business Context
- **Purpose**: New page providing a holistic, executive-level view of program status, timeline, milestones, and RAID log for program managers and stakeholders
- **Parent Layout**: MainLayout.razor (HasActiveTenantSession gate)
- **Navigation**: Will be added to NavMenuComp after Home link
- **Related Components**: SettingsPage.requirements.md (similar multi-tab pattern), HomePage.requirements.md (dashboard pattern)
- **Tab Children**:
  - ProgramHolisticViewHomeTab (Executive Summary - KPIs, status indicators)
  - ProgramHolisticViewTimelineTab (Chronological program events)
  - ProgramHolisticViewMilestoneTab (Milestone tracking with status)
  - ProgramHolisticViewRaidLogTab (Risks, Assumptions, Issues, Decisions)
