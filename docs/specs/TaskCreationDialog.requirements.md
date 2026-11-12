<!-- ===========================================================================
[SYSTEM / AGENT RUNTIME - STRICT MINIMAL-TOKEN READING & EXECUTION PROTOCOL]
thinking_strategy: zero_waste_discovery_focused_planning
thinking_level_hint: low (0% wasted on file/line search; 100% focused on planning UI/logic changes)
agent_mode: targeted_surgical_edit
token_budget_policy: minimal_context_reads
navigation_index: docs/specs/index/TaskCreationDialog.index.md
change_history: docs/specs/history/TaskCreationDialog.history.md
source_file: src/BlazorEnterprise.Web/Components/Dialogs/TaskCreationDialog.razor
code_behind: src/BlazorEnterprise.Web/Components/Dialogs/TaskCreationDialog.razor.cs

MANDATORY AGENT RULES (READ BEFORE TAKING ANY ACTION):
  RULE 0 [PRE-FLIGHT FRESHNESS & LINE-NUMBER SYNC]: Before reading source slices or planning, verify the navigation index line numbers are in sync with live code by running: pwsh -NoProfile -File scripts/Generate-Specs.ps1 -TargetFilter 'TaskCreationDialog' -EnsureFresh (exits in <50ms if unchanged, or refreshes docs/specs/index/TaskCreationDialog.index.md while preserving Section 1 Pending Changes if external edits occurred).
  RULE 1 [READ THE NAVIGATION INDEX]: Read 'docs/specs/index/TaskCreationDialog.index.md' to get the UI Map [Lstart-Lend], Method Map, Service Map, and Validation Rules. Completely IGNORE 'docs/specs/history/TaskCreationDialog.history.md' and 'docs/specs/validation/' unless explicitly asked.
  RULE 2 [SURGICAL LINE-SLICE READS ONLY - NEVER READ FULL SOURCE FILES]: Do NOT call read_file on the entire .razor, .razor.cs, or service .cs files, and do NOT run broad codebase searches. Look up the exact [Lstart-Lend] range in 'docs/specs/index/TaskCreationDialog.index.md' and call read_file ONLY with explicit StartLine and EndLine (+/- 10 lines of context) for the specific section or method being modified.
  RULE 3 [REUSE-FIRST, NO-NEW-ARCHITECTURE & CHANGE BOUNDARY]: Reuse existing components (Csk*, DataListSelect, EntityHistoryButton), dialogs/modals, DTOs, validators, and services first. Do NOT introduce new services, repositories, or abstractions unless required. Modify ONLY referenced files and methods — do NOT refactor unrelated code, rename symbols, or perform opportunistic cleanup.
  RULE 4 [CONDITIONAL PLANNING & COMPLEXITY CLASSIFICATION]:
    - Small / Medium Change (<= 3 files modified, UI/validation updates, existing method/service updates): Implement immediately without waiting for plan approval.
    - Large Change (> 3 files modified, DB schema changes, security model changes, new architecture, or new services/repositories): Output a concise 3-6 bullet 'Execution Plan' first and STOP for user approval ('yes' / Proceed).
  RULE 5 [SURGICAL EDIT, AUTO-ARCHIVE & OUTPUT CONTRACT]: After applying edit_file calls, immediately run: pwsh -NoProfile -File scripts/Generate-Specs.ps1 -TargetFilter 'TaskCreationDialog' -ArchiveCompleted (moves completed items to docs/specs/history/TaskCreationDialog.history.md with date/time and refreshes line numbers). NEVER run: ["dotnet build", "dotnet test"]. Return the standardized Output Contract: (1) Files Modified, (2) Change Summary, (3) Validation Performed, (4) Risks.
============================================================================ -->
# Spec: `TaskCreationDialog`

## 1. Pending Changes (Write in Plain English)
<!-- Write plain English below. For complex items, optionallyprefix with [UI], [DATA], [SECURITY], [VALIDATION], [BUGFIX], or [PERFORMANCE] and add 'Acceptance Criteria:' bullets. Then prompt: Implement @[docs/specs/TaskCreationDialog.requirements.md] -->
<!-- BEGIN PENDING CHANGES -->
- [UI] Create a reusable Task Creation Dialog component for creating different task types.
  Acceptance Criteria:
    - Dialog width set to 85% of screen width
    - Responsive height (scrollable if content exceeds viewport)
    - Title: "Create New Task" or "Create Task"
    - Always visible BaseTaskEntity fields in dialog form:
      - Title (text input, required, max 255 characters)
      - Description (rich text editor, optional)
      - Priority (dropdown: Low, Medium, High, Critical, required)
      - Status (dropdown: Backlog, Planned, In Progress, Blocked, Completed, default=Backlog)
      - Due Date (date picker, optional, must be within project date range if applicable)
      - Assigned To (user dropdown, optional, must be project team member)
      - Estimated Hours (numeric input, optional, minimum 0)
      - Attachments (file upload with drag-drop support, optional)
    - Create/Cancel buttons at bottom right
    - Validation error messages shown inline above fields
    - Required field indicators (asterisk *)
    - Clean, organized layout with logical field grouping

