# GitHub Copilot Instructions - BlazorFluent Starter Kit (.NET 10, C# 13, Fluent UI V5)

## 🏛️ 1. Architecture & Solution Topology (12 Projects)
Always adhere to the authoritative rules in `PROJECT_RULES.md` and `docs/09-Coding-Standards-and-Project-Rules.md`:
- **Clean Architecture Inward Flow**:
  * `BlazorFluent.Core`: Pure domain entities and contracts. Zero internal dependencies.
  * `BlazorFluent.Persistence`: 100% of EF Core database queries, migrations, and scoped service implementations.
  * `BlazorFluent.Infrastructure`: External protocols (SMTP, Teams, S2S Token Service, HTTP Engine client).
  * `BlazorFluent.Jobs`: All background queue listeners, batch handlers, and file import pipelines.
  * `BlazorFluent.Worker`: Thin console host shell (.NET 10 Worker SDK). Zero batch job implementations belong in Worker; all jobs reside in `BlazorFluent.Jobs`.
  * `BlazorFluent.Web`: Interactive Blazor Server composition root. Direct DbContext queries in Razor components are prohibited.
- **Fail-Closed Multi-Tenancy**: All tenant entities implement `ITenantScopedEntity`. Never bypass EF Core query filters.
- **Strict CSP**: Zero `'unsafe-inline'` or `'unsafe-eval'` in scripts. All script/style elements must use per-request cryptographic nonces (`nonce="@nonce"`).

## 📖 2. Developer Recipes (Single Source of Truth)
Always reference the verified code skeletons in `docs/07-How-To-Extend-and-Developer-Recipes.md` when assisting the user:
- **Recipe 1**: Add a New Domain Entity & EF Core Mapping (`Core` -> `Persistence`)
- **Recipe 2**: Add an Authorized Domain Service (`Core` -> `Persistence`)
- **Recipe 3**: Author a Fluent UI V5 Role-Gated Page (`AuthorizedPageComponentBase`)
- **Recipe 4**: Add a Batch File Import Target (`BlazorFluent.Jobs`)
- **Recipe 5**: Author a Multi-Step Wizard with `<FluentWizardModal>`
- **Recipe 6**: Add a Calculation Rule to `BlazorFluent.RulesEngine`
- **Recipe 7**: Downstream Extension with TemplateGuard (`.Custom.cs`)
- **Recipe 8**: Onboard a New Tenant & Rotate License Keys
- **Recipe 9**: Author a Dedicated Background Job for `BlazorFluent.Worker`

## 🛡️ 3. Operational Safety
- Never run git staging or committing commands (`git add`, `git commit`, `git reset`, `git restore`).
- Never run proactive builds (`dotnet build`, `dotnet run`) unless explicitly instructed by the user.
- Use Fluent UI Blazor V5 component syntax (`FluentSelect<TOption, TValue>`, `FluentTextInput`, etc.).
