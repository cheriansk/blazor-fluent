using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorEnterprise.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CalendarChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AssociatedTaskId",
                schema: "calendar",
                table: "UserCalendars",
                newName: "ParentTimelineId");

            migrationBuilder.RenameColumn(
                name: "AssociatedTaskId",
                schema: "calendar",
                table: "ProjectCalendars",
                newName: "ParentTimelineId");

            migrationBuilder.RenameColumn(
                name: "AssociatedTaskId",
                schema: "calendar",
                table: "ProgramCalendars",
                newName: "ParentTimelineId");

            migrationBuilder.AddColumn<string>(
                name: "Comments",
                schema: "calendar",
                table: "UserCalendars",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DueDate",
                schema: "calendar",
                table: "UserCalendars",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "OwnerEmails",
                schema: "calendar",
                table: "UserCalendars",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Comments",
                schema: "calendar",
                table: "ProjectCalendars",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerEmails",
                schema: "calendar",
                table: "ProjectCalendars",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Comments",
                schema: "calendar",
                table: "ProgramCalendars",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerEmails",
                schema: "calendar",
                table: "ProgramCalendars",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserCalendars_User_DueDate",
                schema: "calendar",
                table: "UserCalendars",
                columns: new[] { "UserId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCalendars_ParentTimelineId",
                schema: "calendar",
                table: "ProjectCalendars",
                column: "ParentTimelineId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramCalendars_ParentTimelineId",
                schema: "calendar",
                table: "ProgramCalendars",
                column: "ParentTimelineId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProgramCalendars_ProgramCalendars_ParentTimelineId",
                schema: "calendar",
                table: "ProgramCalendars",
                column: "ParentTimelineId",
                principalSchema: "calendar",
                principalTable: "ProgramCalendars",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectCalendars_ProjectCalendars_ParentTimelineId",
                schema: "calendar",
                table: "ProjectCalendars",
                column: "ParentTimelineId",
                principalSchema: "calendar",
                principalTable: "ProjectCalendars",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProgramCalendars_ProgramCalendars_ParentTimelineId",
                schema: "calendar",
                table: "ProgramCalendars");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectCalendars_ProjectCalendars_ParentTimelineId",
                schema: "calendar",
                table: "ProjectCalendars");

            migrationBuilder.DropIndex(
                name: "IX_UserCalendars_User_DueDate",
                schema: "calendar",
                table: "UserCalendars");

            migrationBuilder.DropIndex(
                name: "IX_ProjectCalendars_ParentTimelineId",
                schema: "calendar",
                table: "ProjectCalendars");

            migrationBuilder.DropIndex(
                name: "IX_ProgramCalendars_ParentTimelineId",
                schema: "calendar",
                table: "ProgramCalendars");

            migrationBuilder.DropColumn(
                name: "Comments",
                schema: "calendar",
                table: "UserCalendars");

            migrationBuilder.DropColumn(
                name: "DueDate",
                schema: "calendar",
                table: "UserCalendars");

            migrationBuilder.DropColumn(
                name: "OwnerEmails",
                schema: "calendar",
                table: "UserCalendars");

            migrationBuilder.DropColumn(
                name: "Comments",
                schema: "calendar",
                table: "ProjectCalendars");

            migrationBuilder.DropColumn(
                name: "OwnerEmails",
                schema: "calendar",
                table: "ProjectCalendars");

            migrationBuilder.DropColumn(
                name: "Comments",
                schema: "calendar",
                table: "ProgramCalendars");

            migrationBuilder.DropColumn(
                name: "OwnerEmails",
                schema: "calendar",
                table: "ProgramCalendars");

            migrationBuilder.RenameColumn(
                name: "ParentTimelineId",
                schema: "calendar",
                table: "UserCalendars",
                newName: "AssociatedTaskId");

            migrationBuilder.RenameColumn(
                name: "ParentTimelineId",
                schema: "calendar",
                table: "ProjectCalendars",
                newName: "AssociatedTaskId");

            migrationBuilder.RenameColumn(
                name: "ParentTimelineId",
                schema: "calendar",
                table: "ProgramCalendars",
                newName: "AssociatedTaskId");
        }
    }
}
