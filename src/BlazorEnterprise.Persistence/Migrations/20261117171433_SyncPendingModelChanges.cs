using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorEnterprise.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261117171433_SyncPendingModelChanges : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "FinalizedAtUtc",
            schema: "core",
            table: "DocumentParsingResults",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "FinalizedBy",
            schema: "core",
            table: "DocumentParsingResults",
            type: "character varying(256)",
            maxLength: 256,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "Status",
            schema: "core",
            table: "DocumentParsingResults",
            type: "integer",
            nullable: false,
            defaultValue: 0);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "FinalizedAtUtc",
            schema: "core",
            table: "DocumentParsingResults");

        migrationBuilder.DropColumn(
            name: "FinalizedBy",
            schema: "core",
            table: "DocumentParsingResults");

        migrationBuilder.DropColumn(
            name: "Status",
            schema: "core",
            table: "DocumentParsingResults");
    }
}
