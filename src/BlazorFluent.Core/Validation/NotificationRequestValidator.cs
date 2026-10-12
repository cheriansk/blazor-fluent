using BlazorFluent.Core.DataListTypes;
using BlazorFluent.Core.Dtos;
using BlazorFluent.Core.Dtos.Requests;
using FluentValidation;

namespace BlazorFluent.Core.Validation;

public class SendNotificationReqDtoValidator : AbstractValidator<SendNotificationReqDto>
{
    public SendNotificationReqDtoValidator()
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
