# Implementation Plan: PostgreSQL Native Row-Level Security (RLS) Defense-in-Depth

This plan formalizes the implementation of Layer 5 defense-in-depth: PostgreSQL Native Row-Level Security (RLS). This ensures that PostgreSQL physically rejects cross-tenant reads, inserts, updates, and deletes at the database engine level, even if C# query filters are bypassed.

## User Review Required

> [!IMPORTANT]
> **PostgreSQL Superuser Bypass Rule**: In PostgreSQL, database superusers (`postgres`) and roles with the `BYPASSRLS` attribute inherently bypass RLS policies even when `FORCE ROW LEVEL SECURITY` is applied. 
> To guarantee RLS enforcement, application runtime queries will execute under a dedicated non-superuser role (`blazorfluent_app`), while `postgres` is reserved for schema migrations and administrative operations.

> [!NOTE]
> As requested by the user, **no build, compilation, or execution commands** (`dotnet build`, `dotnet run`, `dotnet ef`) will be run by the assistant. All verification commands will be clearly provided for the user to execute.

---

## Architectural Workflow

```mermaid
sequenceDiagram
    autonumber
    actor User as Client / User Request
    participant EF as EF Core (AppDbContext)
    participant Interceptor as TenantDbConnectionInterceptor
    participant PG as PostgreSQL Engine (RLS)
    
    User->>EF: LINQ Query / Mutation
    EF->>Interceptor: ConnectionOpened / ConnectionOpenedAsync
    alt IsHost == true
        Interceptor->>PG: set_config('app.is_host', 'true', false)
        Interceptor->>PG: set_config('app.current_tenant_id', '', false)
    else Tenant User
        Interceptor->>PG: set_config('app.is_host', 'false', false)
        Interceptor->>PG: set_config('app.current_tenant_id', '<TenantId>', false)
    else Anonymous / Unauthenticated
        Interceptor->>PG: set_config('app.is_host', 'false', false)
        Interceptor->>PG: set_config('app.current_tenant_id', '', false) (Fail-Closed)
    end
    EF->>PG: Execute SQL Statement
    PG->>PG: Evaluate RLS Policy:<br/>app.is_host = 'true' OR "TenantId" = app.current_tenant_id
    PG-->>EF: Return Tenant-Filtered Rows or Reject Foreign Mutations
    EF-->>User: Result
```

---
