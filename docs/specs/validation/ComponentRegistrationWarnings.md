# Component Registration Warnings (Validation Report)

> **Last Generated**: `2026-11-12 11:18:58 -05:00`  
> **Manifest**: [specs-manifest.json](../specs-manifest.json) | **Catalog**: [README.md](../README.md)

> [!CAUTION]
> **31 Unregistered Child Component(s) detected across 11 parent component(s)!** Copy desired entries below into `"children"` of the respective parent in `docs/specs/specs-manifest.json` (or add shared UI widgets to `component_discovery.ignore_components`).

## Parent: `BlazorEnterprise.Web/Components/Layout/MainLayout.razor` ([docs/specs/MainLayout.requirements.md](../../docs/specs/MainLayout.requirements.md))

| Unregistered Component | Source Path | Line | Gate Condition |
| :--- | :--- | :--- | :--- |
| `<CalendarQuickViewPopup />` | `BlazorEnterprise.Web/Components/Layout/CalendarQuickViewPopup.razor` | `L24` | `HasActiveTenantSession` |
| `<ProgramAndProjectSwitcherComp />` | `BlazorEnterprise.Web/Components/Layout/ProgramAndProjectSwitcherComp.razor` | `L25` | `HasActiveTenantSession` |

### Ready-to-Paste JSON for `MainLayout.razor`
```json
"children": [
    {
      "path": "BlazorEnterprise.Web/Components/Layout/CalendarQuickViewPopup.razor",
      "category": "Layout",
      "thinking_level": "low",
      "description": "Child component <CalendarQuickViewPopup /> rendered by MainLayout.razor at L24 (Gate: HasActiveTenantSession).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Layout/ProgramAndProjectSwitcherComp.razor",
      "category": "Layout",
      "thinking_level": "low",
      "description": "Child component <ProgramAndProjectSwitcherComp /> rendered by MainLayout.razor at L25 (Gate: HasActiveTenantSession).",
      "enabled": true
    }
]
```

## Parent: `BlazorEnterprise.Web/Components/Pages/Settings/SettingsPage.razor` ([docs/specs/SettingsPage.requirements.md](../../docs/specs/SettingsPage.requirements.md))

| Unregistered Component | Source Path | Line | Gate Condition |
| :--- | :--- | :--- | :--- |
| `<SettingsJobsTab />` | `BlazorEnterprise.Web/Components/Pages/Settings/SettingsJobsTab.razor` | `L113` | `_activeTabId == "jobs" && _visitedTabs.Contains("jobs")` |
| `<SettingsEventsTab />` | `BlazorEnterprise.Web/Components/Pages/Settings/SettingsEventsTab.razor` | `L117` | `_activeTabId == "events" && _visitedTabs.Contains("events")` |
| `<SettingsAuditTab />` | `BlazorEnterprise.Web/Components/Pages/Settings/SettingsAuditTab.razor` | `L121` | `_activeTabId == "auditing" && _visitedTabs.Contains("auditing")` |
| `<SettingsSessionsTab />` | `BlazorEnterprise.Web/Components/Pages/Settings/SettingsSessionsTab.razor` | `L125` | `_activeTabId == "sessions" && _visitedTabs.Contains("sessions")` |
| `<SettingsUserTab />` | `BlazorEnterprise.Web/Components/Pages/Settings/SettingsUserTab.razor` | `L129` | `_activeTabId == "user" && _visitedTabs.Contains("user")` |
| `<SettingsUserCalendarTab />` | `BlazorEnterprise.Web/Components/Pages/Settings/SettingsUserCalendarTab.razor` | `L133` | `_activeTabId == "calendar" && _visitedTabs.Contains("calendar")` |
| `<SettingsRateCardTab />` | `BlazorEnterprise.Web/Components/Pages/Settings/SettingsRateCardTab.razor` | `L137` | `_activeTabId == "ratecard" && _visitedTabs.Contains("ratecard")` |
| `<SettingsSprintTimelineTab />` | `BlazorEnterprise.Web/Components/Pages/Settings/SettingsSprintTimelineTab.razor` | `L141` | `_activeTabId == "sprinttimeline" && _visitedTabs.Contains("sprinttimeline")` |
| `<SettingsNotificationsTab />` | `BlazorEnterprise.Web/Components/Pages/Settings/SettingsNotificationsTab.razor` | `L145` | `_activeTabId == "notifications" && _visitedTabs.Contains("notifications")` |

