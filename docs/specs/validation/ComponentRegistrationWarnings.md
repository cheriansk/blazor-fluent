# Component Registration Warnings (Validation Report)

> **Last Generated**: `2026-11-02 21:50:49 -05:00`  
> **Manifest**: [specs-manifest.json](../specs-manifest.json) | **Catalog**: [README.md](../README.md)

> [!CAUTION]
> **19 Unregistered Child Component(s) detected across 5 parent component(s)!** Copy desired entries below into `"children"` of the respective parent in `docs/specs/specs-manifest.json` (or add shared UI widgets to `component_discovery.ignore_components`).

## Parent: `BlazorEnterprise.Web/Components/Layout/MainLayout.razor` ([docs/specs/MainLayout.requirements.md](../../docs/specs/MainLayout.requirements.md))

| Unregistered Component | Source Path | Line | Gate Condition |
| :--- | :--- | :--- | :--- |
| `<CalendarQuickViewPopup />` | `BlazorEnterprise.Web/Components/Layout/CalendarQuickViewPopup.razor` | `L22` | `HasActiveTenantSession` |
| `<ProgramAndProjectSwitcherComp />` | `BlazorEnterprise.Web/Components/Layout/ProgramAndProjectSwitcherComp.razor` | `L23` | `HasActiveTenantSession` |

### Ready-to-Paste JSON for `MainLayout.razor`
```json
"children": [
    {
      "path": "BlazorEnterprise.Web/Components/Layout/CalendarQuickViewPopup.razor",
      "category": "Layout",
      "thinking_level": "low",
      "description": "Child component <CalendarQuickViewPopup /> rendered by MainLayout.razor at L22 (Gate: HasActiveTenantSession).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Layout/ProgramAndProjectSwitcherComp.razor",
      "category": "Layout",
      "thinking_level": "low",
      "description": "Child component <ProgramAndProjectSwitcherComp /> rendered by MainLayout.razor at L23 (Gate: HasActiveTenantSession).",
      "enabled": true
    }
]
```

## Parent: `BlazorEnterprise.Web/Components/Pages/Settings/SettingsPage.razor` ([docs/specs/SettingsPage.requirements.md](../../docs/specs/SettingsPage.requirements.md))

| Unregistered Component | Source Path | Line | Gate Condition |
| :--- | :--- | :--- | :--- |
| `<SettingsJobsTab />` | `BlazorEnterprise.Web/Components/Pages/Settings/SettingsJobsTab.razor` | `L97` | `_activeTabId == "jobs" && _visitedTabs.Contains("jobs")` |
| `<SettingsEventsTab />` | `BlazorEnterprise.Web/Components/Pages/Settings/SettingsEventsTab.razor` | `L101` | `_activeTabId == "events" && _visitedTabs.Contains("events")` |
| `<SettingsAuditTab />` | `BlazorEnterprise.Web/Components/Pages/Settings/SettingsAuditTab.razor` | `L105` | `_activeTabId == "auditing" && _visitedTabs.Contains("auditing")` |
| `<SettingsSessionsTab />` | `BlazorEnterprise.Web/Components/Pages/Settings/SettingsSessionsTab.razor` | `L109` | `_activeTabId == "sessions" && _visitedTabs.Contains("sessions")` |
| `<SettingsUserTab />` | `BlazorEnterprise.Web/Components/Pages/Settings/SettingsUserTab.razor` | `L113` | `_activeTabId == "user" && _visitedTabs.Contains("user")` |
| `<SettingsUserCalendarTab />` | `BlazorEnterprise.Web/Components/Pages/Settings/SettingsUserCalendarTab.razor` | `L117` | `_activeTabId == "calendar" && _visitedTabs.Contains("calendar")` |
| `<SettingsNotificationsTab />` | `BlazorEnterprise.Web/Components/Pages/Settings/SettingsNotificationsTab.razor` | `L121` | `_activeTabId == "notifications" && _visitedTabs.Contains("notifications")` |

