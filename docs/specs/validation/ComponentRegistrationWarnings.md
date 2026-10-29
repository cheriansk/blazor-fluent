# Component Registration Warnings (Validation Report)

> **Last Generated**: `2026-10-29 12:57:08 -04:00`  
> **Manifest**: [specs-manifest.json](../specs-manifest.json) | **Catalog**: [README.md](../README.md)

> [!CAUTION]
> **12 Unregistered Child Component(s) detected across 3 parent component(s)!** Copy desired entries below into `"children"` of the respective parent in `docs/specs/specs-manifest.json` (or add shared UI widgets to `component_discovery.ignore_components`).

## Parent: `BlazorEnterprise.Web/Components/Pages/Settings/SettingsPage.razor` ([docs/specs/SettingsPage.requirements.md](../../docs/specs/SettingsPage.requirements.md))

| Unregistered Component | Source Path | Line | Gate Condition |
| :--- | :--- | :--- | :--- |
| `<SettingsProgramsTab />` | `BlazorEnterprise.Web/Components/Pages/Settings/SettingsProgramsTab.razor` | `L99` | `_activeTabId == "programs" && _visitedTabs.Contains("programs")` |
| `<SettingsJobsTab />` | `BlazorEnterprise.Web/Components/Pages/Settings/SettingsJobsTab.razor` | `L107` | `_activeTabId == "jobs" && _visitedTabs.Contains("jobs")` |
| `<SettingsEventsTab />` | `BlazorEnterprise.Web/Components/Pages/Settings/SettingsEventsTab.razor` | `L111` | `_activeTabId == "events" && _visitedTabs.Contains("events")` |
| `<SettingsAuditTab />` | `BlazorEnterprise.Web/Components/Pages/Settings/SettingsAuditTab.razor` | `L115` | `_activeTabId == "auditing" && _visitedTabs.Contains("auditing")` |
| `<SettingsSessionsTab />` | `BlazorEnterprise.Web/Components/Pages/Settings/SettingsSessionsTab.razor` | `L119` | `_activeTabId == "sessions" && _visitedTabs.Contains("sessions")` |
| `<SettingsUserTab />` | `BlazorEnterprise.Web/Components/Pages/Settings/SettingsUserTab.razor` | `L123` | `_activeTabId == "user" && _visitedTabs.Contains("user")` |
| `<SettingsUserCalendarTab />` | `BlazorEnterprise.Web/Components/Pages/Settings/SettingsUserCalendarTab.razor` | `L127` | `_activeTabId == "calendar" && _visitedTabs.Contains("calendar")` |
| `<SettingsNotificationsTab />` | `BlazorEnterprise.Web/Components/Pages/Settings/SettingsNotificationsTab.razor` | `L131` | `_activeTabId == "notifications" && _visitedTabs.Contains("notifications")` |

### Ready-to-Paste JSON for `SettingsPage.razor`
```json
"children": [
    {
      "path": "BlazorEnterprise.Web/Components/Pages/Settings/SettingsProgramsTab.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <SettingsProgramsTab /> rendered by SettingsPage.razor at L99 (Gate: _activeTabId == 'programs' && _visitedTabs.Contains('programs')).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Pages/Settings/SettingsJobsTab.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <SettingsJobsTab /> rendered by SettingsPage.razor at L107 (Gate: _activeTabId == 'jobs' && _visitedTabs.Contains('jobs')).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Pages/Settings/SettingsEventsTab.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <SettingsEventsTab /> rendered by SettingsPage.razor at L111 (Gate: _activeTabId == 'events' && _visitedTabs.Contains('events')).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Pages/Settings/SettingsAuditTab.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <SettingsAuditTab /> rendered by SettingsPage.razor at L115 (Gate: _activeTabId == 'auditing' && _visitedTabs.Contains('auditing')).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Pages/Settings/SettingsSessionsTab.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <SettingsSessionsTab /> rendered by SettingsPage.razor at L119 (Gate: _activeTabId == 'sessions' && _visitedTabs.Contains('sessions')).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Pages/Settings/SettingsUserTab.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <SettingsUserTab /> rendered by SettingsPage.razor at L123 (Gate: _activeTabId == 'user' && _visitedTabs.Contains('user')).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Pages/Settings/SettingsUserCalendarTab.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <SettingsUserCalendarTab /> rendered by SettingsPage.razor at L127 (Gate: _activeTabId == 'calendar' && _visitedTabs.Contains('calendar')).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Pages/Settings/SettingsNotificationsTab.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <SettingsNotificationsTab /> rendered by SettingsPage.razor at L131 (Gate: _activeTabId == 'notifications' && _visitedTabs.Contains('notifications')).",
      "enabled": true
    }
]
```

## Parent: `BlazorEnterprise.Web/Components/Pages/Settings/SettingsTenantsTab.razor` ([docs/specs/SettingsTenantsTab.requirements.md](../../docs/specs/SettingsTenantsTab.requirements.md))

| Unregistered Component | Source Path | Line | Gate Condition |
| :--- | :--- | :--- | :--- |
| `<EntityHistoryButton />` | `BlazorEnterprise.Web/Components/Common/EntityHistoryButton.razor` | `L201` | `_managingUsersTenant is not null` |
| `<DataListSelect />` | `BlazorEnterprise.Web/Components/Common/DataListSelect.razor` | `L468` | `_showAddProgramForm` |

### Ready-to-Paste JSON for `SettingsTenantsTab.razor`
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

## Parent: `BlazorEnterprise.Web/Components/Pages/Settings/SettingsProjectsTab.razor` ([docs/specs/SettingsProjectsTab.requirements.md](../../docs/specs/SettingsProjectsTab.requirements.md))

| Unregistered Component | Source Path | Line | Gate Condition |
| :--- | :--- | :--- | :--- |
| `<EntityHistoryButton />` | `BlazorEnterprise.Web/Components/Common/EntityHistoryButton.razor` | `L182` | `context.Program is not null` |
| `<DataListSelect />` | `BlazorEnterprise.Web/Components/Common/DataListSelect.razor` | `L300` | `!_isEditingDetails` |

### Ready-to-Paste JSON for `SettingsProjectsTab.razor`
```json
"children": [
    {
      "path": "BlazorEnterprise.Web/Components/Common/EntityHistoryButton.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <EntityHistoryButton /> rendered by SettingsProjectsTab.razor at L182 (Gate: context.Program is not null).",
      "enabled": true
    },
    {
      "path": "BlazorEnterprise.Web/Components/Common/DataListSelect.razor",
      "category": "Settings UI",
      "thinking_level": "low",
      "description": "Child component <DataListSelect /> rendered by SettingsProjectsTab.razor at L300 (Gate: !_isEditingDetails).",
      "enabled": true
    }
]
```