### Ready-to-Paste JSON for `SettingsPage.razor`
```json
"children": [
    {
      "path": "BlazorEnterprise.Web/Components/Pages/Settings/SettingsJobsTab.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <SettingsJobsTab /> rendered by SettingsPage.razor at L113 (Gate: _activeTabId == 'jobs' && _visitedTabs.Contains('jobs')).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Pages/Settings/SettingsEventsTab.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <SettingsEventsTab /> rendered by SettingsPage.razor at L117 (Gate: _activeTabId == 'events' && _visitedTabs.Contains('events')).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Pages/Settings/SettingsAuditTab.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <SettingsAuditTab /> rendered by SettingsPage.razor at L121 (Gate: _activeTabId == 'auditing' && _visitedTabs.Contains('auditing')).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Pages/Settings/SettingsSessionsTab.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <SettingsSessionsTab /> rendered by SettingsPage.razor at L125 (Gate: _activeTabId == 'sessions' && _visitedTabs.Contains('sessions')).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Pages/Settings/SettingsUserTab.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <SettingsUserTab /> rendered by SettingsPage.razor at L129 (Gate: _activeTabId == 'user' && _visitedTabs.Contains('user')).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Pages/Settings/SettingsUserCalendarTab.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <SettingsUserCalendarTab /> rendered by SettingsPage.razor at L133 (Gate: _activeTabId == 'calendar' && _visitedTabs.Contains('calendar')).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Pages/Settings/SettingsRateCardTab.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <SettingsRateCardTab /> rendered by SettingsPage.razor at L137 (Gate: _activeTabId == 'ratecard' && _visitedTabs.Contains('ratecard')).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Pages/Settings/SettingsSprintTimelineTab.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <SettingsSprintTimelineTab /> rendered by SettingsPage.razor at L141 (Gate: _activeTabId == 'sprinttimeline' && _visitedTabs.Contains('sprinttimeline')).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Pages/Settings/SettingsNotificationsTab.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <SettingsNotificationsTab /> rendered by SettingsPage.razor at L145 (Gate: _activeTabId == 'notifications' && _visitedTabs.Contains('notifications')).",
      "enabled": true
    }
]
```

## Parent: `BlazorEnterprise.Web/Components/Pages/Settings/SettingsTenantsTab.razor` ([docs/specs/SettingsTenantsTab.requirements.md](../../docs/specs/SettingsTenantsTab.requirements.md))

| Unregistered Component | Source Path | Line | Gate Condition |
| :--- | :--- | :--- | :--- |
| `<EntityHistoryButton />` | `BlazorEnterprise.Web/Components/Common/EntityHistoryButton.razor` | `L257` | `_managingUsersTenant is not null` |
| `<DataListSelect />` | `BlazorEnterprise.Web/Components/Common/DataListSelect.razor` | `L524` | `_showAddProgramForm` |

### Ready-to-Paste JSON for `SettingsTenantsTab.razor`
```json
"children": [
    {
      "path": "BlazorEnterprise.Web/Components/Common/EntityHistoryButton.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <EntityHistoryButton /> rendered by SettingsTenantsTab.razor at L257 (Gate: _managingUsersTenant is not null).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Common/DataListSelect.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <DataListSelect /> rendered by SettingsTenantsTab.razor at L524 (Gate: _showAddProgramForm).",
      "enabled": true
    }
]
```

## Parent: `BlazorEnterprise.Web/Components/Pages/Settings/SettingsProgramsTab.razor` ([docs/specs/SettingsProgramsTab.requirements.md](../../docs/specs/SettingsProgramsTab.requirements.md))

