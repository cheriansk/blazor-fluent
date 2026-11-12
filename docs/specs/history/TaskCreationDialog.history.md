# Completed Change History: `TaskCreationDialog`

> **Active Spec**: [TaskCreationDialog.requirements.md](../TaskCreationDialog.requirements.md) | **Navigation Index**: [TaskCreationDialog.index.md](../index/TaskCreationDialog.index.md)

<!-- BEGIN CHANGE HISTORY -->
### 2026-11-12 11:25
- [x] [UI] Create a reusable Task Creation Dialog component for creating different task types.
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
- [x] [UI] Create dynamic Task Type selector with dropdown in New Task button.
  Acceptance Criteria:
  - "New Task" button with integrated dropdown arrow
  - Dropdown options: Sprint Task, Misc Task, Risk, Assumption, Issue
  - Default selection: Sprint Task (or none - user must select)
  - Selecting type from dropdown opens TaskCreationDialog
  - Selected type is passed to dialog and cannot be changed in dialog
  - Show selected type as label/badge in dialog header
  - Dropdown closes after selection
- [x] [UI] Sprint Task type - additional fields displayed in dialog.
  Acceptance Criteria:
  - Sprint (dropdown, required for Sprint Task, shows all open sprints)
  - Story Point Estimate (numeric input, optional)
  - Acceptance Criteria (rich text editor, optional)
  - Sub-task checkbox (if parent task selection enabled)
  - Test Case Reference (text input or link, optional)
  - Definition of Done checklist (optional)
- [x] [UI] Misc Task type - additional fields displayed in dialog.
  Acceptance Criteria:
  - Category (dropdown: Admin, Documentation, Review, Cleanup, Other, optional)
  - Related Link (URL input, optional)
  - Task Type detail (text input, optional)
  - Recurrence (dropdown: None, Daily, Weekly, Monthly, optional)
  - Recurrence End Date (date picker, optional, shown if Recurrence != None)
- [x] [UI] Risk type - additional fields displayed in dialog.
  Acceptance Criteria:
  - Risk Category (dropdown: Technical, Schedule, Resource, Budget, External, Other, required)
  - Probability (dropdown: Low, Medium, High, Very High, required)
  - Impact (dropdown: Low, Medium, High, Critical, required)
  - Mitigation Strategy (rich text editor, required)
  - Risk Owner (user dropdown, required)
  - Contingency Plan (rich text editor, optional)
  - Exposure Level (auto-calculated: Probability × Impact, display only)
- [x] [UI] Assumption type - additional fields displayed in dialog.
  Acceptance Criteria:
  - Assumption Category (dropdown: Technical, Business, Resource, Schedule, Vendor, Other, required)
  - Validation Criteria (rich text editor, required - how will this assumption be validated)
  - Validation Date (date picker, optional - when will assumption be validated)
  - Assumption Owner (user dropdown, required)
  - Dependency on Other Assumptions (multi-select, optional)
  - Validation Status (dropdown: Unvalidated, In Review, Validated, Invalid, optional)
- [x] [UI] Issue type - additional fields displayed in dialog.
  Acceptance Criteria:
  - Issue Category (dropdown: Technical, Process, Quality, Communication, Vendor, Other, required)
  - Root Cause (rich text editor, required)
  - Impact Assessment (rich text editor, required)
  - Issue Owner (user dropdown, required)
  - Resolution Steps (rich text editor, optional - step-by-step resolution plan)
  - Related Issues (multi-select dropdown, optional - link to other issues)
  - Severity Level (dropdown: Low, Medium, High, Critical, required)
  - Target Resolution Date (date picker, optional)
- [x] [VALIDATION] Implement field validation rules for all task types.
  Acceptance Criteria:
  - BaseTaskEntity fields: Title required, max 255 chars; Due Date within project dates; Estimated Hours >= 0
  - Sprint Task: Sprint required, Sprint must not be Completed status
  - Risk: All required fields must be filled; Probability and Impact must be valid; Mitigation Strategy min 10 characters
  - Assumption: All required fields must be filled; Validation Criteria min 10 characters
  - Issue: All required fields must be filled; Root Cause and Impact min 10 characters each
  - Cross-field validation: Assigned To must be active project member
  - Show validation messages below each field in real-time as user types/changes fields
  - Disable Create button if any required field is empty or invalid
- [x] [UI] Create Task Type Selection Button Component for reuse across pages.
  Acceptance Criteria:
  - Standalone "New Task" button with dropdown
  - Can be placed in page headers or toolbars
  - Accepts callback parameter for task type selection
  - Accepts optional filter parameter to show only specific task types
  - Button text customizable (e.g., "New Task", "Add Task", "Create Item")
  - Icon customizable (default: plus icon)
  - Shows loading state while dialog opens
  - Returns selected task type to parent component
- [x] [SERVICE] Create TaskCreationService for handling task creation across all types.
  Acceptance Criteria:
  - CreateTaskAsync(baseTaskData, taskTypeData) - create task with type-specific data
  - ValidateTaskDataAsync(taskType, formData) - validate all fields for specific type
  - GetTaskTypeFieldsAsync(taskType) - return list of required/optional fields for type
  - GetRelatedDataAsync(projectId, taskType) - get dropdown options (sprints, users, categories) based on type
  - PopulateDefaultValuesAsync(taskType, projectId) - set default values based on task type and project
  - CalculateTaskMetricsAsync(taskType, data) - auto-calculate fields (e.g., Risk Exposure)
  - GetTaskTypeDisplayNameAsync(taskType) - return localized display name
- [x] [UI] Dialog State Management and UX enhancements.
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
<!-- END CHANGE HISTORY -->
