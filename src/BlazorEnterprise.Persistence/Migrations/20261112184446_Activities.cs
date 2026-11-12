using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorEnterprise.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Activities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MiscTasks",
                schema: "tasks");

            migrationBuilder.CreateTable(
                name: "ActionItems",
                schema: "tasks",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentSubTaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Complexity = table.Column<int>(type: "integer", nullable: false),
                    EstimatedHours = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    IsBlocked = table.Column<bool>(type: "boolean", nullable: false),
                    BlockingReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ActivityTasks",
                schema: "tasks",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EstimatedHours = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MilestoneId = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Level = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevisedDueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReporterEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false, defaultValue: ""),
                    AssigneeEmails = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true, defaultValue: ""),
                    WatcherEmails = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true, defaultValue: ""),
                    Labels = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SourceRowId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsEncryptedAtRest = table.Column<bool>(type: "boolean", nullable: false),
                    EncryptedPayloadBytes = table.Column<byte[]>(type: "bytea", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityTasks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActionItems_ParentSubTaskId_Order",
                schema: "tasks",
                table: "ActionItems",
                columns: new[] { "ParentSubTaskId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_ActionItems_ParentTaskId",
                schema: "tasks",
                table: "ActionItems",
                column: "ParentTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_ActionItems_Tenant_Status",
                schema: "tasks",
                table: "ActionItems",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ActionItems_TenantId",
                schema: "tasks",
                table: "ActionItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityTasks_Tenant_Level_Status_DueDate",
                schema: "tasks",
                table: "ActivityTasks",
                columns: new[] { "TenantId", "Level", "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityTasks_TenantId",
                schema: "tasks",
                table: "ActivityTasks",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityTasks_TenantId_Level",
                schema: "tasks",
                table: "ActivityTasks",
                columns: new[] { "TenantId", "Level" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActionItems",
                schema: "tasks");

            migrationBuilder.DropTable(
                name: "ActivityTasks",
                schema: "tasks");

            migrationBuilder.CreateTable(
                name: "MiscTasks",
                schema: "tasks",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActualEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AssigneeEmails = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true, defaultValue: ""),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EncryptedPayloadBytes = table.Column<byte[]>(type: "bytea", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EstimatedHours = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsEncryptedAtRest = table.Column<bool>(type: "boolean", nullable: false),
                    Labels = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Level = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    MilestoneId = table.Column<Guid>(type: "uuid", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReporterEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false, defaultValue: ""),
                    RevisedDueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SourceRowId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    WatcherEmails = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true, defaultValue: "")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MiscTasks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MiscTasks_Tenant_Level_Status_DueDate",
                schema: "tasks",
                table: "MiscTasks",
                columns: new[] { "TenantId", "Level", "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_MiscTasks_TenantId",
                schema: "tasks",
                table: "MiscTasks",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MiscTasks_TenantId_Level",
                schema: "tasks",
                table: "MiscTasks",
                columns: new[] { "TenantId", "Level" });
        }
    }
}
