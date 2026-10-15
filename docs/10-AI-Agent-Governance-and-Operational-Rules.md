# 10. AI Agent Governance & Operational Rules

> **Target Audience**: AI Coding Assistants (Google Antigravity, GitHub Copilot, Cursor, Claude, etc.) and Prompt Engineers configuring AI developer agents.  
> **Key Objective**: Establish immutable behavioral boundaries, command execution safety constraints, authority hierarchies, and plan-first protocols for autonomous or semi-autonomous AI coding assistants.
>
> *(Note: For human software engineering team standards, see [09. Coding Standards & Project Rules](file:///c:/Users/cheri/source/repos/BlazorFluent/docs/09-Coding-Standards-and-Project-Rules.md) and [`PROJECT_RULES.md`](file:///c:/Users/cheri/source/repos/BlazorFluent/PROJECT_RULES.md)).*

---

## 🚫 1. Immutable Execution Boundaries (The Cardinal Rules)

Every AI coding assistant operating within this repository must adhere to the following strict constraints:

### Rule 1: NEVER Run Build, Run, or Test Commands Unprompted
- **STRICT PROHIBITION**: The assistant MUST NEVER execute `dotnet build`, `dotnet run`, `dotnet test`, or `dotnet ef database update` without asking the user beforehand and obtaining explicit permission.
- **Rationale**: Long-running background processes, file locking, and compilation storms interfere with the developer's active Visual Studio debug sessions.
- **Required Behavior**: Provide the exact terminal commands for the user to copy and run, or ask: *"May I run `dotnet build` now to verify compilation?"*

### Rule 2: NEVER Execute Git Commands
- **STRICT PROHIBITION**: The assistant MUST NEVER execute `git add`, `git commit`, `git push`, `git checkout`, `git reset`, or `git status`.
- **Rationale**: Git staging, branching, and commit history are strictly reserved for human engineering discretion.

### Rule 3: Maintain Plan-First Discipline (`/plan` Mode)
- When invoked with `/plan` (or when tasked with architectural, multi-project, or structural changes):
  1. Thoroughly research the codebase first.
  2. Create a detailed implementation plan artifact (`<plan_name>.md`) with `RequestFeedback: true` and `UserFacing: true`.
  3. Await explicit user approval before modifying code or creating non-artifact files.

---

## 🏛️ 2. Architectural Guardrails for AI Agents

1. **Adhere to the 11-Project Solution Topology**:
   - Never introduce circular dependencies.
   - Never place UI components inside `BlazorFluent.Core`, `Persistence`, or `Infrastructure`.
   - Never inject `DbContext` into Razor components directly; always route through domain contracts in `BlazorFluent.Core`.

2. **Fluent UI Blazor V5 Compliance**:
   - The assistant must strictly author **Microsoft Fluent UI Blazor V5** syntax.
   - Forbidden legacy tags: `<FluentTextField>`, `<FluentNumberField>`, `<FluentTextArea>`.
   - Forbidden legacy attributes: `Appearance="Appearance.Accent"`, `Appearance="Appearance.Neutral"`.
   - Always run or recommend `pwsh scripts/verify-fluent-v5.ps1`.

3. **TemplateGuard Protection Compliance**:
   - In template authoring mode (`TemplateAuthoring`), base files can be updated, but downstream custom extensions must always be authored as `.Custom.cs` partial classes.
   - Never delete or corrupt `tools/TemplateGuard/.basefiles.json`.

---

## 📜 3. Code Modification & Documentation Integrity

1. **Preserve Comments & Docstrings**: Preserve all existing comments, copyright notices, and docstrings that are unrelated to the immediate edit.
2. **Clickable File Links**: When referencing files, code symbols, classes, or interfaces in conversation or artifacts, always provide clickable markdown links formatted with forward slashes (e.g. `[CustomerService.cs](file:///c:/Users/cheri/source/repos/BlazorFluent/src/BlazorFluent.Persistence/Services/CustomerService.cs)`).
3. **Artifact Guidelines**:
   - Keep answers concise.
   - Point the user to the generated artifact and highlight only open questions or decisions.
