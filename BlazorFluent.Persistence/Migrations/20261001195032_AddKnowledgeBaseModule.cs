using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorFluent.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeBaseModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KnowledgeArticles",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Content = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    Labels = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IsGlobal = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorUserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AuthorUserEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AuthorUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ApprovedByUserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ApprovedByUserEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ApprovedByUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_KnowledgeArticles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeArticles_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "tenancy",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeArticleReviewers",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArticleId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UserEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    HasApproved = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeArticleReviewers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeArticleReviewers_KnowledgeArticles_ArticleId",
                        column: x => x.ArticleId,
                        principalSchema: "app",
                        principalTable: "KnowledgeArticles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeArticleReviewers_ArticleId",
                schema: "app",
                table: "KnowledgeArticleReviewers",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeArticleReviewers_UserId",
                schema: "app",
                table: "KnowledgeArticleReviewers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeArticles_IsDeleted",
                schema: "app",
                table: "KnowledgeArticles",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeArticles_IsGlobal",
                schema: "app",
                table: "KnowledgeArticles",
                column: "IsGlobal");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeArticles_ProjectId",
                schema: "app",
                table: "KnowledgeArticles",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeArticles_Status",
                schema: "app",
                table: "KnowledgeArticles",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeArticles_TenantId",
                schema: "app",
                table: "KnowledgeArticles",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KnowledgeArticleReviewers",
                schema: "app");

            migrationBuilder.DropTable(
                name: "KnowledgeArticles",
                schema: "app");
        }
    }
}
