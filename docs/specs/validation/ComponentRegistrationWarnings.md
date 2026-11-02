# Component Registration Warnings (Validation Report)

> **Last Generated**: `2026-11-01 20:49:02 -05:00`  
> **Manifest**: [specs-manifest.json](../specs-manifest.json) | **Catalog**: [README.md](../README.md)

> [!CAUTION]
> **4 Unregistered Child Component(s) detected across 1 parent component(s)!** Copy desired entries below into `"children"` of the respective parent in `docs/specs/specs-manifest.json` (or add shared UI widgets to `component_discovery.ignore_components`).

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

