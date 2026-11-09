using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorEnterprise.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditRecordRetentionDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RetentionDays",
                schema: "app",
                table: "AuditRecords",
                type: "integer",
                nullable: false,
                defaultValue: 90);

            // Backfill existing Security audit records to 365 days
            migrationBuilder.Sql("UPDATE app.\"AuditRecords\" SET \"RetentionDays\" = 365 WHERE \"EventType\" = 2;");

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_RetentionDays_Created",
                schema: "app",
                table: "AuditRecords",
                columns: new[] { "RetentionDays", "Created" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditRecords_RetentionDays_Created",
                schema: "app",
                table: "AuditRecords");

            migrationBuilder.DropColumn(
                name: "RetentionDays",
                schema: "app",
                table: "AuditRecords");
        }
    }
}
