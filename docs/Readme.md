# BlazorFluent Documentation Portal

Welcome to the **BlazorFluent .NET 10 Starter Kit** documentation suite.

BlazorFluent is a production-grade, enterprise-ready multi-tenant SaaS starter kit built on **.NET 10**, **Blazor Server**, **Microsoft Fluent UI Blazor V5**, **EF Core 10**, and **PostgreSQL**.

> [!NOTE]
> All detailed architectural plans, guides, recipes, and security models are organized into dedicated chapters below.
> Historical early planning artifacts have been archived in [`docs/archive/Design-History.md`](file:///c:/Users/cheri/source/repos/BlazorFluent/docs/archive/Design-History.md).

---

## 📚 Documentation Index

| Guide | Description | Target Audience |
| :--- | :--- | :--- |
| [**00 — Overview & Domain Glossary**](file:///c:/Users/cheri/source/repos/BlazorFluent/docs/00-Overview-and-Glossary.md) | High-level mental model, personas, feature routing map, domain glossary, and tenancy architecture. | Architects, New Developers, Tech Leads |
| [**01 — Getting Started**](file:///c:/Users/cheri/source/repos/BlazorFluent/docs/01-Getting-Started.md) | Prerequisites, renaming script (`Rename-Project.ps1`), `appsettings` key dictionary, EF Core migrations, dev login, and troubleshooting. | Everyone starting a new project |
| [**02 — Architecture**](file:///c:/Users/cheri/source/repos/BlazorFluent/docs/02-Architecture.md) | Clean Architecture 6-project topology, DI lifetime rules, request pipeline, EF interceptors, caching, and background jobs. | Software Engineers, Architects |
| [**03 — Security Model**](file:///c:/Users/cheri/source/repos/BlazorFluent/docs/03-Security-Model.md) | Defense-in-depth model, HMAC user seals, fail-closed tenant isolation, RBAC/PBAC, active session control, impersonation, and AES-GCM file encryption. | Security Engineers, Lead Developers |
| [**04 — Task Hierarchy & Workflows**](file:///c:/Users/cheri/source/repos/BlazorFluent/docs/04-Task-Hierarchy-and-Workflows.md) | Multi-level work management (Tenant > Program > Project), depth-1 tasks, ServiceNow-style sequential workflows, RBAC rules, and `<FluentWizardModal>`. | Full-stack Engineers, Product Owners |
| [**05 — UI Conventions (Fluent UI Blazor V5)**](file:///c:/Users/cheri/source/repos/BlazorFluent/docs/05-UI-Conventions-Fluent-V5.md) | 4-tier naming taxonomy (`*Page`, `*Comp`, `*Modal`, `*Popup`), reusable catalog, V5 compliance scanner, and component pitfall table. | Frontend & Blazor Developers |
| [**06 — How to Extend**](file:///c:/Users/cheri/source/repos/BlazorFluent/docs/06-How-To-Extend.md) | Copy-pasteable step-by-step recipes: add entity, service, page, background job, file importer, or wizard. | Developers building new features |
| [**07 — AI Agent Rules**](file:///c:/Users/cheri/source/repos/BlazorFluent/docs/07-AI-Agent-Rules.md) | Operational rules for AI assistants (never build/run, never git stage, V5 compliance, authority hierarchy). | AI Agents, Prompt Engineers |

---

## 🗄️ Historical Documentation

- [`docs/archive/Design-History.md`](file:///c:/Users/cheri/source/repos/BlazorFluent/docs/archive/Design-History.md) — Preserved records of historical RFCs, early security design reviews, and framework comparison studies.