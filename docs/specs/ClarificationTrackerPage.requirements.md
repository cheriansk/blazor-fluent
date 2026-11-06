<!-- ===========================================================================
[SYSTEM / AGENT RUNTIME - STRICT MINIMAL-TOKEN READING & EXECUTION PROTOCOL]
thinking_strategy: zero_waste_discovery_focused_planning
thinking_level_hint: low (0% wasted on file/line search; 100% focused on planning UI/logic changes)
agent_mode: targeted_surgical_edit
token_budget_policy: minimal_context_reads
navigation_index: docs/specs/index/ClarificationTrackerPage.index.md
change_history: docs/specs/history/ClarificationTrackerPage.history.md
source_file: src/BlazorEnterprise.Web/Components/Pages/ClarificationTrackerPage.razor
code_behind: src/BlazorEnterprise.Web/Components/Pages/ClarificationTrackerPage.razor.cs

MANDATORY AGENT RULES (READ BEFORE TAKING ANY ACTION):
  RULE 0 [PRE-FLIGHT FRESHNESS & LINE-NUMBER SYNC]: Before reading source slices or planning, verify the navigation index line numbers are in sync with live code by running: pwsh -NoProfile -File scripts/Generate-Specs.ps1 -TargetFilter 'ClarificationTrackerPage' -EnsureFresh (exits in <50ms if unchanged, or refreshes docs/specs/index/ClarificationTrackerPage.index.md while preserving Section 1 Pending Changes if external edits occurred).
  RULE 1 [READ THE NAVIGATION INDEX]: Read 'docs/specs/index/ClarificationTrackerPage.index.md' to get the UI Map [Lstart-Lend], Method Map, Service Map, and Validation Rules. Completely IGNORE 'docs/specs/history/ClarificationTrackerPage.history.md' and 'docs/specs/validation/' unless explicitly asked.
  RULE 2 [SURGICAL LINE-SLICE READS ONLY - NEVER READ FULL SOURCE FILES]: Do NOT call read_file on the entire .razor, .razor.cs, or service .cs files, and do NOT run broad codebase searches. Look up the exact [Lstart-Lend] range in 'docs/specs/index/ClarificationTrackerPage.index.md' and call read_file ONLY with explicit StartLine and EndLine (+/- 10 lines of context) for the specific section or method being modified.
  RULE 3 [REUSE-FIRST, NO-NEW-ARCHITECTURE & CHANGE BOUNDARY]: Reuse existing components (Csk*, DataListSelect, EntityHistoryButton), dialogs/modals, DTOs, validators, and services first. Do NOT introduce new services, repositories, or abstractions unless required. Modify ONLY referenced files and methods — do NOT refactor unrelated code, rename symbols, or perform opportunistic cleanup.
  RULE 4 [CONDITIONAL PLANNING & COMPLEXITY CLASSIFICATION]:
    - Small / Medium Change (<= 3 files modified, UI/validation updates, existing method/service updates): Implement immediately without waiting for plan approval.
    - Large Change (> 3 files modified, DB schema changes, security model changes, new architecture, or new services/repositories): Output a concise 3-6 bullet 'Execution Plan' first and STOP for user approval ('yes' / Proceed).
  RULE 5 [SURGICAL EDIT, AUTO-ARCHIVE & OUTPUT CONTRACT]: After applying edit_file calls, immediately run: pwsh -NoProfile -File scripts/Generate-Specs.ps1 -TargetFilter 'ClarificationTrackerPage' -ArchiveCompleted (moves completed items to docs/specs/history/ClarificationTrackerPage.history.md with date/time and refreshes line numbers). NEVER run: ["dotnet build", "dotnet test"]. Return the standardized Output Contract: (1) Files Modified, (2) Change Summary, (3) Validation Performed, (4) Risks.
============================================================================ -->
# Spec: `ClarificationTrackerPage`

## 1. Pending Changes (Write in Plain English)
<!-- Write plain English below. For complex items, optionally prefix with [UI], [DATA], [SECURITY], [VALIDATION], [BUGFIX], or [PERFORMANCE] and add 'Acceptance Criteria:' bullets. Then prompt: Implement @[docs/specs/ClarificationTrackerPage.requirements.md] -->
<!-- BEGIN PENDING CHANGES -->
- [UI] Create a Clarification Tracker page with a data grid listing all clarifications for the active project.
  Acceptance Criteria:
    - Accessible from left navigation
    - Grid columns: Title, Question (preview), Assigned To, Status
    - Sortable and filterable by Status (Open, In Progress, Pending Response, Resolved, Closed)
    - Show creation date and last updated date
    - Quick filters for My Clarifications, Assigned to Me, Open Items
    - Searchable by Title and Question text
    - Action buttons: Create New, Edit, Delete (with confirmation)
    - Double-click or Edit button opens detail view
    - Status color coding (Open=Red, In Progress=Orange, Pending Response=Yellow, Resolved=Green, Closed=Gray)

- [UI] Create a Clarification Detail view/dialog that displays and allows editing of a single clarification item.
  Acceptance Criteria:
    - Title field (text input, required)
    - Question field (rich text editor allowing formatting, links, images, required)
    - Question read-only display when viewing responses from assignee
    - Status dropdown (Open, In Progress, Pending Response, Resolved, Closed)
    - Created By field (read-only, shows creator name and date)
    - Assigned To field (dropdown to select team member, required when status changes to In Progress)
    - Answers/Responses section below question
    - Add Answer button when viewing own clarification or when assigned
    - Save and Cancel buttons
    - Audit trail showing creation/modification history

