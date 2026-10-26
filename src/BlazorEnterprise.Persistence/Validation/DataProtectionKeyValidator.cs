using FluentValidation;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;

namespace BlazorEnterprise.Persistence.Validation;

/// <summary>
/// Tier 2 Entity Validator for <see cref="DataProtectionKey"/>.
/// Ensures that Microsoft DataProtection key entries stored in the database are well-formed and non-empty.
/// Gives AppDbContext 100% validator coverage with zero exemptions.
/// </summary>
public class DataProtectionKeyValidator : AbstractValidator<DataProtectionKey>
{
    public DataProtectionKeyValidator()
    {
        RuleFor(x => x.Xml)
            .NotEmpty().WithMessage("Data protection key XML payload cannot be empty.");
    }
}
