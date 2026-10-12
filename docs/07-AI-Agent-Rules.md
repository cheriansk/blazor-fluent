# AI Agent Rules

Operating rules for AI coding assistants (and the humans who supervise them) working in this repository.

**Who should read this:** anyone using an AI assistant on this codebase; maintainers reviewing AI-authored changes.

The canonical rule set is [PROJECT_RULES.md](../PROJECT_RULES.md). This page is the short, assistant-facing summary of the constraints most often violated.

## Hard constraints

| Rule | Why |
|---|---|
| **Never run `dotnet build`, `dotnet compile` or `dotnet run`.** The developer builds and runs. | Keeps the developer in control of compile/test cycles. |
| **Never stage or commit** (`git add`, `git reset`, `git restore`, `git commit`). Leave every change unstaged (` M` / `??`). | The developer reviews diffs and decides what to stage. |
| **Consult the `fluent-ui-blazor` MCP server before using or changing any Fluent component** (`search_components`, `get_component_details`, `get_component_enums`, `get_component_migration`). Never guess parameter names. | V5 differs from V4 in many parameter names and generics. |
| **Fluent UI Blazor V5 only.** Run `scripts/verify-fluent-v5.ps1` after UI changes. | Legacy V4 tokens are rejected by the repo. |
| **Preserve existing comments and docstrings** that are unrelated to your change. | Avoids noisy diffs and lost context. |

## Architecture guardrails
- Respect layer boundaries (Core ← Persistence / Infrastructure ← Jobs ← Web). New service contracts go in `Core/Contracts`.
- Every entity is tenant-scoped or explicitly `IGlobalEntity`. Never bypass query filters except in audited host-only code.
- Authorization is enforced in services, not only on pages.
- Use `.AsTracking()` only for queries whose entities you will modify (queries are no-tracking by default).
- Never put real secrets, emails or tenant data in code, docs or samples. Use placeholders.

## Verify before you claim
1. Search the code for every class, route and enum value you reference.
2. Prefer the current code over older plans or docs when they disagree.
3. State clearly what you could not verify (you cannot build).

## Common V5 mistakes to avoid
See the pitfalls table in [05-UI-Conventions-Fluent-V5.md](05-UI-Conventions-Fluent-V5.md): `FluentSelect` needs both `TOption` and `TValue` and binds with `Value` / `ValueChanged`; enum values on `FluentOption` need `@`; `FluentSpinner` takes `SpinnerSize`; `UserDateTime` takes `Value` and `Format`.

## Related docs
[Overview](00-Overview-and-Glossary.md) · [Getting started](01-Getting-Started.md) · [Architecture](02-Architecture.md) · [Security model](03-Security-Model.md) · [Workflows](04-Task-Hierarchy-and-Workflows.md) · [UI conventions](05-UI-Conventions-Fluent-V5.md) · [How to extend](06-How-To-Extend.md)
