using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorFluent.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ImportFileEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "imports");

            migrationBuilder.EnsureSchema(
                name: "staging");

            migrationBuilder.CreateTable(
                name: "ImportFiles",
                schema: "imports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageFolder = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false, defaultValue: ""),
                    StepStage = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "None"),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    BlobUri = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "application/octet-stream"),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256Checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FileType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TotalRowsCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ValidRowsCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ErrorRowsCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ValidationErrorsJson = table.Column<string>(type: "text", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TenantId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportFiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StagedTasks",
                schema: "staging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    SheetName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "Tasks"),
                    RowIndex = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    Priority = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Medium"),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Open"),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AssigneeEmails = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Labels = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ValidationStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Valid"),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TenantId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StagedTasks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImportFiles_TenantId",
                schema: "imports",
                table: "ImportFiles",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportFiles_TenantId_ImportId",
                schema: "imports",
                table: "ImportFiles",
                columns: new[] { "TenantId", "ImportId" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportFiles_TenantId_ProjectId_Status",
                schema: "imports",
                table: "ImportFiles",
                columns: new[] { "TenantId", "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportFiles_TenantId_Sha256Checksum",
                schema: "imports",
                table: "ImportFiles",
                columns: new[] { "TenantId", "Sha256Checksum" });

            migrationBuilder.CreateIndex(
                name: "IX_StagedTasks_TenantId",
                schema: "staging",
                table: "StagedTasks",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StagedTasks_TenantId_ImportFileId_ValidationStatus",
                schema: "staging",
                table: "StagedTasks",
                columns: new[] { "TenantId", "ImportFileId", "ValidationStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_StagedTasks_TenantId_ImportId",
                schema: "staging",
                table: "StagedTasks",
                columns: new[] { "TenantId", "ImportId" });

            migrationBuilder.CreateIndex(
                name: "IX_StagedTasks_TenantId_ProjectId",
                schema: "staging",
                table: "StagedTasks",
                columns: new[] { "TenantId", "ProjectId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportFiles",
                schema: "imports");

            migrationBuilder.DropTable(
                name: "StagedTasks",
                schema: "staging");
        }
    }
}
