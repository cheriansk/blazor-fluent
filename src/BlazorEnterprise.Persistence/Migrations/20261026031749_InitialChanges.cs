using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BlazorEnterprise.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "tasks");

            migrationBuilder.EnsureSchema(
                name: "app");

            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.EnsureSchema(
                name: "imports");

            migrationBuilder.EnsureSchema(
                name: "tenancy");

            migrationBuilder.EnsureSchema(
                name: "calendar");

            migrationBuilder.EnsureSchema(
                name: "staging");

            migrationBuilder.CreateTable(
                name: "Assumptions",
                schema: "tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MilestoneId = table.Column<Guid>(type: "uuid", nullable: true),
                    ValidationCriteria = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true, defaultValue: ""),
                    ValidationStatus = table.Column<int>(type: "integer", nullable: false),
                    ImpactIfInvalid = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true, defaultValue: ""),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevisedDueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReporterEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false, defaultValue: ""),
                    AssigneeEmails = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true, defaultValue: ""),
                    WatcherEmails = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true, defaultValue: ""),
                    Labels = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, defaultValue: ""),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SourceRowId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsEncryptedAtRest = table.Column<bool>(type: "boolean", nullable: false),
                    EncryptedPayloadBytes = table.Column<byte[]>(type: "bytea", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assumptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditRecords",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UserEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UserType = table.Column<int>(type: "integer", nullable: true),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    EntityName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EntityId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Operation = table.Column<int>(type: "integer", nullable: true),
                    ChangesJson = table.Column<string>(type: "jsonb", nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TraceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FriendlyName = table.Column<string>(type: "text", nullable: true),
                    Xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EventPublishTrackers",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventPublishTrackers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImpersonationGrants",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceAdminId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SourceAdminEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TargetUserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TargetUserEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TargetTenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImpersonationGrants", x => x.Id);
                });

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
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportFiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Issues",
                schema: "tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MilestoneId = table.Column<Guid>(type: "uuid", nullable: true),
                    RootCause = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true, defaultValue: ""),
                    Workaround = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true, defaultValue: ""),
                    ResolutionStrategy = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true, defaultValue: ""),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevisedDueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReporterEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false, defaultValue: ""),
                    AssigneeEmails = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true, defaultValue: ""),
                    WatcherEmails = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true, defaultValue: ""),
                    Labels = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, defaultValue: ""),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SourceRowId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsEncryptedAtRest = table.Column<bool>(type: "boolean", nullable: false),
                    EncryptedPayloadBytes = table.Column<byte[]>(type: "bytea", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Issues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ParentExecutionId = table.Column<Guid>(type: "uuid", nullable: true),
                    JobName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TriggerSource = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    MaxRetries = table.Column<int>(type: "integer", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DurationMs = table.Column<double>(type: "double precision", nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobExecutions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    LinkUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ReadAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentToTeams = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TeamsSentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentToMailbox = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    MailboxSentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Risks",
                schema: "tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MilestoneId = table.Column<Guid>(type: "uuid", nullable: true),
                    Impact = table.Column<int>(type: "integer", nullable: false),
                    Probability = table.Column<int>(type: "integer", nullable: false),
                    MitigationStrategy = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true, defaultValue: ""),
                    ContingencyPlan = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true, defaultValue: ""),
                    RiskStatus = table.Column<int>(type: "integer", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevisedDueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReporterEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false, defaultValue: ""),
                    AssigneeEmails = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true, defaultValue: ""),
                    WatcherEmails = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true, defaultValue: ""),
                    Labels = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, defaultValue: ""),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SourceRowId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsEncryptedAtRest = table.Column<bool>(type: "boolean", nullable: false),
                    EncryptedPayloadBytes = table.Column<byte[]>(type: "bytea", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Risks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StagedImports",
                schema: "staging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SheetName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "Default"),
                    ChunkIndex = table.Column<int>(type: "integer", nullable: false),
                    TotalChunks = table.Column<int>(type: "integer", nullable: false),
                    RowCount = table.Column<int>(type: "integer", nullable: false),
                    IsProcessed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Staged"),
                    ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StagedImports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                schema: "tenancy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    InternalEmailDomains = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, defaultValue: ""),
                    ExternalEmailDomains = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, defaultValue: ""),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UserType = table.Column<int>(type: "integer", nullable: false),
                    UserKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "Human"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    StartDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RowSignature = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TimeZoneId = table.Column<string>(type: "text", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    PhysicalAddress_City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PhysicalAddress_Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PhysicalAddress_PostalCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PhysicalAddress_State = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PhysicalAddress_Street = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserSessions",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UserEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastActivityAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkDependencies",
                schema: "tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DependentType = table.Column<int>(type: "integer", nullable: false),
                    DependentItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    DependsOnType = table.Column<int>(type: "integer", nullable: false),
                    DependsOnItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    DependsOnLevel = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    DependsOnProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    DependsOnProgramId = table.Column<Guid>(type: "uuid", nullable: true),
                    DependsOnSnapshotTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DependsOnSnapshotStatus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DependsOnSnapshotDueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsScheduleConflict = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DependencyType = table.Column<int>(type: "integer", nullable: false),
                    MilestoneId = table.Column<Guid>(type: "uuid", nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevisedDueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReporterEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, defaultValue: ""),
                    AssigneeEmails = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true, defaultValue: ""),
                    WatcherEmails = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true, defaultValue: ""),
                    Labels = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, defaultValue: ""),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SourceRowId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsEncryptedAtRest = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    EncryptedPayloadBytes = table.Column<byte[]>(type: "bytea", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkDependencies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Workflows",
                schema: "tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentStepId = table.Column<Guid>(type: "uuid", nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevisedDueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReporterEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false, defaultValue: ""),
                    AssigneeEmails = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false, defaultValue: ""),
                    WatcherEmails = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true, defaultValue: ""),
                    Labels = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, defaultValue: ""),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    SourceRowId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsEncryptedAtRest = table.Column<bool>(type: "boolean", nullable: false),
                    EncryptedPayloadBytes = table.Column<byte[]>(type: "bytea", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workflows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EventConsumptionTrackers",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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

            migrationBuilder.CreateTable(
                name: "programs",
                schema: "tenancy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevisedDueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_programs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_programs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "tenancy",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tenant_users",
                schema: "tenancy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    RowSignature = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tenant_users_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "tenancy",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tenant_users_Users_UserEntityId",
                        column: x => x.UserEntityId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserCalendars",
                schema: "calendar",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true, defaultValue: ""),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VerNum = table.Column<int>(type: "integer", nullable: false),
                    EntryType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AssociatedTaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCalendars", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserCalendars_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "program_user_roles",
                schema: "tenancy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    RowSignature = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_program_user_roles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_program_user_roles_Users_UserEntityId",
                        column: x => x.UserEntityId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_program_user_roles_programs_ProgramId",
                        column: x => x.ProgramId,
                        principalSchema: "tenancy",
                        principalTable: "programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProgramCalendars",
                schema: "calendar",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true, defaultValue: ""),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VerNum = table.Column<int>(type: "integer", nullable: false),
                    EntryType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AssociatedTaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramCalendars", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgramCalendars_programs_ProgramId",
                        column: x => x.ProgramId,
                        principalSchema: "tenancy",
                        principalTable: "programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                schema: "tenancy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ShortCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Location = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    ScopeSummary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    TeamsWebhookUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevisedDueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Projects_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "tenancy",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Projects_programs_ProgramId",
                        column: x => x.ProgramId,
                        principalSchema: "tenancy",
                        principalTable: "programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

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
                    AuthorUserEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedByUserEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    table.ForeignKey(
                        name: "FK_KnowledgeArticles_Users_ApprovedByUserEntityId",
                        column: x => x.ApprovedByUserEntityId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KnowledgeArticles_Users_AuthorUserEntityId",
                        column: x => x.AuthorUserEntityId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectCalendars",
                schema: "calendar",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: true),
                    ParentProgramCalendarId = table.Column<Guid>(type: "uuid", nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true, defaultValue: ""),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VerNum = table.Column<int>(type: "integer", nullable: false),
                    EntryType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AssociatedTaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectCalendars", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectCalendars_ProgramCalendars_ParentProgramCalendarId",
                        column: x => x.ParentProgramCalendarId,
                        principalSchema: "calendar",
                        principalTable: "ProgramCalendars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectCalendars_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "tenancy",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectCalendars_programs_ProgramId",
                        column: x => x.ProgramId,
                        principalSchema: "tenancy",
                        principalTable: "programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProjectUserRoles",
                schema: "tenancy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    RowSignature = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectUserRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectUserRoles_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "tenancy",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectUserRoles_Users_UserEntityId",
                        column: x => x.UserEntityId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Tasks",
                schema: "tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    MilestoneId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkflowStepOrder = table.Column<int>(type: "integer", nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevisedDueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReporterEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false, defaultValue: ""),
                    AssigneeEmails = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true, defaultValue: ""),
                    WatcherEmails = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true, defaultValue: ""),
                    Labels = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, defaultValue: ""),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SourceRowId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsEncryptedAtRest = table.Column<bool>(type: "boolean", nullable: false),
                    EncryptedPayloadBytes = table.Column<byte[]>(type: "bytea", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tasks", x => x.Id);
                    table.CheckConstraint("CK_Tasks_WorkflowId_WorkflowStepOrder", "(\"WorkflowId\" IS NULL AND \"WorkflowStepOrder\" IS NULL) OR (\"WorkflowId\" IS NOT NULL AND \"WorkflowStepOrder\" IS NOT NULL AND \"WorkflowStepOrder\" > 0)");
                    table.ForeignKey(
                        name: "FK_Tasks_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "tenancy",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tasks_Workflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalSchema: "tasks",
                        principalTable: "Workflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Tasks_programs_ProgramId",
                        column: x => x.ProgramId,
                        principalSchema: "tenancy",
                        principalTable: "programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeArticleReviewers",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArticleId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    HasApproved = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.ForeignKey(
                        name: "FK_KnowledgeArticleReviewers_Users_UserEntityId",
                        column: x => x.UserEntityId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubTasks",
                schema: "tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    BlockedBySubTaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevisedDueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReporterEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false, defaultValue: ""),
                    AssigneeEmails = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true, defaultValue: ""),
                    WatcherEmails = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true, defaultValue: ""),
                    Labels = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, defaultValue: ""),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SourceRowId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsEncryptedAtRest = table.Column<bool>(type: "boolean", nullable: false),
                    EncryptedPayloadBytes = table.Column<byte[]>(type: "bytea", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubTasks_SubTasks_BlockedBySubTaskId",
                        column: x => x.BlockedBySubTaskId,
                        principalSchema: "tasks",
                        principalTable: "SubTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubTasks_Tasks_ParentTaskId",
                        column: x => x.ParentTaskId,
                        principalSchema: "tasks",
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskComments",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorUserEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommentText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskComments_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalSchema: "tasks",
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskComments_Users_AuthorUserEntityId",
                        column: x => x.AuthorUserEntityId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Assumptions_Tenant_Level_ValidationStatus",
                schema: "tasks",
                table: "Assumptions",
                columns: new[] { "TenantId", "Level", "ValidationStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_Assumptions_TenantId",
                schema: "tasks",
                table: "Assumptions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Assumptions_TenantId_Level",
                schema: "tasks",
                table: "Assumptions",
                columns: new[] { "TenantId", "Level" });

            migrationBuilder.CreateIndex(
                name: "IX_Assumptions_TenantId_ProgramId",
                schema: "tasks",
                table: "Assumptions",
                columns: new[] { "TenantId", "ProgramId" });

            migrationBuilder.CreateIndex(
                name: "IX_Assumptions_TenantId_ProjectId",
                schema: "tasks",
                table: "Assumptions",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_EventType",
                schema: "app",
                table: "AuditRecords",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_TenantId",
                schema: "app",
                table: "AuditRecords",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_TenantId_Created",
                schema: "app",
                table: "AuditRecords",
                columns: new[] { "TenantId", "Created" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_UserId",
                schema: "app",
                table: "AuditRecords",
                column: "UserId");

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

            migrationBuilder.CreateIndex(
                name: "IX_ImpersonationGrants_Admin_Revoked",
                schema: "identity",
                table: "ImpersonationGrants",
                columns: new[] { "SourceAdminId", "IsRevoked" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ImpersonationGrants_ExpiresAtUtc",
                schema: "identity",
                table: "ImpersonationGrants",
                column: "ExpiresAtUtc");

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
                name: "IX_Issues_Tenant_Level_Priority_Status",
                schema: "tasks",
                table: "Issues",
                columns: new[] { "TenantId", "Level", "Priority", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Issues_TenantId",
                schema: "tasks",
                table: "Issues",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Issues_TenantId_Level",
                schema: "tasks",
                table: "Issues",
                columns: new[] { "TenantId", "Level" });

            migrationBuilder.CreateIndex(
                name: "IX_Issues_TenantId_ProgramId",
                schema: "tasks",
                table: "Issues",
                columns: new[] { "TenantId", "ProgramId" });

            migrationBuilder.CreateIndex(
                name: "IX_Issues_TenantId_ProjectId",
                schema: "tasks",
                table: "Issues",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_JobExecutions_CorrelationId",
                table: "JobExecutions",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_JobExecutions_Status",
                table: "JobExecutions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_JobExecutions_TenantId",
                table: "JobExecutions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeArticleReviewers_ArticleId",
                schema: "app",
                table: "KnowledgeArticleReviewers",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeArticleReviewers_UserEntityId",
                schema: "app",
                table: "KnowledgeArticleReviewers",
                column: "UserEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeArticles_ApprovedByUserEntityId",
                schema: "app",
                table: "KnowledgeArticles",
                column: "ApprovedByUserEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeArticles_AuthorUserEntityId",
                schema: "app",
                table: "KnowledgeArticles",
                column: "AuthorUserEntityId");

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

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeArticles_TenantId_ProjectId",
                schema: "app",
                table: "KnowledgeArticles",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_Created",
                schema: "app",
                table: "Notifications",
                column: "Created");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_Tenant_Project_Category",
                schema: "app",
                table: "Notifications",
                columns: new[] { "TenantId", "ProjectId", "Category" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_Tenant_User_IsRead",
                schema: "app",
                table: "Notifications",
                columns: new[] { "TenantId", "UserId", "IsRead" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TenantId",
                schema: "app",
                table: "Notifications",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_program_user_roles_ProgramId_Role",
                schema: "tenancy",
                table: "program_user_roles",
                columns: new[] { "ProgramId", "Role" });

            migrationBuilder.CreateIndex(
                name: "IX_program_user_roles_TenantId",
                schema: "tenancy",
                table: "program_user_roles",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_program_user_roles_TenantId_ProgramId_UserEntityId",
                schema: "tenancy",
                table: "program_user_roles",
                columns: new[] { "TenantId", "ProgramId", "UserEntityId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_program_user_roles_UserEntityId",
                schema: "tenancy",
                table: "program_user_roles",
                column: "UserEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramCalendars_Program_Start_End",
                schema: "calendar",
                table: "ProgramCalendars",
                columns: new[] { "ProgramId", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProgramCalendars_Tenant_Program",
                schema: "calendar",
                table: "ProgramCalendars",
                columns: new[] { "TenantId", "ProgramId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProgramCalendars_TenantId",
                schema: "calendar",
                table: "ProgramCalendars",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_programs_Status",
                schema: "tenancy",
                table: "programs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_programs_TenantId",
                schema: "tenancy",
                table: "programs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_programs_TenantId_Code",
                schema: "tenancy",
                table: "programs",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_programs_TenantId_Name",
                schema: "tenancy",
                table: "programs",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCalendars_ParentProgramCalendarId",
                schema: "calendar",
                table: "ProjectCalendars",
                column: "ParentProgramCalendarId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCalendars_ProgramId",
                schema: "calendar",
                table: "ProjectCalendars",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCalendars_Project_Start_End",
                schema: "calendar",
                table: "ProjectCalendars",
                columns: new[] { "ProjectId", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCalendars_Tenant_Project",
                schema: "calendar",
                table: "ProjectCalendars",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCalendars_TenantId",
                schema: "calendar",
                table: "ProjectCalendars",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ProgramId",
                schema: "tenancy",
                table: "Projects",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_TenantId",
                schema: "tenancy",
                table: "Projects",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_TenantId_Name",
                schema: "tenancy",
                table: "Projects",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_TenantId_ShortCode",
                schema: "tenancy",
                table: "Projects",
                columns: new[] { "TenantId", "ShortCode" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"ShortCode\" IS NOT NULL AND \"ShortCode\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUserRoles_Project_Role",
                schema: "tenancy",
                table: "ProjectUserRoles",
                columns: new[] { "ProjectId", "Role" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUserRoles_Tenant_Project_User",
                schema: "tenancy",
                table: "ProjectUserRoles",
                columns: new[] { "TenantId", "ProjectId", "UserEntityId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUserRoles_TenantId",
                schema: "tenancy",
                table: "ProjectUserRoles",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectUserRoles_UserEntityId",
                schema: "tenancy",
                table: "ProjectUserRoles",
                column: "UserEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Risks_Tenant_Level_RiskStatus_DueDate",
                schema: "tasks",
                table: "Risks",
                columns: new[] { "TenantId", "Level", "RiskStatus", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Risks_TenantId",
                schema: "tasks",
                table: "Risks",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Risks_TenantId_Level",
                schema: "tasks",
                table: "Risks",
                columns: new[] { "TenantId", "Level" });

            migrationBuilder.CreateIndex(
                name: "IX_Risks_TenantId_ProgramId",
                schema: "tasks",
                table: "Risks",
                columns: new[] { "TenantId", "ProgramId" });

            migrationBuilder.CreateIndex(
                name: "IX_Risks_TenantId_ProjectId",
                schema: "tasks",
                table: "Risks",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_StagedImports_TenantId",
                schema: "staging",
                table: "StagedImports",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StagedImports_TenantId_ImportFileId_IsProcessed_ChunkIndex",
                schema: "staging",
                table: "StagedImports",
                columns: new[] { "TenantId", "ImportFileId", "IsProcessed", "ChunkIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_StagedImports_TenantId_ImportId",
                schema: "staging",
                table: "StagedImports",
                columns: new[] { "TenantId", "ImportId" });

            migrationBuilder.CreateIndex(
                name: "IX_StagedImports_TenantId_ProjectId",
                schema: "staging",
                table: "StagedImports",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_SubTasks_BlockedBySubTaskId",
                schema: "tasks",
                table: "SubTasks",
                column: "BlockedBySubTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_SubTasks_ParentTask_Order",
                schema: "tasks",
                table: "SubTasks",
                columns: new[] { "ParentTaskId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_SubTasks_ParentTaskId",
                schema: "tasks",
                table: "SubTasks",
                column: "ParentTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_SubTasks_Tenant_Status_DueDate",
                schema: "tasks",
                table: "SubTasks",
                columns: new[] { "TenantId", "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SubTasks_TenantId",
                schema: "tasks",
                table: "SubTasks",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskComments_AuthorUserEntityId",
                schema: "app",
                table: "TaskComments",
                column: "AuthorUserEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskComments_TaskId_CreatedAtUtc",
                schema: "app",
                table: "TaskComments",
                columns: new[] { "TaskId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskComments_Tenant_Project_Task",
                schema: "app",
                table: "TaskComments",
                columns: new[] { "TenantId", "ProjectId", "TaskId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskComments_TenantId",
                schema: "app",
                table: "TaskComments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_MilestoneId",
                schema: "tasks",
                table: "Tasks",
                column: "MilestoneId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_ProgramId",
                schema: "tasks",
                table: "Tasks",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_ProjectId",
                schema: "tasks",
                table: "Tasks",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_Tenant_Project_Status_DueDate",
                schema: "tasks",
                table: "Tasks",
                columns: new[] { "TenantId", "ProjectId", "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_TenantId",
                schema: "tasks",
                table: "Tasks",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_TenantId_Level",
                schema: "tasks",
                table: "Tasks",
                columns: new[] { "TenantId", "Level" });

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_TenantId_ProgramId",
                schema: "tasks",
                table: "Tasks",
                columns: new[] { "TenantId", "ProgramId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_TenantId_ProjectId",
                schema: "tasks",
                table: "Tasks",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_TenantId_WorkflowId_StepOrder",
                schema: "tasks",
                table: "Tasks",
                columns: new[] { "TenantId", "WorkflowId", "WorkflowStepOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_WorkflowId",
                schema: "tasks",
                table: "Tasks",
                column: "WorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_users_TenantId",
                schema: "tenancy",
                table: "tenant_users",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_users_TenantId_Role",
                schema: "tenancy",
                table: "tenant_users",
                columns: new[] { "TenantId", "Role" });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_users_TenantId_UserEntityId",
                schema: "tenancy",
                table: "tenant_users",
                columns: new[] { "TenantId", "UserEntityId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_users_UserEntityId",
                schema: "tenancy",
                table: "tenant_users",
                column: "UserEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Slug",
                schema: "tenancy",
                table: "Tenants",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserCalendars_Tenant_User",
                schema: "calendar",
                table: "UserCalendars",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserCalendars_TenantId",
                schema: "calendar",
                table: "UserCalendars",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCalendars_User_Start_End",
                schema: "calendar",
                table: "UserCalendars",
                columns: new[] { "UserId", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                schema: "identity",
                table: "Users",
                column: "Email",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Users_UserKind_IsActive",
                schema: "identity",
                table: "Users",
                columns: new[] { "UserKind", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_LastActivity_Active",
                schema: "identity",
                table: "UserSessions",
                column: "LastActivityAtUtc",
                filter: "\"IsRevoked\" = false AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_StartedAtUtc",
                schema: "identity",
                table: "UserSessions",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_Tenant_User_Revoked",
                schema: "identity",
                table: "UserSessions",
                columns: new[] { "TenantId", "UserId", "IsRevoked" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_TenantId",
                schema: "identity",
                table: "UserSessions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkDependencies_Tenant_DependentItem",
                schema: "tasks",
                table: "WorkDependencies",
                columns: new[] { "TenantId", "DependentType", "DependentItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkDependencies_Tenant_DependsOnItem",
                schema: "tasks",
                table: "WorkDependencies",
                columns: new[] { "TenantId", "DependsOnType", "DependsOnItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkDependencies_Tenant_DependsOnProgramId",
                schema: "tasks",
                table: "WorkDependencies",
                columns: new[] { "TenantId", "DependsOnProgramId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkDependencies_Tenant_DependsOnProjectId",
                schema: "tasks",
                table: "WorkDependencies",
                columns: new[] { "TenantId", "DependsOnProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkDependencies_Tenant_ProgramId",
                schema: "tasks",
                table: "WorkDependencies",
                columns: new[] { "TenantId", "ProgramId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkDependencies_Tenant_ProjectId",
                schema: "tasks",
                table: "WorkDependencies",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkDependencies_Tenant_ScheduleConflict",
                schema: "tasks",
                table: "WorkDependencies",
                columns: new[] { "TenantId", "IsScheduleConflict" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkDependencies_Tenant_Status_DueDate",
                schema: "tasks",
                table: "WorkDependencies",
                columns: new[] { "TenantId", "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkDependencies_TenantId",
                schema: "tasks",
                table: "WorkDependencies",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_TenantId",
                schema: "tasks",
                table: "Workflows",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_TenantId_AssigneeEmails",
                schema: "tasks",
                table: "Workflows",
                columns: new[] { "TenantId", "AssigneeEmails" });

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_TenantId_Level",
                schema: "tasks",
                table: "Workflows",
                columns: new[] { "TenantId", "Level" });

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_TenantId_ProgramId",
                schema: "tasks",
                table: "Workflows",
                columns: new[] { "TenantId", "ProgramId" });

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_TenantId_ProjectId_Status",
                schema: "tasks",
                table: "Workflows",
                columns: new[] { "TenantId", "ProjectId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Assumptions",
                schema: "tasks");

            migrationBuilder.DropTable(
                name: "AuditRecords",
                schema: "app");

            migrationBuilder.DropTable(
                name: "DataProtectionKeys");

            migrationBuilder.DropTable(
                name: "EventConsumptionTrackers",
                schema: "app");

            migrationBuilder.DropTable(
                name: "ImpersonationGrants",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "ImportFiles",
                schema: "imports");

            migrationBuilder.DropTable(
                name: "Issues",
                schema: "tasks");

            migrationBuilder.DropTable(
                name: "JobExecutions");

            migrationBuilder.DropTable(
                name: "KnowledgeArticleReviewers",
                schema: "app");

            migrationBuilder.DropTable(
                name: "Notifications",
                schema: "app");

            migrationBuilder.DropTable(
                name: "program_user_roles",
                schema: "tenancy");

            migrationBuilder.DropTable(
                name: "ProjectCalendars",
                schema: "calendar");

            migrationBuilder.DropTable(
                name: "ProjectUserRoles",
                schema: "tenancy");

            migrationBuilder.DropTable(
                name: "Risks",
                schema: "tasks");

            migrationBuilder.DropTable(
                name: "StagedImports",
                schema: "staging");

            migrationBuilder.DropTable(
                name: "SubTasks",
                schema: "tasks");

            migrationBuilder.DropTable(
                name: "TaskComments",
                schema: "app");

            migrationBuilder.DropTable(
                name: "tenant_users",
                schema: "tenancy");

            migrationBuilder.DropTable(
                name: "UserCalendars",
                schema: "calendar");

            migrationBuilder.DropTable(
                name: "UserSessions",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "WorkDependencies",
                schema: "tasks");

            migrationBuilder.DropTable(
                name: "EventPublishTrackers",
                schema: "app");

            migrationBuilder.DropTable(
                name: "KnowledgeArticles",
                schema: "app");

            migrationBuilder.DropTable(
                name: "ProgramCalendars",
                schema: "calendar");

            migrationBuilder.DropTable(
                name: "Tasks",
                schema: "tasks");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "Projects",
                schema: "tenancy");

            migrationBuilder.DropTable(
                name: "Workflows",
                schema: "tasks");

            migrationBuilder.DropTable(
                name: "programs",
                schema: "tenancy");

            migrationBuilder.DropTable(
                name: "Tenants",
                schema: "tenancy");
        }
    }
}
