# Contributing

Thanks for helping improve BlazorFluent. This project is a template, so changes should keep it **clean, secure and easy to build on**.

## Before you start
1. Read [docs/00-Overview-and-Glossary.md](docs/00-Overview-and-Glossary.md) and [docs/02-Architecture.md](docs/02-Architecture.md).
2. Read [PROJECT_RULES.md](PROJECT_RULES.md): the canonical rules for architecture, tenancy, security, EF Core and UI.
3. Set up locally with [docs/01-Getting-Started.md](docs/01-Getting-Started.md).

## Workflow
1. Fork and create a branch (`feature/<short-name>` or `fix/<short-name>`).
2. Make focused changes. Follow the recipes in [docs/06-How-To-Extend.md](docs/06-How-To-Extend.md).
3. Run the Fluent UI V5 compliance check before opening a PR:
   ```powershell
   pwsh -ExecutionPolicy Bypass -File ./scripts/verify-fluent-v5.ps1
   ```
4. Build and run the app yourself and exercise the feature you changed.
5. Open a pull request and complete the checklist below.

## Pull request checklist
- [ ] **Architecture:** layer boundaries respected (Core ← Persistence / Infrastructure ← Jobs ← Web); contracts live in `Core/Contracts`.
- [ ] **Tenancy:** every new entity is tenant-scoped (`ITenantEntity` / `TenantAuditableEntity`) or explicitly `IGlobalEntity`.
- [ ] **Naming:** entity classes end in `Entity`; shared enums live in `Core/DataListTypes` with `[Display]` metadata; Razor files follow `*Page / *Comp / *Modal / *Popup / *Layout`.
- [ ] **Security:** server-side authorization on every read and write path (not only `[Authorize]` on the page); user text sanitized; no raw SQL with interpolation; no secrets committed.
- [ ] **EF Core:** Fluent API configuration only; `.AsTracking()` only where entities are mutated; no N+1 queries; bulk work uses `ExecuteUpdate/Delete`.
- [ ] **UI:** Fluent UI Blazor **V5** components only (see [docs/05-UI-Conventions-Fluent-V5.md](docs/05-UI-Conventions-Fluent-V5.md)); `verify-fluent-v5.ps1` passes.
- [ ] **Errors and logging:** no stack traces shown to users; structured log templates (no string interpolation).
- [ ] **Docs:** behaviour or design changes are reflected in `docs/`.

## Commit messages
Use short imperative subjects with an optional scope:

```
feat(workflows): add admin-only workflow designer
fix(ui): migrate FluentSelect to Value/ValueChanged
docs: update security model
```

## Reporting security issues
Please do **not** open a public issue for a suspected vulnerability. Contact the maintainers privately (use GitHub's *Report a vulnerability* feature on the repository's Security tab).

## AI-assisted contributions
If you use an AI coding assistant, follow [docs/07-AI-Agent-Rules.md](docs/07-AI-Agent-Rules.md). You remain responsible for reviewing everything submitted.

## License
By contributing you agree your contributions are licensed under the [MIT License](LICENSE).
