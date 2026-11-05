using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorEnterprise.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VersioningChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VerNum",
                schema: "calendar",
                table: "UserCalendars");

            migrationBuilder.EnsureSchema(
                name: "versioning");

            migrationBuilder.AddColumn<Guid>(
                name: "ParentRowId",
                schema: "calendar",
                table: "ProjectCalendars",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "RelId",
                schema: "calendar",
                table: "ProjectCalendars",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "RevisionHeaderId",
                schema: "calendar",
                table: "ProjectCalendars",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ParentRowId",
                schema: "calendar",
                table: "ProgramCalendars",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "RelId",
                schema: "calendar",
                table: "ProgramCalendars",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "RevisionHeaderId",
                schema: "calendar",
                table: "ProgramCalendars",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "RevisionHeaders",
                schema: "versioning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeLevel = table.Column<int>(type: "integer", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    FeatureType = table.Column<int>(type: "integer", nullable: false),
                    VerNum = table.Column<int>(type: "integer", nullable: false),
                    ParentVerNum = table.Column<int>(type: "integer", nullable: false, defaultValue: -1),
                    ParentRevisionHeaderId = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("00000000-0000-0000-0000-000000000000")),
                    RevisionName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatorEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AssignedToEmails = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReviewerEmails = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReviewComments = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SubmittedForReviewAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ActivatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActivatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SupersededAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevisionHeaders", x => x.Id);
                    table.CheckConstraint("CK_RevisionHeaders_Lineage_Integrity", "\"VerNum\" >= 1 AND (\"ParentVerNum\" = -1 OR (\"ParentVerNum\" >= 1 AND \"ParentVerNum\" < \"VerNum\"))");
                    table.CheckConstraint("CK_RevisionHeaders_Scope_Hierarchy", "(\"ScopeLevel\" = 1 AND \"ProgramId\" IS NULL AND \"ProjectId\" IS NULL) OR (\"ScopeLevel\" = 2 AND \"ProgramId\" IS NOT NULL AND \"ProjectId\" IS NULL) OR (\"ScopeLevel\" = 3 AND \"ProgramId\" IS NOT NULL AND \"ProjectId\" IS NOT NULL)");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCalendars_RelId",
                schema: "calendar",
                table: "ProjectCalendars",
                column: "RelId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCalendars_Revision_RelId",
                schema: "calendar",
                table: "ProjectCalendars",
                columns: new[] { "RevisionHeaderId", "RelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCalendars_RevisionHeaderId",
                schema: "calendar",
                table: "ProjectCalendars",
                column: "RevisionHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramCalendars_RelId",
                schema: "calendar",
                table: "ProgramCalendars",
                column: "RelId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramCalendars_Revision_RelId",
                schema: "calendar",
                table: "ProgramCalendars",
                columns: new[] { "RevisionHeaderId", "RelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProgramCalendars_RevisionHeaderId",
                schema: "calendar",
                table: "ProgramCalendars",
                column: "RevisionHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_RevisionHeaders_ParentRevisionHeaderId",
                schema: "versioning",
                table: "RevisionHeaders",
                column: "ParentRevisionHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_RevisionHeaders_Scenario_SingleActive",
                schema: "versioning",
                table: "RevisionHeaders",
                columns: new[] { "TenantId", "ProgramId", "ProjectId", "FeatureType" },
                unique: true,
                filter: "\"Status\" = 4 AND \"IsDeleted\" = false")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_RevisionHeaders_Scenario_VerNum_Unique",
                schema: "versioning",
                table: "RevisionHeaders",
                columns: new[] { "TenantId", "ProgramId", "ProjectId", "FeatureType", "VerNum" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_RevisionHeaders_Tenant_Status",
                schema: "versioning",
                table: "RevisionHeaders",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RevisionHeaders_TenantId",
                schema: "versioning",
                table: "RevisionHeaders",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProgramCalendars_RevisionHeaders_RevisionHeaderId",
                schema: "calendar",
                table: "ProgramCalendars",
                column: "RevisionHeaderId",
                principalSchema: "versioning",
                principalTable: "RevisionHeaders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectCalendars_RevisionHeaders_RevisionHeaderId",
                schema: "calendar",
                table: "ProjectCalendars",
                column: "RevisionHeaderId",
                principalSchema: "versioning",
                principalTable: "RevisionHeaders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProgramCalendars_RevisionHeaders_RevisionHeaderId",
                schema: "calendar",
                table: "ProgramCalendars");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectCalendars_RevisionHeaders_RevisionHeaderId",
                schema: "calendar",
                table: "ProjectCalendars");

            migrationBuilder.DropTable(
                name: "RevisionHeaders",
                schema: "versioning");

            migrationBuilder.DropIndex(
                name: "IX_ProjectCalendars_RelId",
                schema: "calendar",
                table: "ProjectCalendars");

            migrationBuilder.DropIndex(
                name: "IX_ProjectCalendars_Revision_RelId",
                schema: "calendar",
                table: "ProjectCalendars");

            migrationBuilder.DropIndex(
                name: "IX_ProjectCalendars_RevisionHeaderId",
                schema: "calendar",
                table: "ProjectCalendars");

            migrationBuilder.DropIndex(
                name: "IX_ProgramCalendars_RelId",
                schema: "calendar",
                table: "ProgramCalendars");

            migrationBuilder.DropIndex(
                name: "IX_ProgramCalendars_Revision_RelId",
                schema: "calendar",
                table: "ProgramCalendars");

            migrationBuilder.DropIndex(
                name: "IX_ProgramCalendars_RevisionHeaderId",
                schema: "calendar",
                table: "ProgramCalendars");

            migrationBuilder.DropColumn(
                name: "ParentRowId",
                schema: "calendar",
                table: "ProjectCalendars");

            migrationBuilder.DropColumn(
                name: "RelId",
                schema: "calendar",
                table: "ProjectCalendars");

            migrationBuilder.DropColumn(
                name: "RevisionHeaderId",
                schema: "calendar",
                table: "ProjectCalendars");

            migrationBuilder.DropColumn(
                name: "ParentRowId",
                schema: "calendar",
                table: "ProgramCalendars");

            migrationBuilder.DropColumn(
                name: "RelId",
                schema: "calendar",
                table: "ProgramCalendars");

            migrationBuilder.DropColumn(
                name: "RevisionHeaderId",
                schema: "calendar",
                table: "ProgramCalendars");

            migrationBuilder.AddColumn<int>(
                name: "VerNum",
                schema: "calendar",
                table: "UserCalendars",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
