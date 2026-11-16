using FluentValidation;

namespace BlazorEnterprise.Core.Domain.DocumentParsing;

/// <summary>
/// Tier-2 entity validator for <see cref="DocumentParsingConfigEntity"/>.
/// </summary>
public class DocumentParsingConfigEntityValidator : AbstractValidator<DocumentParsingConfigEntity>
{
    public DocumentParsingConfigEntityValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.DocumentType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ModelBlobPath).NotEmpty().MaximumLength(1024);
        RuleFor(x => x.ParsingRulesJson).NotEmpty();
        RuleFor(x => x.MinConfidenceThreshold).InclusiveBetween(0.0, 1.0);
    }
}

/// <summary>
/// Tier-2 entity validator for <see cref="DocumentParsingResultEntity"/>.
/// </summary>
public class DocumentParsingResultEntityValidator : AbstractValidator<DocumentParsingResultEntity>
{
    public DocumentParsingResultEntityValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.ImportFileId).NotEmpty();
        RuleFor(x => x.DocumentType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ExtractedDataJson).NotEmpty();
        RuleFor(x => x.ConfidenceScore).InclusiveBetween(0.0, 1.0);
    }
}

/// <summary>
/// Tier-2 entity validator for <see cref="JsonComparisonResultEntity"/>.
/// </summary>
public class JsonComparisonResultEntityValidator : AbstractValidator<JsonComparisonResultEntity>
{
    public JsonComparisonResultEntityValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.SourceFileId).NotEmpty();
        RuleFor(x => x.TargetFileId).NotEmpty();
        RuleFor(x => x.DiffResultJson).NotEmpty();
        RuleFor(x => x.SummaryMetricsJson).NotEmpty();
    }
}
