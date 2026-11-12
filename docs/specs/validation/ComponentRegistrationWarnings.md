# Component Registration Warnings (Validation Report)

> **Last Generated**: `2026-11-12 13:22:38 -05:00`  
> **Manifest**: [specs-manifest.json](../specs-manifest.json) | **Catalog**: [README.md](../README.md)

> [!CAUTION]
> **6 Unregistered Child Component(s) detected across 3 parent component(s)!** Copy desired entries below into `"children"` of the respective parent in `docs/specs/specs-manifest.json` (or add shared UI widgets to `component_discovery.ignore_components`).

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

