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
   - `BlazorFluent.Infrastructure` handles external protocols (SMTP, Teams, SignalR) and must never reference `Persistence`.
   - `BlazorFluent.Web` is the composition root and presentation layer.

2. **Persistence Isolation**:
   - 100% of database access belongs in `BlazorFluent.Persistence`.
   - No direct `DbContext` queries inside UI components or Razor pages. Always call an injected domain service interface.

---

## 🔒 2. Service Scope Lifetime & Captive Dependency Governance

| Lifetime | Permitted Registrations | Critical Rules |
| :--- | :--- | :--- |
| **`Scoped`** | `AppDbContext`, domain services (`ITenantService`), EF interceptors, `ICurrentUser`. | Created once per Blazor circuit or HTTP request. **Never inject a Scoped service into a Singleton!** |
| **`Singleton`** | In-memory queues (`ChannelJobEventQueue`), time providers, stateless sanitizers. | Must be strictly thread-safe. If a background Singleton needs `AppDbContext`, it **MUST** inject `IServiceScopeFactory` and create an explicit scope: `using var scope = _scopeFactory.CreateScope();`. |
| **`Transient`** | FluentValidation validators, formatting utilities. | Stateless and lightweight. |

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

---

## 💻 4. Modern C# 13 & .NET 10 Idioms

- **File-Scoped Namespaces**: Always use file-scoped namespaces (`namespace BlazorFluent.Core.Services;`).
- **Primary Constructors**: Use primary constructors for dependency injection:
  ```csharp
  public class CustomerService(AppDbContext db, ICurrentUser currentUser, ILogger<CustomerService> logger) : ICustomerService
  ```
- **Nullability**: Nullable reference types are enabled (`<Nullable>enable</Nullable>`). Avoid null-forgiving operators (`!`) unless operating on EF Core generated keys.
- **Pattern Matching & Expressions**: Prefer modern switch expressions and pattern matching for domain state transitions.
