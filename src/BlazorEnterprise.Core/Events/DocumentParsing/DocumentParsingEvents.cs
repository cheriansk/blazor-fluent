using BlazorEnterprise.Core.Events;

namespace BlazorEnterprise.Core.Events.DocumentParsing;

/// <summary>
/// Background job event queued when a Word (.docx) document is uploaded and ready for local ML.NET + OpenXML parsing.
/// </summary>
public sealed record DocumentParsingJobEvent(
    Guid ImportFileId,
    Guid? ProjectId,
    string ExpectedDocumentType,
    Guid TenantId,
    string CorrelationId,
    string SenderOrigin,
    string SenderUserId,
    string SenderUserEmail)
    : BaseJobEvent("DocumentParsing", TenantId, CorrelationId, SenderOrigin, SenderUserId, SenderUserEmail, ProjectId);

/// <summary>
/// Background job event queued to compare two parsed document JSON payloads via RFC 6902 diffing.
/// </summary>
public sealed record JsonComparisonJobEvent(
    Guid SourceFileId,
    Guid TargetFileId,
    Guid? ProjectId,
    Guid TenantId,
    string CorrelationId,
    string SenderOrigin,
    string SenderUserId,
    string SenderUserEmail)
    : BaseJobEvent("DocumentParsing", TenantId, CorrelationId, SenderOrigin, SenderUserId, SenderUserEmail, ProjectId);
