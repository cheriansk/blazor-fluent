using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorFluent.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEventTrackingSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventPublishTrackers",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EventName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EventTypeFullName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    SourceClass = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SourceMethod = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SourceFilePath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    UserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UserEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TriggerSource = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ConsumerCount = table.Column<int>(type: "integer", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventPublishTrackers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EventConsumptionTrackers",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EventPublishTrackerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ConsumerClass = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ConsumerMethod = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DurationMs = table.Column<double>(type: "double precision", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ExceptionDetails = table.Column<string>(type: "text", nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventConsumptionTrackers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventConsumptionTrackers_EventPublishTrackers_EventPublishT~",
                        column: x => x.EventPublishTrackerId,
                        principalSchema: "app",
                        principalTable: "EventPublishTrackers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventConsumptionTrackers_ConsumerClass",
                schema: "app",
                table: "EventConsumptionTrackers",
                column: "ConsumerClass");

            migrationBuilder.CreateIndex(
                name: "IX_EventConsumptionTrackers_PublishTrackerId",
                schema: "app",
                table: "EventConsumptionTrackers",
                column: "EventPublishTrackerId");

            migrationBuilder.CreateIndex(
                name: "IX_EventConsumptionTrackers_Status",
                schema: "app",
                table: "EventConsumptionTrackers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_EventConsumptionTrackers_TenantId",
                schema: "app",
                table: "EventConsumptionTrackers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EventPublishTrackers_CorrelationId",
                schema: "app",
                table: "EventPublishTrackers",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_EventPublishTrackers_EventId",
                schema: "app",
                table: "EventPublishTrackers",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_EventPublishTrackers_EventName",
                schema: "app",
                table: "EventPublishTrackers",
                column: "EventName");

            migrationBuilder.CreateIndex(
                name: "IX_EventPublishTrackers_Status",
                schema: "app",
                table: "EventPublishTrackers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_EventPublishTrackers_TenantId",
                schema: "app",
                table: "EventPublishTrackers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EventPublishTrackers_TenantId_PublishedAtUtc",
                schema: "app",
                table: "EventPublishTrackers",
                columns: new[] { "TenantId", "PublishedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventConsumptionTrackers",
                schema: "app");

            migrationBuilder.DropTable(
                name: "EventPublishTrackers",
                schema: "app");
        }
    }
}
