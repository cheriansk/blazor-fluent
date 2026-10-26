# 13. Database Migrations & Schema Evolution Guide

> **Target Audience**: Database Administrators, Backend Engineers, and Platform Operators.  
> **Key Objective**: Standardized protocols for creating, testing, verifying, and executing PostgreSQL schema migrations in multi-tenant production environments without service interruption.

---

## 🏛️ 1. Core Principles of Schema Evolution

1. **Zero-Downtime Rule (Expand / Contract Pattern)**:
   - Schema changes must **never break currently running application instances**.
   - Breaking changes must span at least two releases:
     - **Release N (Expand)**: Add new column/table, write to both, read from old (fallback to new).
     - **Release N+1 (Contract)**: Backfill data, switch reads to new, deprecate old column.
     - **Release N+2 (Cleanup)**: Safely drop old column/table.
2. **Deterministic Versioning**:
   - Every migration must be generated through EF Core CLI tooling with a descriptive name.
3. **Multi-Tenant RLS Preservation**:
   - Any new table carrying `ITenantEntity` must be registered with RLS policies in `MigrationBuilderRlsExtensions`.

---

## 🛠️ 2. Developer Migration Workflow

> [!CAUTION]
> In accordance with repository rules, AI assistants must **NEVER** run migration commands directly. The human developer executes these commands from the repository root.

### 2.1 Generating a Migration
```bash
dotnet ef migrations add AddUserPreferencesTable \
    --project src/BlazorEnterprise.Persistence \
    --startup-project src/BlazorEnterprise.Web \
    --context AppDbContext \
    --output-dir Migrations
```

### 2.2 Generating Idempotent SQL Scripts for Production Review
Production database updates are **never applied via automatic `context.Database.Migrate()`** in enterprise deployments. Instead, generate an idempotent SQL script:

```bash
dotnet ef migrations script \
    --project src/BlazorEnterprise.Persistence \
    --startup-project src/BlazorEnterprise.Web \
    --idempotent \
    --output migrations_release_v2.sql
```

The resulting script contains safe conditional checks:
```sql
CREATE TABLE IF NOT EXISTS public."UserPreferences" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "TimeZoneId" character varying(100) NOT NULL,
    CONSTRAINT "PK_UserPreferences" PRIMARY KEY ("Id")
);
```

---

## 📋 3. Schema Evolution Checklist

Before approving any migration PR, verify the following:

- [ ] **RLS Filter Applied**: Does the table implement `ITenantEntity`? If yes, is `builder.ApplyTenantSecurityPolicies()` called?
- [ ] **Soft Delete Covered**: Does the table implement `ISoftDeletable`? If yes, is `IsDeleted` indexed?
- [ ] **No Table Locks**:
  - Adding a column with a `DEFAULT` in PostgreSQL 11+ is metadata-only (safe).
  - Adding a non-null column without default is **strictly prohibited**.
  - Renaming columns requires the Expand/Contract phased approach.
- [ ] **Indexes Concurrently Created**:
  - For large existing tables (> 100k rows), create indexes with `CONCURRENTLY` outside of a single transaction:
    ```csharp
    migrationBuilder.Sql("CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_tasks_tenant_project ON tasks(tenant_id, project_id);", suppressTransaction: true);
    ```

---

## ↩️ 4. Rollback & Disaster Recovery Procedures

### 4.1 Rolling Back to a Previous Migration
If an issue is detected during staging verification:

```bash
dotnet ef database update TargetMigrationName \
    --project src/BlazorEnterprise.Persistence \
    --startup-project src/BlazorEnterprise.Web
```

### 4.2 Removing Unapplied Migrations
If a newly created migration has not been pushed or committed:
```bash
dotnet ef migrations remove \
    --project src/BlazorEnterprise.Persistence \
    --startup-project src/BlazorEnterprise.Web
```

---

*Related Documentation*:
- [02. Architecture & Project Structure](file:///c:/Users/cheri/source/repos/BlazorEnterprise/docs/02-Architecture-and-Project-Structure.md)
- [12. Production Deployment & Operations](file:///c:/Users/cheri/source/repos/BlazorEnterprise/docs/12-Production-Deployment-and-Operations.md)
