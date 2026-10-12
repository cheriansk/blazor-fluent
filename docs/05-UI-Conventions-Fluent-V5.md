# 05. UI Conventions & Fluent UI Blazor V5 Guide

> **Purpose**: Definitive standards guide for Razor file organization, component naming taxonomy, reusable component library, and Microsoft Fluent UI Blazor V5 compliance.  
> **Who should read this**: Frontend and full-stack developers authoring Razor components, styling pages, or migrating legacy controls.

---

## 🏷️ 1. Mandatory Razor Component Naming Taxonomy

To maintain clarity across hundreds of components and eliminate naming collisions, all `.razor` files must strictly follow this **4-tier naming taxonomy**:

| Suffix | Architectural Scope & Role | Example File | How it is Rendered / Consumed |
|---|---|---|---|
| **`*Page.razor`** | **Routable Screen**: Any file declaring an `@page` directive at the top. | `WorkflowsDashboardPage.razor`, `ProjectTasksBoardPage.razor` | Loaded automatically by Blazor Router |
| **`*Comp.razor`** | **Embedded Component**: Reusable component embedded inside pages or layouts. | `WorkflowStepperComp.razor`, `PageHeaderComp.razor` | Embedded via tag: `<WorkflowStepperComp />` |
| **`*Modal.razor`** | **Modal Dialog / Backdrop**: Standalone modal dialogs with backdrop overlays. | `CreateWorkflowWizardModal.razor`, `ConfirmDialogModal.razor` | Displayed via state: `<CreateWorkflowWizardModal IsOpen="..." />` |
| **`*Popup.razor`** | **Interactive Pop-up / Flyout**: Floating popover, dropdown, or header flyout. | `NotificationBellPopup.razor` | Activated via toggle: `<NotificationBellPopup />` |
| **`*Layout.razor`** | **Structural Layout**: Master layout wrappers providing navigation shells. | `MainLayout.razor`, `AdminLayout.razor` | Specified via directive: `@layout AdminLayout` |

### Co-Location Rules: Code-Behind & Scoped CSS
If extracting C# logic or scoped styles, files must match the exact naming convention:
- **Code-Behind**: `*Page.razor.cs`, `*Comp.razor.cs`, `*Modal.razor.cs`
- **Scoped CSS**: `*Page.razor.css`, `*Comp.razor.css`, `*Modal.razor.css`

---

## 📦 2. Reusable Component Catalog

Located in `src/BlazorFluent.Web/Components/Common/` and `Components/Layout/`:

| Component Name | File Path | Key Parameters | Architectural Purpose |
|---|---|---|---|
| `<PageHeaderComp>` | `Components/Common/PageHeaderComp.razor` | `Title`, `Subtitle`, `ChildContent` | Standardized header bar with typography tokens and action slot. |
| `<ConfirmDialogModal>` | `Components/Common/ConfirmDialogModal.razor` | `IsOpen`, `Title`, `Message`, `OnConfirmed`, `OnCancelled` | Accessible confirmation dialog for destructive mutations (e.g. Delete, Kill Session). |
| `<EmptyStateComp>` | `Components/Common/EmptyStateComp.razor` | `Title`, `Description`, `Icon`, `ActionTemplate` | Consistent centered placeholder indicator for empty tables and search feeds. |
| `<FluentSwimlaneBoardComp<TItem>>` | `Components/Common/FluentSwimlaneBoardComp.razor` | `Columns`, `Items`, `CardTemplate`, `OnItemClick` | Generic multi-column kanban board with badge counts and card rendering. |
| `<WorkflowStepperComp>` | `Components/Common/WorkflowStepperComp.razor` | `Workflow`, `OnWorkflowUpdated`, `OnEditWorkflow` | Stage-gated sequential pipeline card rendering active steps, subtasks, and action buttons. |
| `<UserDateTime>` | `Components/Common/UserDateTime.razor` | `Value` (`DateTime?`), `Format`, `ShowTimezone`, `NullText` | Timezone-aware date/time renderer formatting UTC timestamps according to user preference. |
| `<AppErrorBoundaryComp>` | `Components/Common/AppErrorBoundaryComp.razor` | `ChildContent` | User-facing circuit error boundary catching rendering crashes and displaying `ERR-XXXXXXXX` incident IDs. |
| `<SessionRevokedModal>` | `Components/Common/SessionRevokedModal.razor` | *(Internal SignalR state)* | Watchdog modal that freezes the client circuit upon administrative revocation. |
| `<NotificationBellPopup>` | `Components/Layout/NotificationBellPopup.razor` | `CurrentUserId` | Header pop-up flyout displaying real-time categorized notification alerts. |
| `<TenantSwitcherComp>` | `Components/Layout/TenantSwitcherComp.razor` | *(ITenantContext)* | Header dropdown permitting authorized company users to switch active tenant context. |

---

## 🚀 3. Microsoft Fluent UI Blazor V5 Standards

BlazorFluent is built 100% on **Microsoft Fluent UI Blazor V5**. All legacy V4 controls and deprecated parameters are rejected at compile time via `<WarningsAsErrors>CS0618</WarningsAsErrors>`.

