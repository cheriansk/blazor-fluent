using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorEnterprise.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSprintIdToTaskEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SprintId",
                schema: "tasks",
                table: "Tasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_TenantId_SprintId",
                schema: "tasks",
                table: "Tasks",
                columns: new[] { "TenantId", "SprintId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_Sprints_SprintId",
                schema: "tasks",
                table: "Tasks",
                column: "SprintId",
                principalSchema: "app",
                principalTable: "Sprints",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_Sprints_SprintId",
                schema: "tasks",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_TenantId_SprintId",
                schema: "tasks",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "SprintId",
                schema: "tasks",
                table: "Tasks");
        }
    }
}
