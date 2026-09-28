using BlazorFluent.Core.Common;
using BlazorFluent.Core.DataListTypes;
using FluentValidation;

namespace BlazorFluent.Core.Validation;

public class SendNotificationRequestValidator : AbstractValidator<SendNotificationRequest>
{
    public SendNotificationRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Message body is required.")
            .MaximumLength(2000).WithMessage("Message must not exceed 2000 characters.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required when sending personal notifications.")
            .When(x => x.Category == NotificationCategory.Personal);
    }
}
