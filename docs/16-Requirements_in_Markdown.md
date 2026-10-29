Here is exactly how both scenarios will work so you never lose work and can drive code changes with minimal tokens:

---

### 1. What Happens When Code is Changed Externally & You Re-Run `Generate-Specs.ps1`?

It will **NOT** blindly wipe out your notes or requirements! Instead, the script uses a **Smart Merge & Hash-Guard**:

1. **Protected Blocks are 100% Preserved**:
   Every `.requirements.md` file has two protected zones wrapped in HTML comments:
   - `<!-- BEGIN PENDING CHANGES --> ... <!-- END PENDING CHANGES -->`
   - `<!-- BEGIN CUSTOM BUSINESS RULES --> ... <!-- END CUSTOM BUSINESS RULES -->`
   When you run `Generate-Specs.ps1`, the script extracts everything you wrote inside those blocks and injects it straight into the newly regenerated file.

2. **Inline `[CHANGE: ...]` / `[TODO: ...]` Extraction**:
   If you wrote an inline `[CHANGE: ...]` comment inside the UI Layout Tree or inside a Mermaid diagram, and someone re-runs `Generate-Specs.ps1` before the change was coded:
   - The script detects any lines containing `[CHANGE:` or `[TODO:` in the auto-generated sections, rescues them, and automatically moves them into your `<!-- BEGIN PENDING CHANGES -->` box at the top so they are never lost!
   - It also saves a `.bak` snapshot if uncommitted edits were detected outside the protected zones.

3. **Auto-Generated Sections Refresh Cleanly**:
   - The **Line-Number Quick-Jump Map**, **UI Component Tree**, **Page Load Mermaid Diagram**, and **Action Call-Chain Mermaid Diagrams** are refreshed to match the latest C# and Razor code.

---


### Step 1: How to Write Your Changes (Plain English Only)
Open the target spec file (e.g., [SettingsTenantsTab.requirements.md](file:///c:/Users/cheri/source/repos/BlazorFluent/docs/specs/SettingsTenantsTab.requirements.md#L16-L20)) and write normal, everyday English sentences inside `## 1. What I Want Changed (Write in Plain English)` between `<!-- BEGIN PENDING CHANGES -->` and `<!-- END PENDING CHANGES -->`:

```markdown
## 1. What I Want Changed (Write in Plain English)
<!-- Write your requirements in plain English below (no line numbers or code syntax needed), then prompt: Implement @[docs/specs/SettingsTenantsTab.requirements.md] -->
<!-- BEGIN PENDING CHANGES -->
1. Remove Programs, Users, Tasks & Risks, and Dates buttons from the tenant row in the Tenant Directory table.
2. When clicking a tenant name in the table, open a popup showing all tenant details in edit mode.
3. Inside the popup:
   - Keep Slug, Name, and Domains read-only, and allow editing Start Date and End Date.
   - Include tabs for Users, Programs, and Tasks & Risks inside the popup instead of separate cards on the page.
   - Add Save and Close buttons at the bottom of the popup.
<!-- END PENDING CHANGES -->
```
*(You can glance at `## 2. Current Page Overview (Plain-English Summary)` right below it to see what buttons/fields currently exist on the page without reading any technical code).*

---

### Step 2: How to Tell the Agent to Apply Changes
In the Antigravity chat box, send a single one-line prompt referencing the file:

> `Implement @[docs/specs/SettingsTenantsTab.requirements.md]`

When you send this:
1. Antigravity reads your plain-English points in **Section 1**.
2. Antigravity expands the hidden `<details>` **Agent Code Index** at the bottom of the file to get the exact line numbers (`L751-L828`), `@bind` variables, C# handlers, and service methods, and applies the edits to `.razor` and `.razor.cs`.

---

### Step 3: What Happens After Changes Are Done & How to See History by Date
Every `.requirements.md` file now has two built-in mechanisms for automatic archival and regeneration:

1. **`agent_completion_protocol` in the `[SYSTEM / AGENT RUNTIME]` Header** ([SettingsTenantsTab.requirements.md](file:///c:/Users/cheri/source/repos/BlazorFluent/docs/specs/SettingsTenantsTab.requirements.md#L7)):
   - Instructs the agent that as soon as it finishes editing your source files, it must run:
     ```powershell
     pwsh -NoProfile -File scripts/Generate-Specs.ps1 -TargetFilter 'SettingsTenantsTab' -ArchiveCompleted
     ```
2. **Automatic Date-Stamped Archival (`-ArchiveCompleted`)** ([Generate-Specs.ps1](file:///c:/Users/cheri/source/repos/BlazorFluent/scripts/Generate-Specs.ps1#L653-L686)):
   - **Moves Completed Items to `Change History` by Date/Time**: Moves your items out of `## 1. What I Want Changed` into the collapsible **`Change History (Completed Requirements by Date)`** section ([SettingsTenantsTab.requirements.md](file:///c:/Users/cheri/source/repos/BlazorFluent/docs/specs/SettingsTenantsTab.requirements.md#L73-L79)), stamped with the exact date and time (`#### YYYY-MM-DD HH:mm`) and checked off (`- [x]`):
     ```markdown
     <details>
     <summary><b>Change History (Completed Requirements by Date)</b></summary>

     <!-- BEGIN CHANGE HISTORY -->
     #### 2026-10-29 10:55
     - [x] Remove Programs, Users, Tasks & Risks, and Dates buttons from the tenant row in the Tenant Directory table.
     - [x] When clicking a tenant name in the table, open a popup showing all tenant details in edit mode.
     - [x] Inside the popup: Keep Slug, Name, and Domains read-only, and allow editing Start Date and End Date...
     <!-- END CHANGE HISTORY -->
     </details>
     ```
   - **Resets `## 1. What I Want Changed`**: Clears Section 1 back to the empty placeholder so it is ready for your next round of requirements.
   - **Refreshes the Spec**: Automatically updates `## 2. Current Page Overview` and recalculates all line numbers in the `Agent Code Index` to match the newly modified `.razor` and `.razor.cs` files, while preserving your `Change History` and `Permanent Rules` across all future regenerations.

---
### How Your End-to-End Workflow Works Now
1. **You Write Plain English** in `## 1. What I Want Changed (Write in Plain English)`.
2. **You Prompt**: `Implement @[docs/specs/SettingsTenantsTab.requirements.md]`
3. **Phase 1 — Focused Planning (0% Wasted Search Tokens)**:
   - The AI uses the **Agent Code Index** inside the spec file to immediately know every line number, `@bind` field, button handler, and service call without reading full files.
   - It uses its thinking budget purely to design your UI/logic changes, outputs a concise **Execution Plan** (mapping your plain-English items to the exact lines and methods it will change), and **stops for your review**.
4. **Phase 2 — Approve, Edit & Auto-Archive by Date**:
   - Once you reply **`yes`** (or click **Proceed**), the AI applies the surgical code edits and automatically runs:
     ```powershell
     pwsh -NoProfile -File scripts/Generate-Specs.ps1 -TargetFilter 'SettingsTenantsTab' -ArchiveCompleted
     ```
   - Your completed requirements are moved into **`Change History (Completed Requirements by Date)`** under `#### YYYY-MM-DD HH:mm`, `## 1. What I Want Changed` is reset for your next task, and the spec's Page Overview and line numbers are refreshed automatically.