- [UI] Create an Answers/Responses discussion format component within the Detail view.
  Acceptance Criteria:
    - List all answers/responses in chronological order (oldest first)
    - Each answer displays: Responder name, timestamp, answer text (rich text formatted)
    - Allow nested discussion/comments under each answer (threaded conversation)
    - Comment indentation showing reply hierarchy
    - Each comment shows: Author, timestamp, comment text, Edit/Delete buttons (if owner)
    - "Reply to this answer" button under each answer to add comment
    - Rich text editor for answers and comments
    - Show unread indicator if answer contains updates since last view
    - Ability to mark answer as "Solution" or primary response

- [UI] Create an Assignment Workflow for clarification reassignment.
  Acceptance Criteria:
    - Requestor can assign clarification to another person
    - Assignment changes status to In Progress
    - Assignee receives notification of new assignment
    - Assignee can provide answer/response
    - Assignee can reassign to another person (with reason/note)
    - Status transitions: Open -> In Progress (on assignment) -> Pending Response (awaiting requestor review) -> Resolved -> Closed
    - Audit log shows all reassignments and status changes
    - Only clarification creator can mark as Resolved/Closed
    - Assignee must provide answer before status can change to Pending Response

- [DATA] Create supporting data models/DTOs for Clarifications.
  Acceptance Criteria:
    - Clarification entity with: Title, Question (rich text), Status, Created By (User), Created Date, Assigned To (User), Last Modified Date, Last Modified By (User)
    - Answer entity with: Clarification (foreign key), Answer Text (rich text), Responder (User), Created Date, Is Solution Flag
    - AnswerComment entity with: Answer (foreign key), Comment Text, Author (User), Created Date, Parent Comment (foreign key for nesting)
    - ClarificationAuditLog entity with: Clarification (foreign key), Change Type (Created, Assigned, Status Changed, Answer Added), Changed By (User), Changed Date, Old Value, New Value
    - Status enumeration with: Open, In Progress, Pending Response, Resolved, Closed

- [DATA] Create ClarificationTrackerService for managing clarification operations.
  Acceptance Criteria:
    - GetClarificationsAsync(projectId, filters) - retrieve clarifications with optional filtering
    - CreateClarificationAsync(clarification) - create new clarification
    - UpdateClarificationAsync(clarification) - update clarification details
    - DeleteClarificationAsync(clarificationId) - delete clarification with confirmation
    - AssignClarificationAsync(clarificationId, assigneeId, reassignReason?) - assign to user
    - AddAnswerAsync(clarificationId, answerText) - add new answer
    - AddCommentAsync(answerId, commentText, parentCommentId?) - add comment to answer
    - UpdateStatusAsync(clarificationId, newStatus) - change status with validation
    - GetAuditTrailAsync(clarificationId) - retrieve change history
    - MarkAnswerAsSolutionAsync(answerId) - mark answer as primary response
    - IsUserAllowedToModifyAsync(clarificationId, userId) - validate permissions

- [UI] Update NavMenuComp to include navigation link to Clarification Tracker.
  Acceptance Criteria:
    - Link visible after Project Holistic View
    - Only visible when user has active tenant and project session
    - Uses appropriate icon (e.g., question mark or discussion icon)
    - Shows badge with count of "Assigned to Me" clarifications
    - Responsive to collapsed/expanded navigation state
<!-- END PENDING CHANGES -->

## 2. Permanent & Implementation Rules
<!-- BEGIN CUSTOM BUSINESS RULES -->
- *(Permanent domain invariants preserved across regenerations)*
- **Clarification Tracker** is a collaborative discussion and tracking system for managing project questions, clarifications, and decisions
- **Discussion Format**: Answers and comments follow a threaded/nested conversation pattern similar to forum discussions or document comments
- **Workflow**: Supports reassignment workflow where clarifications flow between team members until resolved
- **Permissions**: Only clarification creator can close/resolve; assignee can provide answers and reassign; all team members can view project clarifications
- **Audit Trail**: All changes including assignments, status changes, and responses are tracked with user and timestamp
- **Notifications**: Users should be notified when clarification is assigned to them or reassigned back
<!-- END CUSTOM BUSINESS RULES -->
- **Reuse-First**: Reuse existing components (MudBlazor grid, rich text editor if available), modals, DTOs, validators, and services. Leverage existing ProjectContext and User services.
- **No New Architecture**: Extend existing service patterns; do not introduce new architectural abstractions unless explicitly required.
- **Change Boundary**: Modifications scope to Clarification Tracker feature; do not refactor unrelated code or components.
- **Conditional Planning**: This is a Large Change (>3 files, new entities, new service, DB schema changes); generate execution plan and wait for approval before implementation.

## 3. Business Context
- **Purpose**: Provide a collaborative clarification and Q&A tracking system for project teams to document questions, discuss answers, and maintain audit trail of decisions
- **Parent Layout**: MainLayout.razor (HasActiveTenantSession gate)
- **Navigation**: Will be added to NavMenuComp after Project Holistic View
- **Scope**: Project-level feature (visible/editable within active project context)
- **Users**: Project managers, team leads, team members all have access but with permission boundaries
- **Related Features**: 
  - Discussion/Comment systems (if similar component exists elsewhere)
  - Notification system (for assignment notifications)
  - User/Team management (for assignee selection)
- **Database Requirements**: New entities - Clarification, Answer, AnswerComment, ClarificationAuditLog; New service - ClarificationTrackerService
- **Integration Points**:
  - ProjectContext (active project filtering)
  - ICurrentUser (permission checks, audit logging)
  - Notification system (assignment alerts)
  - Rich text editor component (for Question and Answer fields)
