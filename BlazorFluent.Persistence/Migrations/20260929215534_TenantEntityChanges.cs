using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorFluent.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TenantEntityChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalEmailDomains",
                schema: "tenancy",
                table: "Tenants",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InternalEmailDomains",
                schema: "tenancy",
                table: "Tenants",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExternalEmailDomains",
                schema: "tenancy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "InternalEmailDomains",
                schema: "tenancy",
                table: "Tenants");
        }
    }
}
