using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorEnterprise.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261116052313_PendingChanges : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_ProjectUserRoles_TenantId_UserEntityId_IsDeleted",
            schema: "tenancy",
            table: "ProjectUserRoles",
            columns: new[] { "TenantId", "UserEntityId", "IsDeleted" });

        migrationBuilder.CreateIndex(
            name: "IX_Projects_TenantId_IsDeleted_Status",
            schema: "tenancy",
            table: "Projects",
            columns: new[] { "TenantId", "IsDeleted", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_program_user_roles_TenantId_UserEntityId_IsDeleted",
            schema: "tenancy",
            table: "program_user_roles",
            columns: new[] { "TenantId", "UserEntityId", "IsDeleted" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ProjectUserRoles_TenantId_UserEntityId_IsDeleted",
            schema: "tenancy",
            table: "ProjectUserRoles");

        migrationBuilder.DropIndex(
            name: "IX_Projects_TenantId_IsDeleted_Status",
            schema: "tenancy",
            table: "Projects");

        migrationBuilder.DropIndex(
            name: "IX_program_user_roles_TenantId_UserEntityId_IsDeleted",
            schema: "tenancy",
            table: "program_user_roles");
    }
}