- [UI] Create dynamic Task Type selector with dropdown in New Task button.
  Acceptance Criteria:
    - "New Task" button with integrated dropdown arrow
    - Dropdown options: Sprint Task, Misc Task, Risk, Assumption, Issue
    - Default selection: Sprint Task (or none - user must select)
    - Selecting type from dropdown opens TaskCreationDialog
    - Selected type is passed to dialog and cannot be changed in dialog
    - Show selected type as label/badge in dialog header
    - Dropdown closes after selection

- [UI] Sprint Task type - additional fields displayed in dialog.
  Acceptance Criteria:
    - Sprint (dropdown, required for Sprint Task, shows all open sprints)
    - Story Point Estimate (numeric input, optional)
    - Acceptance Criteria (rich text editor, optional)
    - Sub-task checkbox (if parent task selection enabled)
    - Test Case Reference (text input or link, optional)
    - Definition of Done checklist (optional)

- [UI] Misc Task type - additional fields displayed in dialog.
  Acceptance Criteria:
    - Category (dropdown: Admin, Documentation, Review, Cleanup, Other, optional)
    - Related Link (URL input, optional)
    - Task Type detail (text input, optional)
    - Recurrence (dropdown: None, Daily, Weekly, Monthly, optional)
    - Recurrence End Date (date picker, optional, shown if Recurrence != None)

- [UI] Risk type - additional fields displayed in dialog.
  Acceptance Criteria:
    - Risk Category (dropdown: Technical, Schedule, Resource, Budget, External, Other, required)
    - Probability (dropdown: Low, Medium, High, Very High, required)
    - Impact (dropdown: Low, Medium, High, Critical, required)
    - Mitigation Strategy (rich text editor, required)
    - Risk Owner (user dropdown, required)
    - Contingency Plan (rich text editor, optional)
    - Exposure Level (auto-calculated: Probability × Impact, display only)

- [UI] Assumption type - additional fields displayed in dialog.
  Acceptance Criteria:
    - Assumption Category (dropdown: Technical, Business, Resource, Schedule, Vendor, Other, required)
    - Validation Criteria (rich text editor, required - how will this assumption be validated)
    - Validation Date (date picker, optional - when will assumption be validated)
    - Assumption Owner (user dropdown, required)
    - Dependency on Other Assumptions (multi-select, optional)
    - Validation Status (dropdown: Unvalidated, In Review, Validated, Invalid, optional)

- [UI] Issue type - additional fields displayed in dialog.
  Acceptance Criteria:
    - Issue Category (dropdown: Technical, Process, Quality, Communication, Vendor, Other, required)
    - Root Cause (rich text editor, required)
    - Impact Assessment (rich text editor, required)
    - Issue Owner (user dropdown, required)
    - Resolution Steps (rich text editor, optional - step-by-step resolution plan)
    - Related Issues (multi-select dropdown, optional - link to other issues)
    - Severity Level (dropdown: Low, Medium, High, Critical, required)
    - Target Resolution Date (date picker, optional)

- [VALIDATION] Implement field validation rules for all task types.
  Acceptance Criteria:
    - BaseTaskEntity fields: Title required, max 255 chars; Due Date within project dates; Estimated Hours >= 0
    - Sprint Task: Sprint required, Sprint must not be Completed status
    - Risk: All required fields must be filled; Probability and Impact must be valid; Mitigation Strategy min 10 characters
    - Assumption: All required fields must be filled; Validation Criteria min 10 characters
    - Issue: All required fields must be filled; Root Cause and Impact min 10 characters each
    - Cross-field validation: Assigned To must be active project member
    - Show validation messages below each field in real-time as user types/changes fields
    - Disable Create button if any required field is empty or invalid

- [UI] Create Task Type Selection Button Component for reuse across pages.
  Acceptance Criteria:
    - Standalone "New Task" button with dropdown
    - Can be placed in page headers or toolbars
    - Accepts callback parameter for task type selection
    - Accepts optional filter parameter to show only specific task types
    - Button text customizable (e.g., "New Task", "Add Task", "Create Item")
    - Icon customizable (default: plus icon)
    - Shows loading state while dialog opens
    - Returns selected task type to parent component

