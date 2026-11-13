# Gemini / Antigravity Rules — Pointer to Single Source of Truth

> **CRITICAL RULE FOR GEMINI / ANTIGRAVITY**:
> The authoritative Single Source of Truth for this entire solution is [`AGENTS.md`](AGENTS.md).
> **If you or the user updates, adds, or changes coding rules, conventions, or constraints, ALWAYS APPLY THE EDITS DIRECTLY TO [`AGENTS.md`](AGENTS.md)** so that all teammates using Cursor, Claude, or GitHub Copilot stay 100% in sync.
> For exhaustive deep-dive architectural specifications, consult [`PROJECT_RULES.md`](PROJECT_RULES.md) and [`docs/07-How-To-Extend-and-Developer-Recipes.md`](docs/07-How-To-Extend-and-Developer-Recipes.md).

---

## 🏛️ Summary of Governing Standards in `AGENTS.md`
1. **Operational Guardrails**: Direct implementation on spec requests without stalling unless `/plan` was requested. Zero proactive `dotnet build`, `dotnet test`, `dotnet run`, or `git` commands without explicit user permission.
2. **Mandatory Observability & Correlation IDs**: `ICorrelationContext.CorrelationId` is the universal incident reference ID on screen (`"Unable to save changes... Reference ID: {CorrelationId}"`). Zero sensitive error leakage. Zero swallowed exceptions. Mandatory structured logger injection.
3. **Clean Architecture & Base Template Preservation**: Core base classes (`BaseEntity`, `AuditableEntity`, `TenantAuditableEntity`, `Csk*`) are protected. Always extend using `.Custom.cs` partial classes.
4. **Roslyn Analyzer Laws (`TG006`–`TG027`)**: `*Enum` and `*Entity` suffixes, lifecycle markers, companion validators and EF configs, UTC `DateTime` (`DateOnly` strictly prohibited), 3NF normalization, `TG027` zero hardcoding in models/DTOs/records.
5. **Blazor Server, EF Core & Concurrency**: Never inject `AppDbContext` directly; always use `IDbContextFactory<AppDbContext>`. Sequential awaits (no `Task.WhenAll`/`Parallel.*`). $O(1)$ batch queries with `GroupBy`. Split queries on multiple collections.
6. **Native Material Design 3 (`Csk*`) Exclusivity**: Use native `Csk*` components from `BlazorEnterprise.Shared.UI` exclusively (never Fluent UI V5 or MudBlazor). Strict CSP (zero inline scripts, styles, or eval).
7. **Skills Synchronization**: Canonical skills live in `.gemini/skills/` and are mirrored across `.cursor/skills/`, `.claude/skills/`, and `.github/skills/`. Run `pwsh scripts/sync-ai-skills.ps1` to re-sync.
