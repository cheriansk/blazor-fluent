using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorEnterprise.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPostgresXminConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "tasks",
                table: "Workflows",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "tasks",
                table: "WorkDependencies",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "identity",
                table: "UserSessions",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "identity",
                table: "Users",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "calendar",
                table: "UserCalendars",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "tenancy",
                table: "Tenants",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "tenancy",
                table: "tenant_users",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "tasks",
                table: "Tasks",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "app",
                table: "TaskComments",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "tasks",
                table: "SubTasks",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "staging",
                table: "StagedImports",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "tasks",
                table: "Risks",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "tenancy",
                table: "ProjectUserRoles",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "tenancy",
                table: "Projects",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "calendar",
                table: "ProjectCalendars",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "tenancy",
                table: "programs",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "calendar",
                table: "ProgramCalendars",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "tenancy",
                table: "program_user_roles",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "app",
                table: "Notifications",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "app",
                table: "KnowledgeArticles",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "app",
                table: "KnowledgeArticleReviewers",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "JobExecutions",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "tasks",
                table: "Issues",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "imports",
                table: "ImportFiles",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "identity",
                table: "ImpersonationGrants",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "app",
                table: "EventPublishTrackers",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "app",
                table: "EventConsumptionTrackers",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "app",
                table: "AuditRecords",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "tasks",
                table: "Assumptions",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "tasks",
                table: "Workflows");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "tasks",
                table: "WorkDependencies");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "identity",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "calendar",
                table: "UserCalendars");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "tenancy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "tenancy",
                table: "tenant_users");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "tasks",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "app",
                table: "TaskComments");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "tasks",
                table: "SubTasks");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "staging",
                table: "StagedImports");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "tasks",
                table: "Risks");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "tenancy",
                table: "ProjectUserRoles");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "tenancy",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "calendar",
                table: "ProjectCalendars");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "tenancy",
                table: "programs");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "calendar",
                table: "ProgramCalendars");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "tenancy",
                table: "program_user_roles");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "app",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "app",
                table: "KnowledgeArticles");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "app",
                table: "KnowledgeArticleReviewers");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "JobExecutions");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "tasks",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "imports",
                table: "ImportFiles");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "identity",
                table: "ImpersonationGrants");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "app",
                table: "EventPublishTrackers");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "app",
                table: "EventConsumptionTrackers");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "app",
                table: "AuditRecords");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "tasks",
                table: "Assumptions");
        }
    }
}