### Ready-to-Paste JSON for `SettingsPage.razor`
```json
"children": [
    {
      "path": "BlazorEnterprise.Web/Components/Pages/Settings/SettingsJobsTab.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <SettingsJobsTab /> rendered by SettingsPage.razor at L97 (Gate: _activeTabId == 'jobs' && _visitedTabs.Contains('jobs')).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Pages/Settings/SettingsEventsTab.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <SettingsEventsTab /> rendered by SettingsPage.razor at L101 (Gate: _activeTabId == 'events' && _visitedTabs.Contains('events')).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Pages/Settings/SettingsAuditTab.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <SettingsAuditTab /> rendered by SettingsPage.razor at L105 (Gate: _activeTabId == 'auditing' && _visitedTabs.Contains('auditing')).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Pages/Settings/SettingsSessionsTab.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <SettingsSessionsTab /> rendered by SettingsPage.razor at L109 (Gate: _activeTabId == 'sessions' && _visitedTabs.Contains('sessions')).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Pages/Settings/SettingsUserTab.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <SettingsUserTab /> rendered by SettingsPage.razor at L113 (Gate: _activeTabId == 'user' && _visitedTabs.Contains('user')).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Pages/Settings/SettingsUserCalendarTab.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <SettingsUserCalendarTab /> rendered by SettingsPage.razor at L117 (Gate: _activeTabId == 'calendar' && _visitedTabs.Contains('calendar')).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Pages/Settings/SettingsNotificationsTab.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <SettingsNotificationsTab /> rendered by SettingsPage.razor at L121 (Gate: _activeTabId == 'notifications' && _visitedTabs.Contains('notifications')).",
      "enabled": true
    }
]
```

## Parent: `BlazorEnterprise.Web/Components/Pages/Settings/SettingsTenantsTab.razor` ([docs/specs/SettingsTenantsTab.requirements.md](../../docs/specs/SettingsTenantsTab.requirements.md))

| Unregistered Component | Source Path | Line | Gate Condition |
| :--- | :--- | :--- | :--- |
| `<EntityHistoryButton />` | `BlazorEnterprise.Web/Components/Common/EntityHistoryButton.razor` | `L259` | `_managingUsersTenant is not null` |
| `<DataListSelect />` | `BlazorEnterprise.Web/Components/Common/DataListSelect.razor` | `L526` | `_showAddProgramForm` |

### Ready-to-Paste JSON for `SettingsTenantsTab.razor`
```json
"children": [
    {
      "path": "BlazorEnterprise.Web/Components/Common/EntityHistoryButton.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <EntityHistoryButton /> rendered by SettingsTenantsTab.razor at L259 (Gate: _managingUsersTenant is not null).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Common/DataListSelect.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <DataListSelect /> rendered by SettingsTenantsTab.razor at L526 (Gate: _showAddProgramForm).",
      "enabled": true
    }
]
```

## Parent: `BlazorEnterprise.Web/Components/Pages/Settings/SettingsProgramsTab.razor` ([docs/specs/SettingsProgramsTab.requirements.md](../../docs/specs/SettingsProgramsTab.requirements.md))

| Unregistered Component | Source Path | Line | Gate Condition |
| :--- | :--- | :--- | :--- |
| `<EntityHistoryButton />` | `BlazorEnterprise.Web/Components/Common/EntityHistoryButton.razor` | `L138` | `_isTenantAdmin || CurrentUser.IsRootAdmin || _programAdminIds.Contains(context.Id)` |
| `<DataListSelect />` | `BlazorEnterprise.Web/Components/Common/DataListSelect.razor` | `L371` | `_showAddProjectModal` |
| `<CreateProgramWizardModal />` | `BlazorEnterprise.Web/Components/Pages/Settings/ProgramWizard/CreateProgramWizardModal.razor` | `L598` | `context.IsResolved` |
| `<CreateProjectWizardModal />` | `BlazorEnterprise.Web/Components/Pages/Settings/ProjectWizard/CreateProjectWizardModal.razor` | `L607` | `context.IsResolved` |

### Ready-to-Paste JSON for `SettingsProgramsTab.razor`
```json
"children": [
    {
      "path": "BlazorEnterprise.Web/Components/Common/EntityHistoryButton.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <EntityHistoryButton /> rendered by SettingsProgramsTab.razor at L138 (Gate: _isTenantAdmin || CurrentUser.IsRootAdmin || _programAdminIds.Contains(context.Id)).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Common/DataListSelect.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <DataListSelect /> rendered by SettingsProgramsTab.razor at L371 (Gate: _showAddProjectModal).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Pages/Settings/ProgramWizard/CreateProgramWizardModal.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <CreateProgramWizardModal /> rendered by SettingsProgramsTab.razor at L598 (Gate: context.IsResolved).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Pages/Settings/ProjectWizard/CreateProjectWizardModal.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <CreateProjectWizardModal /> rendered by SettingsProgramsTab.razor at L607 (Gate: context.IsResolved).",
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

