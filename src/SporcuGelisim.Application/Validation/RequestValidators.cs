using FluentValidation;
using SporcuGelisim.Application.DTOs;

namespace SporcuGelisim.Application.Validation;

public sealed class CreateBranchRequestValidator : AbstractValidator<CreateBranchRequest>
{
    public CreateBranchRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Branş adı zorunludur.").MaximumLength(120).WithMessage("Branş adı en fazla 120 karakter olabilir.");
        RuleFor(x => x.Description).MaximumLength(1000).WithMessage("Açıklama en fazla 1000 karakter olabilir.");
    }
}

public sealed class CreateMotivationWordRequestValidator : AbstractValidator<CreateMotivationWordRequest>
{
    public CreateMotivationWordRequestValidator()
    {
        RuleFor(x => x.Text).NotEmpty().WithMessage("Kelime zorunludur.").MaximumLength(120).WithMessage("Kelime en fazla 120 karakter olabilir.");
        RuleFor(x => x.Description).MaximumLength(1000).WithMessage("Açıklama en fazla 1000 karakter olabilir.");
    }
}

public sealed class UpdateAthleteProfileRequestValidator : AbstractValidator<UpdateAthleteProfileRequest>
{
    public UpdateAthleteProfileRequestValidator()
    {
        RuleFor(x => x.NationalIdentityNumber)
            .Matches(@"^\d{11}$")
            .When(x => !string.IsNullOrWhiteSpace(x.NationalIdentityNumber))
            .WithMessage("TC kimlik no 11 rakam olmalıdır.");
        RuleFor(x => x.PhoneNumber).MaximumLength(30).WithMessage("Telefon no en fazla 30 karakter olabilir.");
        RuleFor(x => x.SecondaryPhoneNumber).MaximumLength(30).WithMessage("2. telefon no en fazla 30 karakter olabilir.");
        RuleFor(x => x.ParentPhoneNumber).MaximumLength(30).WithMessage("Ebeveyn no en fazla 30 karakter olabilir.");
        RuleFor(x => x.Address).MaximumLength(500).WithMessage("Adres en fazla 500 karakter olabilir.");
        RuleFor(x => x.Biography).MaximumLength(2000).WithMessage("Biyografi en fazla 2000 karakter olabilir.");
    }
}

public sealed class CreateSessionRequestValidator : AbstractValidator<CreateSessionRequest>
{
    public CreateSessionRequestValidator()
    {
        RuleFor(x => x.AthleteProfileId).NotEmpty().WithMessage("Sporcu seçimi zorunludur.");
        RuleFor(x => x.Title).NotEmpty().WithMessage("Başlık zorunludur.").MaximumLength(160).WithMessage("Başlık en fazla 160 karakter olabilir.");
    }
}

public sealed class CreateFeedbackRequestValidator : AbstractValidator<CreateFeedbackRequest>
{
    public CreateFeedbackRequestValidator()
    {
        RuleFor(x => x.AthleteProfileId).NotEmpty().WithMessage("Sporcu seçimi zorunludur.");
        RuleFor(x => x.Comment).NotEmpty().WithMessage("Yorum zorunludur.").MaximumLength(2000).WithMessage("Yorum en fazla 2000 karakter olabilir.");
    }
}

public sealed class SubmitWordRequestValidator : AbstractValidator<SubmitWordRequest>
{
    public SubmitWordRequestValidator()
    {
        RuleFor(x => x.AthleteProfileId).NotEmpty().WithMessage("Sporcu seçimi zorunludur.");
        RuleFor(x => x.TargetCoachUserId).NotEmpty().WithMessage("Antrenör seçimi zorunludur.");
        RuleFor(x => x.Text).NotEmpty().WithMessage("Kelime zorunludur.").MaximumLength(120).WithMessage("Kelime en fazla 120 karakter olabilir.");
        RuleFor(x => x.Note).MaximumLength(1000).WithMessage("Not en fazla 1000 karakter olabilir.");
    }
}