| Unregistered Component | Source Path | Line | Gate Condition |
| :--- | :--- | :--- | :--- |
| `<EntityHistoryButton />` | `BlazorEnterprise.Web/Components/Common/EntityHistoryButton.razor` | `L237` | `_programs.Count == 0` |
| `<DataListSelect />` | `BlazorEnterprise.Web/Components/Common/DataListSelect.razor` | `L671` | `_showAddProjectModal` |
| `<CreateProgramWizardModal />` | `BlazorEnterprise.Web/Components/Pages/Settings/ProgramWizard/CreateProgramWizardModal.razor` | `L1109` | `context.IsResolved` |
| `<CreateProjectWizardModal />` | `BlazorEnterprise.Web/Components/Pages/Settings/ProjectWizard/CreateProjectWizardModal.razor` | `L1129` | `context.IsResolved` |

### Ready-to-Paste JSON for `SettingsProgramsTab.razor`
```json
"children": [
    {
      "path": "BlazorEnterprise.Web/Components/Common/EntityHistoryButton.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <EntityHistoryButton /> rendered by SettingsProgramsTab.razor at L237 (Gate: _programs.Count == 0).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Common/DataListSelect.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <DataListSelect /> rendered by SettingsProgramsTab.razor at L671 (Gate: _showAddProjectModal).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Pages/Settings/ProgramWizard/CreateProgramWizardModal.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <CreateProgramWizardModal /> rendered by SettingsProgramsTab.razor at L1109 (Gate: context.IsResolved).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Pages/Settings/ProjectWizard/CreateProjectWizardModal.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <CreateProjectWizardModal /> rendered by SettingsProgramsTab.razor at L1129 (Gate: context.IsResolved).",
      "enabled": true
    }
]
```

## Parent: `BlazorEnterprise.Web/Components/Pages/HomePage.razor` ([docs/specs/HomePage.requirements.md](../../docs/specs/HomePage.requirements.md))

| Unregistered Component | Source Path | Line | Gate Condition |
| :--- | :--- | :--- | :--- |
| `<ProgramSummaryCard />` | `BlazorEnterprise.Web/Components/Pages/Dashboard/ProgramSummaryCard.razor` | `L52` | `!_isRootAdminOnly` |
| `<MyProjectsGrid />` | `BlazorEnterprise.Web/Components/Pages/Dashboard/MyProjectsGrid.razor` | `L55` | `!_isRootAdminOnly` |
| `<DashboardKanbanBoard />` | `BlazorEnterprise.Web/Components/Pages/Dashboard/DashboardKanbanBoard.razor` | `L59` | `!_isRootAdminOnly` |
| `<DashboardAuditTrail />` | `BlazorEnterprise.Web/Components/Pages/Dashboard/DashboardAuditTrail.razor` | `L60` | `!_isRootAdminOnly` |

### Ready-to-Paste JSON for `HomePage.razor`
```json
"children": [
    {
      "path": "BlazorEnterprise.Web/Components/Pages/Dashboard/ProgramSummaryCard.razor",
      "category": "Home page UI",
      "thinking_level": "low",
      "description": "Child component <ProgramSummaryCard /> rendered by HomePage.razor at L52 (Gate: !_isRootAdminOnly).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Pages/Dashboard/MyProjectsGrid.razor",
      "category": "Home page UI",
      "thinking_level": "low",
      "description": "Child component <MyProjectsGrid /> rendered by HomePage.razor at L55 (Gate: !_isRootAdminOnly).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Pages/Dashboard/DashboardKanbanBoard.razor",
      "category": "Home page UI",
      "thinking_level": "low",
      "description": "Child component <DashboardKanbanBoard /> rendered by HomePage.razor at L59 (Gate: !_isRootAdminOnly).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Pages/Dashboard/DashboardAuditTrail.razor",
      "category": "Home page UI",
      "thinking_level": "low",
      "description": "Child component <DashboardAuditTrail /> rendered by HomePage.razor at L60 (Gate: !_isRootAdminOnly).",
      "enabled": true
    }
]
```

## Parent: `BlazorEnterprise.Web/Components/Pages/ProgramHolisticView.razor` ([docs/specs/ProgramHolisticView.requirements.md](../../docs/specs/ProgramHolisticView.requirements.md))

