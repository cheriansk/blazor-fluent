# Completed Change History: `SettingsProgramsTab`

> **Active Spec**: [SettingsProgramsTab.requirements.md](../SettingsProgramsTab.requirements.md) | **Navigation Index**: [SettingsProgramsTab.index.md](../index/SettingsProgramsTab.index.md)

<!-- BEGIN CHANGE HISTORY -->
### 2026-10-30 16:53
- [x] [UI] **Program Creation & Editing Wizard Modal**
  - Add 4-step wizard modal (`CreateProgramWizardModal`) triggered by 'Add Program' button or program row 'Edit' action
  - Step 1 (Details): Program Code, Name, Summary, Status dropdown, and Admin Users multi-select with Add/Edit/Remove actions
  - Step 2 (Timeline): Start Date and End Date pickers
  - Step 3 (Milestone): Add/Edit/Remove milestone entries with name, target date, and optional description
  - Step 4 (Review): Read-only summary of all entered data with option to go back or confirm
  - Acceptance Criteria:
  - Wizard pre-seeds program ID when editing existing program
  - Persists to `TenantService.CreateTenantProgramAsync` / `UpdateTenantProgramAsync`
  - Automatically refreshes Programs DataGrid after successful save
  - Close/Cancel discards unsaved changes
- [x] [UI] **Program Row Action Buttons**
  - Replace 'Select' button functionality with 'View' and 'Edit' row action buttons
  - 'View' opens wizard in read-only mode (all fields disabled, no Save button)
  - 'Edit' opens wizard in edit mode (all fields enabled, Save button visible)
  - Remove Select checkbox and associated multi-select toolbar
- [x] [DATA] **Admin Users Management in Wizard Step 1**
  - Integrate existing user selection/management pattern (reuse `CskUserSelect` or equivalent component)
  - Support Add, Edit, Remove actions on Admin Users list within Step 1
  - Persist admin user assignments to program entity via `TenantService`
- [x] [VALIDATION] **Wizard Form Validation**
  - Program Code: Required, unique within tenant (via `TenantService.ValidateProgramCodeAsync`)
  - Program Name: Required, max 255 characters
  - Status: Required, must be one of predefined enum values
  - Timeline: Start Date must be <= End Date (if both provided)
  - Milestone: Each milestone requires name and target date; optional description
  - Admin Users: At least one admin user required

### 2026-10-30 15:40
- [x] [UI] New Program Wizard Modal with 5-step flow
  - Acceptance Criteria:
  - Step 1: Details page with Code, Name, Summary fields
  - Step 2: Program Timeline (start date, end date)
  - Step 3: Milestones (add/edit/remove milestone rows)
  - Step 4: Manage Admins (add/remove program administrators)
  - Step 5: Review page displaying all entered data with Create button
  - Modal launches from 'Add Program' button in SettingsProgramsTab toolbar
  - Each step includes validation before proceeding to next step
  - Back/Next/Cancel navigation between steps
  - On Create, submits ProgramCreateRequest DTO to ProgramsService.CreateProgramAsync()
- [x] [UI] When a program row is expanded, display project details table below it with proper header
  - Acceptance Criteria:
  - Show Projects table directly under expanded program row
  - Header includes program identifier and "Add Project" button
  - Display columns: Code, Project Name, Status, Timeline, Actions
  - Header styling matches the Programs DataGrid header (same CSS classes, colors, typography)
  - Consistent spacing and padding with main table layout
  - Only show project data for the expanded program
<!-- END CHANGE HISTORY -->