- [SERVICE] Create TaskCreationService for handling task creation across all types.
  Acceptance Criteria:
    - CreateTaskAsync(baseTaskData, taskTypeData) - create task with type-specific data
    - ValidateTaskDataAsync(taskType, formData) - validate all fields for specific type
    - GetTaskTypeFieldsAsync(taskType) - return list of required/optional fields for type
    - GetRelatedDataAsync(projectId, taskType) - get dropdown options (sprints, users, categories) based on type
    - PopulateDefaultValuesAsync(taskType, projectId) - set default values based on task type and project
    - CalculateTaskMetricsAsync(taskType, data) - auto-calculate fields (e.g., Risk Exposure)
    - GetTaskTypeDisplayNameAsync(taskType) - return localized display name

- [UI] Dialog State Management and UX enhancements.
  Acceptance Criteria:
    - Preserve form data if user navigates away and comes back
    - "Unsaved Changes" warning if user tries to close with unsaved changes
    - Success message after task creation with option to create another or close
    - Link to created task in success message
    - Auto-fill Assigned To with current user (optional preference)
    - Auto-populate Project/Sprint context from current page context
    - Keyboard shortcuts: ESC to cancel, CTRL+ENTER to create
    - Loading state while creating task (disable buttons, show spinner)
    - Error handling with user-friendly error messages
<!-- END PENDING CHANGES -->

## 2. Permanent & Implementation Rules
<!-- BEGIN CUSTOM BUSINESS RULES -->
- *(Permanent domain invariants preserved across regenerations)*
- **Task Creation** is a unified dialog-based system supporting multiple task subtypes through single entry point
- **BaseTaskEntity** contains common fields for all task types (Title, Description, Priority, Status, Due Date, Assigned To, Estimated Hours, Attachments)
- **Type-Specific Fields** are shown/hidden dynamically based on selected task type without cluttering UI
- **Validation** applies both base rules and type-specific rules; user cannot create invalid task
- **Reusability** - TaskCreationDialog and TaskTypeSelectionButton are reusable across all project pages
- **Context Awareness** - Dialog inherits project/sprint context from parent page when possible
- **Auto-Calculations** - Risk Exposure calculated from Probability × Impact; other derived fields auto-populated
- **Permissions** - User must be project member to create tasks; certain fields restricted by role (e.g., Risk Owner must be admin or manager)
<!-- END CUSTOM BUSINESS RULES -->
- **Reuse-First**: Reuse existing components (MudBlazor dialogs, rich text editor, date pickers, dropdowns), validators, and services. Reuse TaskCreationService across pages.
- **No New Architecture**: Extend existing service patterns; TaskCreationService follows existing factory/builder patterns for entity creation.
- **Change Boundary**: TaskCreationDialog and related service are cross-cutting concerns used by Sprint Planning, Clarification Tracker, Risk/Issue tracking; modifications should not affect those specific pages.
- **Conditional Planning**: This is a Medium Change (reusable component + new service, but no new DB entities - task types already exist in system); implement with plan review.

## 3. Business Context
- **Purpose**: Provide unified task creation interface for all task subtypes (Sprint Task, Misc Task, Risk, Assumption, Issue) with type-specific fields and validation
- **Reusability**: Used by multiple pages:
  - Sprint Planning (Sprint Task creation)
  - Clarification Tracker (Task linking)
  - Risk Management (Risk creation)
  - Assumption Tracking (Assumption creation)
  - Issue Tracking (Issue creation)
- **Scope**: Project-level feature (all tasks belong to active project)
- **Access**: All project members can create tasks; certain operations restricted by role
- **Related Components**: 
  - BaseTaskEntity and all child entity types (SprintTask, MiscTask, Risk, Assumption, Issue)
  - Existing task management pages using this dialog
- **Integration Points**:
  - ProjectContext (active project filtering)
  - ICurrentUser (assigned to, created by, permissions)
  - Sprint Management (Sprint Task sprint selection)
  - RAID Log system (Risk, Assumption, Issue types)
  - Notification system (notify assigned user)
- **Database Requirements**: 
  - No new entities (existing BaseTaskEntity and child types)
  - New service: TaskCreationService
  - Indexes already present on task types
- **Component Hierarchy**:
  - TaskTypeSelectionButton (can be placed anywhere, opens dialog)
  - TaskCreationDialog (modal overlay, 85% width)
  - Dynamic field sections per type (shown/hidden)
  - TaskCreationService (backend logic)
- **Field Visibility Logic**:
  - Always visible: BaseTaskEntity fields
  - Conditionally visible: Type-specific fields based on selected task type
  - No field shown/hidden based on role (validation enforced at save time)