| Unregistered Component | Source Path | Line | Gate Condition |
| :--- | :--- | :--- | :--- |
| `<TaskTypeSelectionButton />` | `BlazorEnterprise.Web/Components/Dialogs/TaskTypeSelectionButton.razor` | `L30` | `ProjectContext.ProgramId.HasValue && ProjectContext.ProgramId.Value != Guid.Empty` |
| `<TaskCreationDialog />` | `BlazorEnterprise.Web/Components/Dialogs/TaskCreationDialog.razor` | `L43` | `ProjectContext.ProgramId.HasValue && ProjectContext.ProgramId.Value != Guid.Empty` |

### Ready-to-Paste JSON for `ProgramHolisticView.razor`
```json
"children": [
    {
      "path": "BlazorEnterprise.Web/Components/Dialogs/TaskTypeSelectionButton.razor",
      "category": "Program Management UI",
      "thinking_level": "low",
      "description": "Child component <TaskTypeSelectionButton /> rendered by ProgramHolisticView.razor at L30 (Gate: ProjectContext.ProgramId.HasValue && ProjectContext.ProgramId.Value != Guid.Empty).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Dialogs/TaskCreationDialog.razor",
      "category": "Program Management UI",
      "thinking_level": "low",
      "description": "Child component <TaskCreationDialog /> rendered by ProgramHolisticView.razor at L43 (Gate: ProjectContext.ProgramId.HasValue && ProjectContext.ProgramId.Value != Guid.Empty).",
      "enabled": true
    }
]
```

## Parent: `BlazorEnterprise.Web/Components/Pages/ProgramHolisticView/ProgramHolisticViewTimelineTab.razor` ([docs/specs/ProgramHolisticViewTimelineTab.requirements.md](../../docs/specs/ProgramHolisticViewTimelineTab.requirements.md))

| Unregistered Component | Source Path | Line | Gate Condition |
| :--- | :--- | :--- | :--- |
| `<RevisionToolbarComp />` | `BlazorEnterprise.Web/Components/Common/RevisionToolbarComp.razor` | `L7` | `Unconditional` |

### Ready-to-Paste JSON for `ProgramHolisticViewTimelineTab.razor`
```json
"children": [
    {
      "path": "BlazorEnterprise.Web/Components/Common/RevisionToolbarComp.razor",
      "category": "Program Management UI",
      "thinking_level": "low",
      "description": "Child component <RevisionToolbarComp /> rendered by ProgramHolisticViewTimelineTab.razor at L7 (Gate: Unconditional).",
      "enabled": true
    }
]
```

## Parent: `BlazorEnterprise.Web/Components/Pages/ProjectHolisticView.razor` ([docs/specs/ProjectHolisticView.requirements.md](../../docs/specs/ProjectHolisticView.requirements.md))

| Unregistered Component | Source Path | Line | Gate Condition |
| :--- | :--- | :--- | :--- |
| `<TaskTypeSelectionButton />` | `BlazorEnterprise.Web/Components/Dialogs/TaskTypeSelectionButton.razor` | `L30` | `ProjectContext.ProjectId.HasValue && ProjectContext.ProjectId.Value != Guid.Empty` |
| `<TaskCreationDialog />` | `BlazorEnterprise.Web/Components/Dialogs/TaskCreationDialog.razor` | `L42` | `ProjectContext.ProjectId.HasValue && ProjectContext.ProjectId.Value != Guid.Empty` |

### Ready-to-Paste JSON for `ProjectHolisticView.razor`
```json
"children": [
    {
      "path": "BlazorEnterprise.Web/Components/Dialogs/TaskTypeSelectionButton.razor",
      "category": "Project Management UI",
      "thinking_level": "low",
      "description": "Child component <TaskTypeSelectionButton /> rendered by ProjectHolisticView.razor at L30 (Gate: ProjectContext.ProjectId.HasValue && ProjectContext.ProjectId.Value != Guid.Empty).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Dialogs/TaskCreationDialog.razor",
      "category": "Project Management UI",
      "thinking_level": "low",
      "description": "Child component <TaskCreationDialog /> rendered by ProjectHolisticView.razor at L42 (Gate: ProjectContext.ProjectId.HasValue && ProjectContext.ProjectId.Value != Guid.Empty).",
      "enabled": true
    }
]
```

