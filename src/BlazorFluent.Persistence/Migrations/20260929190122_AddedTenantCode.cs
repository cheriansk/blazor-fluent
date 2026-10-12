using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorFluent.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddedTenantCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DisplayName",
                schema: "tenancy",
                table: "Tenants",
                newName: "Name");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                schema: "tenancy",
                table: "Tenants",
                type: "character varying(15)",
                maxLength: 15,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Code",
                schema: "tenancy",
                table: "Tenants",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tenants_Code",
                schema: "tenancy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Code",
                schema: "tenancy",
                table: "Tenants");

            migrationBuilder.RenameColumn(
                name: "Name",
                schema: "tenancy",
                table: "Tenants",
                newName: "DisplayName");
        }
    }
}
