# Component Registration Warnings (Validation Report)

> **Last Generated**: `2026-10-30 09:36:11 -04:00`  
> **Manifest**: [specs-manifest.json](../specs-manifest.json) | **Catalog**: [README.md](../README.md)

> [!CAUTION]
> **2 Unregistered Child Component(s) detected across 1 parent component(s)!** Copy desired entries below into `"children"` of the respective parent in `docs/specs/specs-manifest.json` (or add shared UI widgets to `component_discovery.ignore_components`).

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

