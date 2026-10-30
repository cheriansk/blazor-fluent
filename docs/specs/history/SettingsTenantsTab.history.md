# Completed Change History: `SettingsTenantsTab`

> **Active Spec**: [SettingsTenantsTab.requirements.md](../SettingsTenantsTab.requirements.md) | **Navigation Index**: [SettingsTenantsTab.index.md](../index/SettingsTenantsTab.index.md)

<!-- BEGIN CHANGE HISTORY -->
### 2026-10-29 17:22
- [x] [UI] Make tenant Status read-only and replace table action buttons with compact View/Edit icon buttons:
  - Status column must always render as a non-editable badge (Active/Inactive), not a toggle switch.
  - Remove the text action buttons (`Programs`, `Users`, `Tasks & Risks`, `Dates`).
  - Add a View icon button (`CskIcons.Eye`) visible to all authorized viewers.
  - Add an Edit icon button (`CskIcons.Edit`) visible only to administrators (`IsRootAdmin || _canEditTenants`).
- [x] [UI] Implement a separate modal window for View / Edit Tenant Details:
  - When View icon is clicked, open a **separate modal window** (not inline) displaying full tenant details (Slug, Name, Status, Internal/External Domains, Start Date, End Date) in read-only mode with a "Close" button.
  - When Edit icon is clicked, open the **same modal window** with Start Date and End Date as editable date pickers, with "Save Dates" and "Cancel" buttons. All other fields (Slug, Name, Status, Domains) remain permanent and locked.
  - The modal must overlay the page and support close via the "Close"/"Cancel" button or escape key.
<!-- END CHANGE HISTORY -->
