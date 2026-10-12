using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorFluent.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectMetadataAndStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Location",
                schema: "tenancy",
                table: "Projects",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScopeSummary",
                schema: "tenancy",
                table: "Projects",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShortCode",
                schema: "tenancy",
                table: "Projects",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "tenancy",
                table: "Projects",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "TentativeEndDate",
                schema: "tenancy",
                table: "Projects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TentativeStartDate",
                schema: "tenancy",
                table: "Projects",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Location",
                schema: "tenancy",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ScopeSummary",
                schema: "tenancy",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ShortCode",
                schema: "tenancy",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "tenancy",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "TentativeEndDate",
                schema: "tenancy",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "TentativeStartDate",
                schema: "tenancy",
                table: "Projects");
        }
    }
}
