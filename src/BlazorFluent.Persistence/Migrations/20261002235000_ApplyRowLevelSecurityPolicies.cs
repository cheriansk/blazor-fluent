using BlazorFluent.Persistence.Extensions;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorFluent.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ApplyRowLevelSecurityPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Provisions non-superuser role (blazorfluent_app) and grants permissions across all schemas
            migrationBuilder.CreateAppRoleAndGrantPermissions();

            // 2. Applies PostgreSQL Native Row-Level Security (RLS) policies and FORCE RLS to all tenant tables
            migrationBuilder.ApplyAllTenantRowLevelSecurity();
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverts PostgreSQL Native Row-Level Security policies and disables RLS
            migrationBuilder.RevertAllTenantRowLevelSecurity();
        }
    }
}
