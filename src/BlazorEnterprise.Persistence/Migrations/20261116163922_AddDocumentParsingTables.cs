using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorEnterprise.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261116163922_AddDocumentParsingTables : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "core");

        migrationBuilder.AlterColumn<int>(
            name: "Level",
            schema: "tasks",
            table: "WorkflowTasks",
            type: "integer",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "integer",
            oldDefaultValue: 3);

        migrationBuilder.AlterColumn<int>(
            name: "Level",
            schema: "tasks",
            table: "Workflows",
            type: "integer",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "integer",
            oldDefaultValue: 3);

        migrationBuilder.AlterColumn<int>(
            name: "Level",
            schema: "tasks",
            table: "WorkDependencies",
            type: "integer",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "integer",
            oldDefaultValue: 3);

        migrationBuilder.AlterColumn<string>(
            name: "UserKind",
            schema: "identity",
            table: "Users",
            type: "character varying(32)",
            maxLength: 32,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(32)",
            oldMaxLength: 32,
            oldDefaultValue: "Human");

        migrationBuilder.AlterColumn<int>(
            name: "Level",
            schema: "tasks",
            table: "SubTasks",
            type: "integer",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "integer",
            oldDefaultValue: 3);

        migrationBuilder.AlterColumn<int>(
            name: "Level",
            schema: "tasks",
            table: "SprintTasks",
            type: "integer",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "integer",
            oldDefaultValue: 3);

        migrationBuilder.AlterColumn<int>(
            name: "Level",
            schema: "tasks",
            table: "Risks",
            type: "integer",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "integer",
            oldDefaultValue: 3);

        migrationBuilder.AlterColumn<int>(
            name: "Status",
            schema: "tenancy",
            table: "Projects",
            type: "integer",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "integer",
            oldDefaultValue: 1);

        migrationBuilder.AlterColumn<int>(
            name: "Level",
            schema: "tasks",
            table: "Issues",
            type: "integer",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "integer",
            oldDefaultValue: 3);

        migrationBuilder.AlterColumn<int>(
            name: "SignOffStatus",
            schema: "tasks",
            table: "Deliverables",
            type: "integer",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "integer",
            oldDefaultValue: 1);

        migrationBuilder.AlterColumn<int>(
            name: "Level",
            schema: "tasks",
            table: "Deliverables",
            type: "integer",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "integer",
            oldDefaultValue: 3);

        migrationBuilder.AlterColumn<int>(
            name: "Level",
            schema: "calendar",
            table: "calendars",
            type: "integer",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "integer",
            oldDefaultValue: 3);

        migrationBuilder.AlterColumn<int>(
            name: "RetentionDays",
            schema: "app",
            table: "AuditRecords",
            type: "integer",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "integer",
            oldDefaultValue: 90);

        migrationBuilder.AlterColumn<int>(
            name: "Level",
            schema: "tasks",
            table: "Assumptions",
            type: "integer",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "integer",
            oldDefaultValue: 3);

        migrationBuilder.AlterColumn<int>(
            name: "Level",
            schema: "tasks",
            table: "ActivityTasks",
            type: "integer",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "integer",
            oldDefaultValue: 3);

        migrationBuilder.AddColumn<Guid>(
            name: "AssumptionEntityId",
            schema: "tasks",
            table: "ActionItems",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "DeliverableEntityId",
            schema: "tasks",
            table: "ActionItems",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "IssueEntityId",
            schema: "tasks",
            table: "ActionItems",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "RiskEntityId",
            schema: "tasks",
            table: "ActionItems",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "SubTaskEntityId",
            schema: "tasks",
            table: "ActionItems",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "TaskEntityId",
            schema: "tasks",
            table: "ActionItems",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "WorkDependencyEntityId",
            schema: "tasks",
            table: "ActionItems",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "WorkflowEntityId",
            schema: "tasks",
            table: "ActionItems",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "DocumentParsingConfigs",
            schema: "core",
            columns: table => new
            {
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                DocumentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                ModelBlobPath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                ParsingRulesJson = table.Column<string>(type: "jsonb", nullable: false),
                MinConfidenceThreshold = table.Column<double>(type: "double precision", nullable: false),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<string>(type: "text", nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                Level = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DocumentParsingConfigs", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "DocumentParsingResults",
            schema: "core",
            columns: table => new
            {
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ImportFileId = table.Column<Guid>(type: "uuid", nullable: false),
                DocumentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                ExtractedDataJson = table.Column<string>(type: "jsonb", nullable: false),
                ConfidenceScore = table.Column<double>(type: "double precision", nullable: false),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<string>(type: "text", nullable: true),
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
                table.PrimaryKey("PK_DocumentParsingResults", x => x.Id);
                table.ForeignKey(
                    name: "FK_DocumentParsingResults_ImportFiles_ImportFileId",
                    column: x => x.ImportFileId,
                    principalSchema: "imports",
                    principalTable: "ImportFiles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "JsonComparisonResults",
            schema: "core",
            columns: table => new
            {
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                SourceFileId = table.Column<Guid>(type: "uuid", nullable: false),
                TargetFileId = table.Column<Guid>(type: "uuid", nullable: false),
                DiffResultJson = table.Column<string>(type: "jsonb", nullable: false),
                SummaryMetricsJson = table.Column<string>(type: "jsonb", nullable: false),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<string>(type: "text", nullable: true),
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
                table.PrimaryKey("PK_JsonComparisonResults", x => x.Id);
                table.ForeignKey(
                    name: "FK_JsonComparisonResults_ImportFiles_SourceFileId",
                    column: x => x.SourceFileId,
                    principalSchema: "imports",
                    principalTable: "ImportFiles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_JsonComparisonResults_ImportFiles_TargetFileId",
                    column: x => x.TargetFileId,
                    principalSchema: "imports",
                    principalTable: "ImportFiles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ActionItems_AssumptionEntityId",
            schema: "tasks",
            table: "ActionItems",
            column: "AssumptionEntityId");

        migrationBuilder.CreateIndex(
            name: "IX_ActionItems_DeliverableEntityId",
            schema: "tasks",
            table: "ActionItems",
            column: "DeliverableEntityId");

        migrationBuilder.CreateIndex(
            name: "IX_ActionItems_IssueEntityId",
            schema: "tasks",
            table: "ActionItems",
            column: "IssueEntityId");

        migrationBuilder.CreateIndex(
            name: "IX_ActionItems_RiskEntityId",
            schema: "tasks",
            table: "ActionItems",
            column: "RiskEntityId");

        migrationBuilder.CreateIndex(
            name: "IX_ActionItems_SubTaskEntityId",
            schema: "tasks",
            table: "ActionItems",
            column: "SubTaskEntityId");

        migrationBuilder.CreateIndex(
            name: "IX_ActionItems_TaskEntityId",
            schema: "tasks",
            table: "ActionItems",
            column: "TaskEntityId");

        migrationBuilder.CreateIndex(
            name: "IX_ActionItems_WorkDependencyEntityId",
            schema: "tasks",
            table: "ActionItems",
            column: "WorkDependencyEntityId");

        migrationBuilder.CreateIndex(
            name: "IX_ActionItems_WorkflowEntityId",
            schema: "tasks",
            table: "ActionItems",
            column: "WorkflowEntityId");

        migrationBuilder.CreateIndex(
            name: "IX_DocumentParsingConfigs_TenantId",
            schema: "core",
            table: "DocumentParsingConfigs",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "IX_DocumentParsingConfigs_TenantId_DocumentType",
            schema: "core",
            table: "DocumentParsingConfigs",
            columns: new[] { "TenantId", "DocumentType" });

        migrationBuilder.CreateIndex(
            name: "IX_DocumentParsingConfigs_TenantId_IsDeleted",
            schema: "core",
            table: "DocumentParsingConfigs",
            columns: new[] { "TenantId", "IsDeleted" });

        migrationBuilder.CreateIndex(
            name: "IX_DocumentParsingResults_ImportFileId",
            schema: "core",
            table: "DocumentParsingResults",
            column: "ImportFileId");

        migrationBuilder.CreateIndex(
            name: "IX_DocumentParsingResults_TenantId",
            schema: "core",
            table: "DocumentParsingResults",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "IX_DocumentParsingResults_TenantId_DocumentType",
            schema: "core",
            table: "DocumentParsingResults",
            columns: new[] { "TenantId", "DocumentType" });

        migrationBuilder.CreateIndex(
            name: "IX_DocumentParsingResults_TenantId_IsDeleted",
            schema: "core",
            table: "DocumentParsingResults",
            columns: new[] { "TenantId", "IsDeleted" });

        migrationBuilder.CreateIndex(
            name: "IX_DocumentParsingResults_TenantId_ProjectId",
            schema: "core",
            table: "DocumentParsingResults",
            columns: new[] { "TenantId", "ProjectId" });

        migrationBuilder.CreateIndex(
            name: "IX_JsonComparisonResults_SourceFileId",
            schema: "core",
            table: "JsonComparisonResults",
            column: "SourceFileId");

        migrationBuilder.CreateIndex(
            name: "IX_JsonComparisonResults_TargetFileId",
            schema: "core",
            table: "JsonComparisonResults",
            column: "TargetFileId");

        migrationBuilder.CreateIndex(
            name: "IX_JsonComparisonResults_TenantId",
            schema: "core",
            table: "JsonComparisonResults",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "IX_JsonComparisonResults_TenantId_IsDeleted",
            schema: "core",
            table: "JsonComparisonResults",
            columns: new[] { "TenantId", "IsDeleted" });

        migrationBuilder.CreateIndex(
            name: "IX_JsonComparisonResults_TenantId_ProjectId",
            schema: "core",
            table: "JsonComparisonResults",
            columns: new[] { "TenantId", "ProjectId" });

        migrationBuilder.AddForeignKey(
            name: "FK_ActionItems_Assumptions_AssumptionEntityId",
            schema: "tasks",
            table: "ActionItems",
            column: "AssumptionEntityId",
            principalSchema: "tasks",
            principalTable: "Assumptions",
            principalColumn: "Id");

        migrationBuilder.AddForeignKey(
            name: "FK_ActionItems_Deliverables_DeliverableEntityId",
            schema: "tasks",
            table: "ActionItems",
            column: "DeliverableEntityId",
            principalSchema: "tasks",
            principalTable: "Deliverables",
            principalColumn: "Id");

        migrationBuilder.AddForeignKey(
            name: "FK_ActionItems_Issues_IssueEntityId",
            schema: "tasks",
            table: "ActionItems",
            column: "IssueEntityId",
            principalSchema: "tasks",
            principalTable: "Issues",
            principalColumn: "Id");

        migrationBuilder.AddForeignKey(
            name: "FK_ActionItems_Risks_RiskEntityId",
            schema: "tasks",
            table: "ActionItems",
            column: "RiskEntityId",
            principalSchema: "tasks",
            principalTable: "Risks",
            principalColumn: "Id");

        migrationBuilder.AddForeignKey(
            name: "FK_ActionItems_SubTasks_SubTaskEntityId",
            schema: "tasks",
            table: "ActionItems",
            column: "SubTaskEntityId",
            principalSchema: "tasks",
            principalTable: "SubTasks",
            principalColumn: "Id");

        migrationBuilder.AddForeignKey(
            name: "FK_ActionItems_WorkDependencies_WorkDependencyEntityId",
            schema: "tasks",
            table: "ActionItems",
            column: "WorkDependencyEntityId",
            principalSchema: "tasks",
            principalTable: "WorkDependencies",
            principalColumn: "Id");

        migrationBuilder.AddForeignKey(
            name: "FK_ActionItems_Workflows_WorkflowEntityId",
            schema: "tasks",
            table: "ActionItems",
            column: "WorkflowEntityId",
            principalSchema: "tasks",
            principalTable: "Workflows",
            principalColumn: "Id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_ActionItems_Assumptions_AssumptionEntityId",
            schema: "tasks",
            table: "ActionItems");

        migrationBuilder.DropForeignKey(
            name: "FK_ActionItems_Deliverables_DeliverableEntityId",
            schema: "tasks",
            table: "ActionItems");

        migrationBuilder.DropForeignKey(
            name: "FK_ActionItems_Issues_IssueEntityId",
            schema: "tasks",
            table: "ActionItems");

        migrationBuilder.DropForeignKey(
            name: "FK_ActionItems_Risks_RiskEntityId",
            schema: "tasks",
            table: "ActionItems");

        migrationBuilder.DropForeignKey(
            name: "FK_ActionItems_SubTasks_SubTaskEntityId",
            schema: "tasks",
            table: "ActionItems");

        migrationBuilder.DropForeignKey(
            name: "FK_ActionItems_WorkDependencies_WorkDependencyEntityId",
            schema: "tasks",
            table: "ActionItems");

        migrationBuilder.DropForeignKey(
            name: "FK_ActionItems_Workflows_WorkflowEntityId",
            schema: "tasks",
            table: "ActionItems");

        migrationBuilder.DropTable(
            name: "DocumentParsingConfigs",
            schema: "core");

        migrationBuilder.DropTable(
            name: "DocumentParsingResults",
            schema: "core");

        migrationBuilder.DropTable(
            name: "JsonComparisonResults",
            schema: "core");

        migrationBuilder.DropIndex(
            name: "IX_ActionItems_AssumptionEntityId",
            schema: "tasks",
            table: "ActionItems");

        migrationBuilder.DropIndex(
            name: "IX_ActionItems_DeliverableEntityId",
            schema: "tasks",
            table: "ActionItems");

        migrationBuilder.DropIndex(
            name: "IX_ActionItems_IssueEntityId",
            schema: "tasks",
            table: "ActionItems");

        migrationBuilder.DropIndex(
            name: "IX_ActionItems_RiskEntityId",
            schema: "tasks",
            table: "ActionItems");

        migrationBuilder.DropIndex(
            name: "IX_ActionItems_SubTaskEntityId",
            schema: "tasks",
            table: "ActionItems");

        migrationBuilder.DropIndex(
            name: "IX_ActionItems_TaskEntityId",
            schema: "tasks",
            table: "ActionItems");

        migrationBuilder.DropIndex(
            name: "IX_ActionItems_WorkDependencyEntityId",
            schema: "tasks",
            table: "ActionItems");

        migrationBuilder.DropIndex(
            name: "IX_ActionItems_WorkflowEntityId",
            schema: "tasks",
            table: "ActionItems");

        migrationBuilder.DropColumn(
            name: "AssumptionEntityId",
            schema: "tasks",
            table: "ActionItems");

        migrationBuilder.DropColumn(
            name: "DeliverableEntityId",
            schema: "tasks",
            table: "ActionItems");

        migrationBuilder.DropColumn(
            name: "IssueEntityId",
            schema: "tasks",
            table: "ActionItems");

        migrationBuilder.DropColumn(
            name: "RiskEntityId",
            schema: "tasks",
            table: "ActionItems");

        migrationBuilder.DropColumn(
            name: "SubTaskEntityId",
            schema: "tasks",
            table: "ActionItems");

        migrationBuilder.DropColumn(
            name: "TaskEntityId",
            schema: "tasks",
            table: "ActionItems");

        migrationBuilder.DropColumn(
            name: "WorkDependencyEntityId",
            schema: "tasks",
            table: "ActionItems");

        migrationBuilder.DropColumn(
            name: "WorkflowEntityId",
            schema: "tasks",
            table: "ActionItems");

        migrationBuilder.AlterColumn<int>(
            name: "Level",
            schema: "tasks",
            table: "WorkflowTasks",
            type: "integer",
            nullable: false,
            defaultValue: 3,
            oldClrType: typeof(int),
            oldType: "integer");

        migrationBuilder.AlterColumn<int>(
            name: "Level",
            schema: "tasks",
            table: "Workflows",
            type: "integer",
            nullable: false,
            defaultValue: 3,
            oldClrType: typeof(int),
            oldType: "integer");

        migrationBuilder.AlterColumn<int>(
            name: "Level",
            schema: "tasks",
            table: "WorkDependencies",
            type: "integer",
            nullable: false,
            defaultValue: 3,
            oldClrType: typeof(int),
            oldType: "integer");

        migrationBuilder.AlterColumn<string>(
            name: "UserKind",
            schema: "identity",
            table: "Users",
            type: "character varying(32)",
            maxLength: 32,
            nullable: false,
            defaultValue: "Human",
            oldClrType: typeof(string),
            oldType: "character varying(32)",
            oldMaxLength: 32);

        migrationBuilder.AlterColumn<int>(
            name: "Level",
            schema: "tasks",
            table: "SubTasks",
            type: "integer",
            nullable: false,
            defaultValue: 3,
            oldClrType: typeof(int),
            oldType: "integer");

        migrationBuilder.AlterColumn<int>(
            name: "Level",
            schema: "tasks",
            table: "SprintTasks",
            type: "integer",
            nullable: false,
            defaultValue: 3,
            oldClrType: typeof(int),
            oldType: "integer");

        migrationBuilder.AlterColumn<int>(
            name: "Level",
            schema: "tasks",
            table: "Risks",
            type: "integer",
            nullable: false,
            defaultValue: 3,
            oldClrType: typeof(int),
            oldType: "integer");

        migrationBuilder.AlterColumn<int>(
            name: "Status",
            schema: "tenancy",
            table: "Projects",
            type: "integer",
            nullable: false,
            defaultValue: 1,
            oldClrType: typeof(int),
            oldType: "integer");

        migrationBuilder.AlterColumn<int>(
            name: "Level",
            schema: "tasks",
            table: "Issues",
            type: "integer",
            nullable: false,
            defaultValue: 3,
            oldClrType: typeof(int),
            oldType: "integer");

        migrationBuilder.AlterColumn<int>(
            name: "SignOffStatus",
            schema: "tasks",
            table: "Deliverables",
            type: "integer",
            nullable: false,
            defaultValue: 1,
            oldClrType: typeof(int),
            oldType: "integer");

        migrationBuilder.AlterColumn<int>(
            name: "Level",
            schema: "tasks",
            table: "Deliverables",
            type: "integer",
            nullable: false,
            defaultValue: 3,
            oldClrType: typeof(int),
            oldType: "integer");

        migrationBuilder.AlterColumn<int>(
            name: "Level",
            schema: "calendar",
            table: "calendars",
            type: "integer",
            nullable: false,
            defaultValue: 3,
            oldClrType: typeof(int),
            oldType: "integer");

        migrationBuilder.AlterColumn<int>(
            name: "RetentionDays",
            schema: "app",
            table: "AuditRecords",
            type: "integer",
            nullable: false,
            defaultValue: 90,
            oldClrType: typeof(int),
            oldType: "integer");

        migrationBuilder.AlterColumn<int>(
            name: "Level",
            schema: "tasks",
            table: "Assumptions",
            type: "integer",
            nullable: false,
            defaultValue: 3,
            oldClrType: typeof(int),
            oldType: "integer");

        migrationBuilder.AlterColumn<int>(
            name: "Level",
            schema: "tasks",
            table: "ActivityTasks",
            type: "integer",
            nullable: false,
            defaultValue: 3,
            oldClrType: typeof(int),
            oldType: "integer");
    }
}