### Key V5 Principles:
1. **Strongly-Typed Enum Parameters**: Use `ButtonAppearance.Primary`, `BadgeColor.Brand`, and `MessageBarIntent.Error`. Never pass arbitrary string values for appearances.
2. **Dual-Generic Selects**: `FluentSelect<TOption, TValue>` requires **both** type arguments (e.g. `TOption="TaskLevel" TValue="TaskLevel"`).
3. **Native Fluent Controls Only**: Raw HTML elements (`<select>`, raw checkboxes, raw buttons) are forbidden when native Fluent UI components exist (`<FluentSelect>`, `<FluentCheckbox>`, `<FluentButton>`).
4. **Automated Verification Script**: All pull requests must pass `./scripts/verify-fluent-v5.ps1`.

---

## ⚠️ 4. V5 Pitfalls & Migration Anti-Patterns Table

Below are the most common compilation and runtime mistakes encountered when working with Fluent UI Blazor V5, along with their verified solutions:

| Anti-Pattern / V4 Legacy (❌ WRONG) | V5 Compliant Pattern (✅ CORRECT) | Why / Root Cause |
|---|---|---|
| `<FluentSelect Items="@list" @bind-SelectedOption="val" />` | `<FluentSelect TOption="T" TValue="string" Items="@list" @bind-Value="val" />` | `SelectedOption` and `SelectedOptionChanged` are removed in V5. Use `Value` and `ValueChanged`. |
| `<FluentOption Value="TaskLevel.Tenant">` | `<FluentOption Value="@TaskLevel.Tenant">` | In generic `FluentOption<TValue>`, omitting `@` passes a literal string `"TaskLevel.Tenant"`, causing a type mismatch. Prepending `@` evaluates the C# enum expression. |
| `<FluentSpinner Size="16" />` | `<FluentSpinner Size="@SpinnerSize.Small" />` | In V5, `Size` accepts `SpinnerSize?` enum (`Tiny`, `Small`, `Medium`, `Large`), not raw integer strings. |
| `<UserDateTime UtcDateTime="@date" ShowTime="false" />` | `<UserDateTime Value="@date" Format="yyyy-MM-dd" />` | The `UserDateTime` component parameter signature is `Value` (`DateTime?`) and `Format` (`string`). `UtcDateTime` does not exist. |
| `<FluentProgressRing ... />` | `<FluentSpinner Size="@SpinnerSize.Medium" />` | `FluentProgressRing` is renamed to `FluentSpinner` in V5. |
| `<FluentTextField ... />` | `<FluentTextInput ... />` | `FluentTextField` was removed in V5; use `FluentTextInput` or `FluentTextArea`. |
| `<FluentSearch ... />` | `<FluentTextInput Placeholder="Search..."><StartTemplate><FluentIcon Value="@(new Icons.Regular.Size16.Search())" /></StartTemplate></FluentTextInput>` | `FluentSearch` is replaced by `FluentTextInput` with a search icon in `StartTemplate`. |
| `<FluentDialogProvider />` | `<FluentProviders />` | `FluentDialogProvider` is replaced by the unified `<FluentProviders />` umbrella component in `App.razor`. |
| Ambiguous `TaskStatus` error | `@using TaskStatus = BlazorFluent.Core.DataListTypes.TaskStatus` | .NET global usings implicitly include `System.Threading.Tasks.TaskStatus`. Add this alias to `_Imports.razor`. |

---

## 🔍 5. Verification Script (`verify-fluent-v5.ps1`)

The repository includes an automated scanner in `scripts/verify-fluent-v5.ps1` that scans all `.razor` and `.cs` files for deprecated tokens:

```powershell
pwsh -ExecutionPolicy Bypass -File .\scripts\verify-fluent-v5.ps1
```

### RegEx Rules Scanned:
- `\bSelectedOption\b` and `\bSelectedOptionChanged\b`
- `\bFluentProgressRing\b`
- `\bFluentTextField\b` and `\bFluentNumberField\b`
- `\bFluentSearch\b`
- `\bFluentAnchor\b`
- `\bFluentDialogProvider\b`
- `\bFluentSelect\b(?![^>]*\bTValue\b)` (missing dual generics)

---

## 🔗 Related Documentation

- [00. Overview & Glossary](00-Overview-and-Glossary.md) — Personas, routes, and domain glossary.
- [01. Getting Started Guide](01-Getting-Started.md) — Local setup, migrations, and troubleshooting.
- [02. Architecture Deep Dive](02-Architecture.md) — Layer boundaries, DI rules, and caching topology.
- [03. Security Model](03-Security-Model.md) — Multi-tenant write barriers and role gates.
- [04. Task Hierarchy & Workflows](04-Task-Hierarchy-and-Workflows.md) — Workflow orchestration and wizard models.
- [06. How to Extend Recipes](06-How-To-Extend.md) — Step-by-step feature development recipes.
- [07. AI Agent Operating Rules](07-AI-Agent-Rules.md) — Operational guidelines for AI coding assistants.
