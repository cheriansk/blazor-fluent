using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorEnterprise.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ResourceAllocation_SprintPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectResources",
                schema: "tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectResources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectResources_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "tenancy",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectResources_Users_UserEntityId",
                        column: x => x.UserEntityId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Sprints",
                schema: "tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    SprintNumber = table.Column<int>(type: "integer", nullable: false),
                    SprintName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Comments = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sprints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sprints_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "tenancy",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResourceAllocations",
                schema: "tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllocationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AllocationPercentage = table.Column<int>(type: "integer", nullable: false),
                    RevisionHeaderId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelId = table.Column<Guid>(type: "uuid", nullable: false),
                    VerNum = table.Column<int>(type: "integer", nullable: false),
                    ParentRowId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceAllocations_ProjectResources_ProjectResourceId",
                        column: x => x.ProjectResourceId,
                        principalSchema: "tasks",
                        principalTable: "ProjectResources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourceAllocations_RevisionHeaders_RevisionHeaderId",
                        column: x => x.RevisionHeaderId,
                        principalSchema: "versioning",
                        principalTable: "RevisionHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResourceRates",
                schema: "tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    HourlyRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    EffectiveFromDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EffectiveToDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevisionHeaderId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelId = table.Column<Guid>(type: "uuid", nullable: false),
                    VerNum = table.Column<int>(type: "integer", nullable: false),
                    ParentRowId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceRates_ProjectResources_ProjectResourceId",
                        column: x => x.ProjectResourceId,
                        principalSchema: "tasks",
                        principalTable: "ProjectResources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourceRates_RevisionHeaders_RevisionHeaderId",
                        column: x => x.RevisionHeaderId,
                        principalSchema: "versioning",
                        principalTable: "RevisionHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SprintAuditLogs",
                schema: "tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SprintId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangeType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ChangedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ChangedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OldValue = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    NewValue = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Details = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SprintAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SprintAuditLogs_Sprints_SprintId",
                        column: x => x.SprintId,
                        principalSchema: "tasks",
                        principalTable: "Sprints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SprintItemVersions",
                schema: "tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    SprintId = table.Column<Guid>(type: "uuid", nullable: true),
                    EstimatedHours = table.Column<double>(type: "double precision", nullable: false),
                    ActualHours = table.Column<double>(type: "double precision", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFromDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveToDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevisionHeaderId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelId = table.Column<Guid>(type: "uuid", nullable: false),
                    VerNum = table.Column<int>(type: "integer", nullable: false),
                    ParentRowId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SprintItemVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SprintItemVersions_RevisionHeaders_RevisionHeaderId",
                        column: x => x.RevisionHeaderId,
                        principalSchema: "versioning",
                        principalTable: "RevisionHeaders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SprintItemVersions_Sprints_SprintId",
                        column: x => x.SprintId,
                        principalSchema: "tasks",
                        principalTable: "Sprints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SprintItemVersions_Tasks_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "tasks",
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectResources_Project_User",
                schema: "tasks",
                table: "ProjectResources",
                columns: new[] { "ProjectId", "UserEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectResources_TenantId",
                schema: "tasks",
                table: "ProjectResources",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectResources_UserEntityId",
                schema: "tasks",
                table: "ProjectResources",
                column: "UserEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceAllocations_Plan_Date",
                schema: "tasks",
                table: "ResourceAllocations",
                columns: new[] { "RevisionHeaderId", "AllocationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ResourceAllocations_Resource_Plan",
                schema: "tasks",
                table: "ResourceAllocations",
                columns: new[] { "ProjectResourceId", "RevisionHeaderId" });

            migrationBuilder.CreateIndex(
                name: "IX_ResourceAllocations_TenantId",
                schema: "tasks",
                table: "ResourceAllocations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceRates_Plan_Resource",
                schema: "tasks",
                table: "ResourceRates",
                columns: new[] { "RevisionHeaderId", "ProjectResourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_ResourceRates_ProjectResourceId",
                schema: "tasks",
                table: "ResourceRates",
                column: "ProjectResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceRates_TenantId",
                schema: "tasks",
                table: "ResourceRates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SprintAuditLogs_SprintId",
                schema: "tasks",
                table: "SprintAuditLogs",
                column: "SprintId");

            migrationBuilder.CreateIndex(
                name: "IX_SprintAuditLogs_TenantId",
                schema: "tasks",
                table: "SprintAuditLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SprintItemVersions_Item_Sprint",
                schema: "tasks",
                table: "SprintItemVersions",
                columns: new[] { "ItemId", "SprintId" });

            migrationBuilder.CreateIndex(
                name: "IX_SprintItemVersions_Project_Sprint",
                schema: "tasks",
                table: "SprintItemVersions",
                columns: new[] { "ProjectId", "SprintId" });

            migrationBuilder.CreateIndex(
                name: "IX_SprintItemVersions_RevisionHeaderId",
                schema: "tasks",
                table: "SprintItemVersions",
                column: "RevisionHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_SprintItemVersions_SprintId",
                schema: "tasks",
                table: "SprintItemVersions",
                column: "SprintId");

            migrationBuilder.CreateIndex(
                name: "IX_SprintItemVersions_TenantId",
                schema: "tasks",
                table: "SprintItemVersions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Sprints_Project_Number",
                schema: "tasks",
                table: "Sprints",
                columns: new[] { "ProjectId", "SprintNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sprints_Project_Status",
                schema: "tasks",
                table: "Sprints",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Sprints_TenantId",
                schema: "tasks",
                table: "Sprints",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResourceAllocations",
                schema: "tasks");

            migrationBuilder.DropTable(
                name: "ResourceRates",
                schema: "tasks");

            migrationBuilder.DropTable(
                name: "SprintAuditLogs",
                schema: "tasks");

            migrationBuilder.DropTable(
                name: "SprintItemVersions",
                schema: "tasks");

            migrationBuilder.DropTable(
                name: "ProjectResources",
                schema: "tasks");

            migrationBuilder.DropTable(
                name: "Sprints",
                schema: "tasks");
        }
    }
}
