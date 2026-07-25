using FluentValidation;
using SporcuGelisim.Application.DTOs;

namespace SporcuGelisim.Application.Validation;

public sealed class CreateBranchRequestValidator : AbstractValidator<CreateBranchRequest>
{
    public CreateBranchRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public sealed class CreateMotivationWordRequestValidator : AbstractValidator<CreateMotivationWordRequest>
{
    public CreateMotivationWordRequestValidator()
    {
        RuleFor(x => x.Text).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public sealed class CreateSessionRequestValidator : AbstractValidator<CreateSessionRequest>
{
    public CreateSessionRequestValidator()
    {
        RuleFor(x => x.AthleteProfileId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(160);
    }
}

public sealed class CreateFeedbackRequestValidator : AbstractValidator<CreateFeedbackRequest>
{
    public CreateFeedbackRequestValidator()
    {
        RuleFor(x => x.AthleteProfileId).NotEmpty();
        RuleFor(x => x.Comment).NotEmpty().MaximumLength(2000);
    }
}
