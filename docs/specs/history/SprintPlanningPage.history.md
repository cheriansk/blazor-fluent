# Completed Change History: `SprintPlanningPage`

> **Active Spec**: [SprintPlanningPage.requirements.md](../SprintPlanningPage.requirements.md) | **Navigation Index**: [SprintPlanningPage.index.md](../index/SprintPlanningPage.index.md)

<!-- BEGIN CHANGE HISTORY -->
### 2026-11-06 14:41
- [x] [UI] Create Sprint Timeline management section in Project Settings for project admin.
  Acceptance Criteria:
  - New card/tab in Project Settings accessible only to project admins
  - Display list of all project sprints with columns: Sprint Number, Sprint Name, Start Date, End Date, Status, Actions
  - Create Sprint button opens dialog/modal to create new sprint
  - Create Sprint form contains: Sprint Name (required), Sprint Number (auto-generated or manual), Start Date (required), End Date (required), Comments (optional)
  - Sprint dates must be within project start date and end date
  - Edit button to modify existing sprint details
  - Delete button to remove sprint with confirmation (only if no items assigned)
  - Sort by Sprint Number, Start Date, Status
  - Filter by Status (Planned, Active, Completed)
  - Show total sprint count and sprint status summary
  - Audit trail showing creation date, created by, last modified date, modified by (history only, no versioning)
  - Status auto-calculated based on current date (Planned, Active, Completed)
  - Cannot modify Start/End dates if sprint status is Completed
- [x] [UI] Create Sprint Planning page with multi-tab interface using TabContainerComp.
  Acceptance Criteria:
  - Accessible from left navigation after Resource Management
  - Uses reusable TabContainerComp component
  - Contains 2 tabs: Sprint Summary and Sprint Planner
  - Only visible when user has active tenant and project session
  - Gate: HasActiveTenantSession && HasActiveProject
- [x] [UI] Tab 1 - Sprint Summary View displaying sprint overview and backlog items.
  Acceptance Criteria:
  - Sprint dropdown/selector with all project sprints plus "Backlog" option
  - When sprint selected, display sprint summary metrics:
  - Sprint Name and Sprint Number
  - Sprint Status (Planned, Active, Completed)
  - Sprint Dates (Start Date - End Date)
  - Planned Hours (total estimated hours for sprint)
  - Completed Hours (hours of completed items)
  - Blocked Hours (hours of blocked/at-risk items)
  - Remaining Hours (Planned - Completed)
  - Completion Percentage (Completed / Planned × 100%)
  - Team Velocity (if historical data available)
  - Collapsible section below metrics showing all items in selected sprint
  - Item list columns: ID, Title, Status, Assigned To, Estimated Hours, Actual Hours, Start Date, End Date
  - Sortable by all columns
  - Filterable by Status, Assigned To
  - Color coding by Status (Backlog=Gray, Planned=Blue, In Progress=Orange, Completed=Green, Blocked=Red)
- [x] [UI] Tab 2 - Sprint Planner displaying task allocation and resource capacity.
  Acceptance Criteria:
  - Sprint dropdown/selector with all project sprints plus "All" option
  - When sprint selected, display all items for that sprint (or all unallocated items if "All" selected)
  - Split view layout:
  - Left panel (60%): All tasks/items list with columns: ID, Title, Current Sprint, Status, Estimated Hours, Assigned To
  - Sortable by all columns
  - Filterable by Status, Assigned To
  - Multi-select checkbox on each item
  - Color coding by Status
  - Right panel (40%): Resource Capacity View
  - List of all project resources with columns: Resource Name, Role, Available Capacity (hours), Allocated Hours, Remaining Capacity
  - Sort by Resource Name, Available Capacity, Allocated Hours
  - Filter by Role
  - Move Items between Sprints action:
  - Select one or more items from left panel
  - "Move to Sprint" button appears when items selected
  - Click button opens modal with columns: ID, Title of Task, Current Sprint, New Sprint (dropdown)
  - New Sprint dropdown shows all sprints except ones marked as Completed
  - Cannot move items from sprints marked as Completed status
  - Bulk move to same new sprint for all selected items
  - Confirm button applies move operation
  - Show success/error messages
  - Real-time update: After moving items, refresh both panels
  - Item filtering affects multi-select (only visible filtered items can be moved)
- [x] [DATA] Create supporting data models/DTOs for Sprint Planning.
  Acceptance Criteria:
  - Sprint entity with: Project (foreign key), Sprint Number (int), Sprint Name (string), Start Date (datetime), End Date (datetime), Comments (string, nullable), Status (enum), Created Date, Created By (User), Last Modified Date, Last Modified By (User)
  - SprintAuditLog entity with: Sprint (foreign key), Change Type (Created, Modified, Deleted), Changed By (User), Changed Date, Old Value, New Value, Details
  - SprintItemVersion entity implementing ISprintVersionEntity with: Item (foreign key), Sprint (foreign key), Version (int), Assigned Sprint (can vary per version), Estimated Hours, Actual Hours, Status, Effective From Date, Effective To Date (nullable)
  - SprintStatus enumeration with: Planned, Active, Completed
  - Unique constraint on (Project, SprintNumber) to ensure unique sprint numbering per project
  - Indexes on (Project, Status), (Sprint, EffectiveToDate), and (Item, Sprint)
- [x] [SERVICE] Create SprintPlanningService for managing sprint operations and versioning.
  Acceptance Criteria:
  - GetProjectSprintsAsync(projectId) - retrieve all sprints for project
  - CreateSprintAsync(projectId, sprintName, sprintNumber, startDate, endDate, comments) - create new sprint (validate dates within project timeline)
  - UpdateSprintAsync(sprintId, sprintName, startDate, endDate, comments) - update sprint details (cannot modify dates if Completed)
  - DeleteSprintAsync(sprintId) - delete sprint if no items assigned
  - GetSprintStatusAsync(sprintId) - calculate and return sprint status (Planned/Active/Completed)
  - GetSprintItemsAsync(sprintId) - retrieve all items assigned to sprint
  - GetSprintMetricsAsync(sprintId) - calculate planned hours, completed hours, blocked hours, remaining hours, completion %
  - MoveItemsToSprintAsync(itemIds, targetSprintId) - move multiple items to new sprint (creates new versions, validates source sprints not Completed)
  - GetSprintAuditHistoryAsync(sprintId) - retrieve change history for sprint
  - GetItemSprintVersionHistoryAsync(itemId) - retrieve version history of item across sprints
  - ValidateSprintDatesAsync(projectId, startDate, endDate) - validate dates within project timeline
  - CalculateResourceCapacityAsync(projectId, startDate, endDate) - get available resource hours for date range
  - GetItemsReadyForSprintAsync(projectId, sprintId) - get backlog items that could be added to sprint
- [x] [UI] Update NavMenuComp to include navigation link to Sprint Planning.
  Acceptance Criteria:
  - Link visible after Resource Management
  - Only visible when user has active tenant and project session
  - Uses appropriate icon (e.g., calendar/sprint icon)
  - Responsive to collapsed/expanded navigation state
<!-- END CHANGE HISTORY -->