## Parent: `BlazorEnterprise.Web/Components/Pages/ProjectHolisticView/ProjectHolisticViewTimelineTab.razor` ([docs/specs/ProjectHolisticViewTimelineTab.requirements.md](../../docs/specs/ProjectHolisticViewTimelineTab.requirements.md))

| Unregistered Component | Source Path | Line | Gate Condition |
| :--- | :--- | :--- | :--- |
| `<RevisionToolbarComp />` | `BlazorEnterprise.Web/Components/Common/RevisionToolbarComp.razor` | `L7` | `Unconditional` |

### Ready-to-Paste JSON for `ProjectHolisticViewTimelineTab.razor`
```json
"children": [
    {
      "path": "BlazorEnterprise.Web/Components/Common/RevisionToolbarComp.razor",
      "category": "Project Management UI",
      "thinking_level": "low",
      "description": "Child component <RevisionToolbarComp /> rendered by ProjectHolisticViewTimelineTab.razor at L7 (Gate: Unconditional).",
      "enabled": true
    }
]
```

## Parent: `BlazorEnterprise.Web/Components/Pages/Resources/ResourceManagementPage.razor` ([docs/specs/ResourceManagementPage.requirements.md](../../docs/specs/ResourceManagementPage.requirements.md))

| Unregistered Component | Source Path | Line | Gate Condition |
| :--- | :--- | :--- | :--- |
| `<RevisionToolbarComp />` | `BlazorEnterprise.Web/Components/Common/RevisionToolbarComp.razor` | `L44` | `_planSummary != null` |
| `<ResourceSelectionModal />` | `BlazorEnterprise.Web/Components/Pages/Resources/ResourceSelectionModal.razor` | `L203` | `_isReadOnly` |

### Ready-to-Paste JSON for `ResourceManagementPage.razor`
```json
"children": [
    {
      "path": "BlazorEnterprise.Web/Components/Common/RevisionToolbarComp.razor",
      "category": "General",
      "thinking_level": "low",
      "description": "Child component <RevisionToolbarComp /> rendered by ResourceManagementPage.razor at L44 (Gate: _planSummary != null).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Pages/Resources/ResourceSelectionModal.razor",
      "category": "General",
      "thinking_level": "low",
      "description": "Child component <ResourceSelectionModal /> rendered by ResourceManagementPage.razor at L203 (Gate: _isReadOnly).",
      "enabled": true
    }
]
```

## Parent: `BlazorEnterprise.Web/Components/Pages/Sprints/SprintPlanningPage.razor` ([docs/specs/SprintPlanningPage.requirements.md](../../docs/specs/SprintPlanningPage.requirements.md))

| Unregistered Component | Source Path | Line | Gate Condition |
| :--- | :--- | :--- | :--- |
| `<TaskTypeSelectionButton />` | `BlazorEnterprise.Web/Components/Dialogs/TaskTypeSelectionButton.razor` | `L36` | `!ProjectContext.HasProject` |
| `<TaskCreationDialog />` | `BlazorEnterprise.Web/Components/Dialogs/TaskCreationDialog.razor` | `L48` | `!ProjectContext.HasProject` |

### Ready-to-Paste JSON for `SprintPlanningPage.razor`
```json
"children": [
    {
      "path": "BlazorEnterprise.Web/Components/Dialogs/TaskTypeSelectionButton.razor",
      "category": "Pages",
      "thinking_level": "low",
      "description": "Child component <TaskTypeSelectionButton /> rendered by SprintPlanningPage.razor at L36 (Gate: !ProjectContext.HasProject).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Dialogs/TaskCreationDialog.razor",
      "category": "Pages",
      "thinking_level": "low",
      "description": "Child component <TaskCreationDialog /> rendered by SprintPlanningPage.razor at L48 (Gate: !ProjectContext.HasProject).",
      "enabled": true
    }
]
```

