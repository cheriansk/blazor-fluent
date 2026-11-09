using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorEnterprise.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEstimatedHoursToTaskEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedHours",
                schema: "tasks",
                table: "Tasks",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstimatedHours",
                schema: "tasks",
                table: "Tasks");
        }
    }
}
