using Microsoft.EntityFrameworkCore.Migrations;

namespace BlazorFluent.Persistence.Extensions;

/// <summary>
/// Migration extension methods to configure PostgreSQL Native Row-Level Security (RLS) - Layer 5 defense-in-depth.
/// Automatically handles ENABLE ROW LEVEL SECURITY, FORCE ROW LEVEL SECURITY, and tenant isolation policies.
/// </summary>
public static class MigrationBuilderRlsExtensions
{
    /// <summary>
    /// Enables and forces PostgreSQL Row-Level Security (RLS) on a table and provisions a fail-closed tenant isolation policy.
    /// </summary>
    public static MigrationBuilder EnableTenantRowLevelSecurity(
        this MigrationBuilder migrationBuilder,
        string schema,
        string table,
        string tenantColumn = "TenantId")
    {
        migrationBuilder.Sql($@"
DO $$
BEGIN
    -- 1. Enable RLS on the table
    ALTER TABLE ""{schema}"".""{table}"" ENABLE ROW LEVEL SECURITY;

    -- 2. FORCE RLS so table owner is also strictly bounded by RLS policies
    ALTER TABLE ""{schema}"".""{table}"" FORCE ROW LEVEL SECURITY;

    -- 3. Idempotently recreate tenant isolation policy
    DROP POLICY IF EXISTS ""{table}_tenant_isolation_policy"" ON ""{schema}"".""{table}"";

    CREATE POLICY ""{table}_tenant_isolation_policy"" ON ""{schema}"".""{table}""
        FOR ALL
        USING (
            current_setting('app.is_host', true) = 'true'
            OR ""{tenantColumn}"" = current_setting('app.current_tenant_id', true)
        )
        WITH CHECK (
            current_setting('app.is_host', true) = 'true'
            OR ""{tenantColumn}"" = current_setting('app.current_tenant_id', true)
        );
END $$;");

        return migrationBuilder;
    }

    /// <summary>
    /// Drops the tenant isolation policy and disables Row-Level Security on a table.
    /// </summary>
    public static MigrationBuilder DisableTenantRowLevelSecurity(
        this MigrationBuilder migrationBuilder,
        string schema,
        string table)
    {
        migrationBuilder.Sql($@"
DO $$
BEGIN
    DROP POLICY IF EXISTS ""{table}_tenant_isolation_policy"" ON ""{schema}"".""{table}"";
    ALTER TABLE ""{schema}"".""{table}"" NO FORCE ROW LEVEL SECURITY;
    ALTER TABLE ""{schema}"".""{table}"" DISABLE ROW LEVEL SECURITY;
END $$;");

        return migrationBuilder;
    }

    /// <summary>
    /// Applies Layer 5 PostgreSQL Native Row-Level Security policies to all ITenantEntity tables.
    /// </summary>
    public static MigrationBuilder ApplyAllTenantRowLevelSecurity(this MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnableTenantRowLevelSecurity("catalog", "Products");
        migrationBuilder.EnableTenantRowLevelSecurity("tenancy", "Projects");
        migrationBuilder.EnableTenantRowLevelSecurity("tenancy", "ProjectUserRoles");
        migrationBuilder.EnableTenantRowLevelSecurity("app", "AuditRecords");
        migrationBuilder.EnableTenantRowLevelSecurity("app", "Notifications");
        migrationBuilder.EnableTenantRowLevelSecurity("app", "Tasks");
        migrationBuilder.EnableTenantRowLevelSecurity("app", "TaskComments");

        return migrationBuilder;
    }

    /// <summary>
    /// Reverts Layer 5 PostgreSQL Native Row-Level Security policies from all ITenantEntity tables.
    /// </summary>
    public static MigrationBuilder RevertAllTenantRowLevelSecurity(this MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DisableTenantRowLevelSecurity("catalog", "Products");
        migrationBuilder.DisableTenantRowLevelSecurity("tenancy", "Projects");
        migrationBuilder.DisableTenantRowLevelSecurity("tenancy", "ProjectUserRoles");
        migrationBuilder.DisableTenantRowLevelSecurity("app", "AuditRecords");
        migrationBuilder.DisableTenantRowLevelSecurity("app", "Notifications");
        migrationBuilder.DisableTenantRowLevelSecurity("app", "Tasks");
        migrationBuilder.DisableTenantRowLevelSecurity("app", "TaskComments");

        return migrationBuilder;
    }

    /// <summary>
    /// Provisions a dedicated non-superuser role for application runtime connections and grants appropriate permissions.
    /// In PostgreSQL, superusers bypass RLS, so runtime app queries must run under a non-superuser role (e.g. 'blazorfluent_app').
    /// </summary>
    public static MigrationBuilder CreateAppRoleAndGrantPermissions(
        this MigrationBuilder migrationBuilder,
        string appRole = "blazorfluent_app",
        string appPassword = "blazorfluent_app_password")
    {
        migrationBuilder.Sql($@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = '{appRole}') THEN
        CREATE ROLE {appRole} WITH LOGIN PASSWORD '{appPassword}' NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOBYPASSRLS;
    END IF;

    -- Grant schema usage
    GRANT USAGE ON SCHEMA public, app, tenancy, catalog, identity TO {appRole};

    -- Grant CRUD on existing tables
    GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public, app, tenancy, catalog, identity TO {appRole};

    -- Grant sequence usage
    GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public, app, tenancy, catalog, identity TO {appRole};

    -- Ensure future tables and sequences inherit permissions
    ALTER DEFAULT PRIVILEGES IN SCHEMA public, app, tenancy, catalog, identity GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {appRole};
    ALTER DEFAULT PRIVILEGES IN SCHEMA public, app, tenancy, catalog, identity GRANT USAGE, SELECT ON SEQUENCES TO {appRole};
END $$;");

        return migrationBuilder;
    }
}
