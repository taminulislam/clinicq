using ClinicQ.Web.Api.Contracts;
using FluentValidation;

namespace ClinicQ.Web.Api.Validators;

public sealed class PrescriptionItemDtoValidator : AbstractValidator<PrescriptionItemDto>
{
    public PrescriptionItemDtoValidator()
    {
        RuleFor(x => x.Medication).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Dosage).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Frequency).NotEmpty().MaximumLength(80);
        RuleFor(x => x.DurationDays).InclusiveBetween(1, 365);
        RuleFor(x => x.Notes).MaximumLength(300);
    }
}

public sealed class CreatePrescriptionRequestValidator : AbstractValidator<CreatePrescriptionRequest>
{
    public CreatePrescriptionRequestValidator()
    {
        RuleFor(x => x.AppointmentId).GreaterThan(0);
        RuleFor(x => x.Diagnosis).MaximumLength(300);
        RuleFor(x => x.Instructions).MaximumLength(1000);
        RuleFor(x => x.Items).NotEmpty().WithMessage("At least one medication is required.");
        RuleForEach(x => x.Items).SetValidator(new PrescriptionItemDtoValidator());
    }
}
