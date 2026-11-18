using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorEnterprise.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261118202957_ScanFindingsEntity : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ScanFindings",
            schema: "app",
            columns: table => new
            {
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ImportFileId = table.Column<Guid>(type: "uuid", nullable: false),
                DocumentParsingResultId = table.Column<Guid>(type: "uuid", nullable: true),
                DocumentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                CustomerName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                SourceVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                TargetVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                ProjectManager = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                Comment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                OrderIndex = table.Column<int>(type: "integer", nullable: false),
                EncryptedPayloadBytes = table.Column<byte[]>(type: "bytea", nullable: true),
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
                table.PrimaryKey("PK_ScanFindings", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ScanFindings_DocumentParsingResultId",
            schema: "app",
            table: "ScanFindings",
            column: "DocumentParsingResultId");

        migrationBuilder.CreateIndex(
            name: "IX_ScanFindings_TenantId",
            schema: "app",
            table: "ScanFindings",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "IX_ScanFindings_TenantId_ProjectId_ImportFileId",
            schema: "app",
            table: "ScanFindings",
            columns: new[] { "TenantId", "ProjectId", "ImportFileId" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ScanFindings",
            schema: "app");
    }
}
