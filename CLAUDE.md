# Claude Instructions - BlazorFluent Enterprise Starter Kit (.NET 10, C# 13, Fluent UI V5)

## 🏛️ 1. Architecture & Dependency Laws
Adhere to the authoritative standards in `PROJECT_RULES.md` and `docs/09-Coding-Standards-and-Project-Rules.md`:
- **Strict Inward Flow**: `Core` -> `Persistence`/`Infrastructure`/`Jobs` -> `Web`/`Worker`/`Api`.
- **Database Encapsulation**: 100% of database access belongs in `BlazorFluent.Persistence`. Never query `AppDbContext` directly inside Razor pages or UI components.
- **Worker Host Isolation**: `BlazorFluent.Worker` is a thin console host shell (.NET 10 Worker SDK). Exactly zero batch job implementations belong in `BlazorFluent.Worker`; all job handlers reside in `BlazorFluent.Jobs`.
- **Zero-Trust Multi-Tenancy**: All tenant entities implement `ITenantScopedEntity`. Fallback to shared keys or default tenant slugs is prohibited.
- **Enterprise Strict CSP**: Zero `'unsafe-inline'` and zero `'unsafe-eval'` in scripts. All script/style elements must use per-request cryptographic nonces (`nonce="@nonce"`).

## 📖 2. Developer Recipes (Single Source of Truth)
Before adding entities, services, pages, or jobs, read the step-by-step code skeletons in `docs/07-How-To-Extend-and-Developer-Recipes.md`:
- **Recipe 1**: Add a New Domain Entity & EF Core Mapping
- **Recipe 2**: Add an Authorized Domain Service
- **Recipe 3**: Author a Fluent UI V5 Role-Gated Page (`AuthorizedPageComponentBase`)
- **Recipe 4**: Add a Batch File Import Target (Hybrid Batch Framework)
- **Recipe 5**: Author a Multi-Step Wizard with `<FluentWizardModal>`
- **Recipe 6**: Add a Calculation Rule to `BlazorFluent.RulesEngine`
- **Recipe 7**: Downstream Extension with TemplateGuard (`.Custom.cs`)
- **Recipe 8**: Onboard a New Tenant & Rotate License Keys
- **Recipe 9**: Author a Dedicated Background Job for `BlazorFluent.Worker`

## 🛡️ 3. Operational Guardrails
- **Git Safety**: Never execute `git add`, `git commit`, `git reset`, or `git restore`. Leave all changes unstaged in the working tree for the developer.
- **Build Discipline**: Do NOT proactively execute `dotnet build` or `dotnet run` unless explicitly requested by the user.
- **Fluent UI Blazor V5**: Adhere to Fluent UI Blazor V5 component syntax (`FluentSelect<TOption, TValue>`, `FluentTextInput`, etc.).
