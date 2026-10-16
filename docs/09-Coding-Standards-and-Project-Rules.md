# 09. Coding Standards & Project Rules

> **Target Audience**: Human Developers, Tech Leads, Code Reviewers, and Architects contributing to this codebase.  
> **Key Objective**: Establish the authoritative coding standards, Clean Architecture laws, captive dependency rules, and modern C# 13 / .NET 10 conventions for the engineering team.
>
> *(Note: For AI assistant behavioral guardrails and operational rules, see [10. AI Agent Governance & Operational Rules](file:///c:/Users/cheri/source/repos/BlazorFluent/docs/10-AI-Agent-Governance-and-Operational-Rules.md). This document aligns with the root [`PROJECT_RULES.md`](file:///c:/Users/cheri/source/repos/BlazorFluent/PROJECT_RULES.md)).*

---

## 🏛️ 1. Clean Architecture Laws

1. **Strict Inward Dependency Flow**:
   - `BlazorFluent.Core` has **zero** internal project references. It contains only pure business contracts, entities, and enums.
   - `BlazorFluent.Persistence` references `Core` and `Shared`. It must never reference `Infrastructure` or UI projects.
   - `BlazorFluent.Infrastructure` handles external protocols (SMTP, Teams, SignalR, HTTP Engine Client) and must never reference `Persistence`.
   - `BlazorFluent.Jobs` contains all background queue listeners, batch handlers, and file import pipelines.
   - `BlazorFluent.Web` is the interactive Blazor UI composition root.
   - `BlazorFluent.Api` is the headless REST/minimal API processing engine.
   - `BlazorFluent.Worker` is a thin headless console host shell (.NET 10 Worker SDK). **Exactly zero batch handlers or business logic belong in `BlazorFluent.Worker`**; all job processing code resides in `BlazorFluent.Jobs`.

2. **Persistence Isolation**:
   - 100% of database access belongs in `BlazorFluent.Persistence`.
   - No direct `DbContext` queries inside UI components or Razor pages. Always call an injected domain service interface.

---

## 🔒 2. Service Scope Lifetime & Captive Dependency Governance

| Lifetime | Permitted Registrations | Critical Rules |
| :--- | :--- | :--- |
| **`Scoped`** | `AppDbContext`, domain services (`ITenantService`), EF interceptors, `ICurrentUser`. | Created once per Blazor circuit or HTTP request. **Never inject a Scoped service into a Singleton!** |
| **`Singleton`** | In-memory queues (`ChannelJobEventQueue`), time providers, stateless sanitizers, `ITenantTokenService`. | Must be strictly thread-safe. If a background Singleton needs `AppDbContext`, it **MUST** inject `IServiceScopeFactory` and create an explicit scope: `using var scope = _scopeFactory.CreateScope();`. |
| **`Transient`** | FluentValidation validators, formatting utilities, delegating handlers (`TenantAuthDelegatingHandler`). | Stateless and lightweight. |

### Fail-Fast Container Enforcement
In `Program.cs`, container validation is enabled across all environments (`ValidateScopes = true`, `ValidateOnBuild = true`). Any captive dependency causes an immediate build/startup exception.

---

## 🛡️ 3. Multi-Tenancy & Authorization Rules

1. **Fail-Closed Multi-Tenancy**:
   - All tenant-scoped entities must implement `ITenantScopedEntity`.
   - Never accept `TenantId` from untrusted client form inputs. The service layer must overwrite `entity.TenantId = currentUser.TenantId`.
   - Never bypass EF Core query filters without documented, audited justification.

2. **Dual-Gate Action Authorization**:
   - UI pages enforce visit/edit permissions via `AuthorizedPageComponentBase`.
   - In addition, domain services must call `EnsureCanEditAsync(projectId)` before executing mutations.

3. **Zero-Trust Multi-Tenancy Isolation & S2S Token Security**:
   - **Dual-Token Architecture**:
     - *Interactive Users*: Standard JWT bearer tokens authenticated via the centralized Cloud Master Key.
     - *Background/S2S Daemons*: Ephemeral JWT tokens (5-minute lifetime) authenticated via tenant-specific license keys (`Tenants:{tenantId}:LicenseKey`).
   - **Dynamic Key Resolution**: `BlazorFluent.Api` relies on `IssuerSigningKeyResolver` to dynamically fetch the tenant's symmetric key. Falling back to a shared system key on tenant mismatch is strictly prohibited.
   - **Opaque Reference Slugs & Data Privacy**:
     - Token claims MUST NOT expose raw database tenant IDs or user PII.
     - Use `OpaqueIdUtility.GenerateTenantSlug(tenantId)` for public `tid` claims (`tnt_...`) and `OpaqueIdUtility.MaskGuid` with per-tenant salt for sub/actor masks.
   - **Zero Shared Key Fallback**: If a tenant's license key is missing or unconfigured, the operation must immediately fail-closed with an `InvalidOperationException`.
   - **Cross-Tenant Quarantine Guards**: All background job handlers (`GenericFileImportBatchJobHandler`, `BatchJobQueueListener`) must compound-query by `(JobId, TenantId)` and immediately quarantine records if payload tenant IDs do not match the execution context.

---

## 💻 4. Modern C# 13 & .NET 10 Idioms

- **File-Scoped Namespaces**: Always use file-scoped namespaces (`namespace BlazorFluent.Core.Services;`).
- **Primary Constructors**: Use primary constructors for dependency injection:
  ```csharp
  public class CustomerService(AppDbContext db, ICurrentUser currentUser, ILogger<CustomerService> logger) : ICustomerService
  ```
- **Nullability**: Nullable reference types are enabled (`<Nullable>enable</Nullable>`). Avoid null-forgiving operators (`!`) unless operating on EF Core generated keys.
- **Pattern Matching & Expressions**: Prefer modern switch expressions and pattern matching for domain state transitions.
