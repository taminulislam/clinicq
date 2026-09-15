using ClinicQ.Web.Api.Contracts;
using FluentValidation;

namespace ClinicQ.Web.Api.Validators;

public sealed class CreateAppointmentRequestValidator : AbstractValidator<CreateAppointmentRequest>
{
    public CreateAppointmentRequestValidator()
    {
        RuleFor(x => x.PatientId).GreaterThan(0);
        RuleFor(x => x.DoctorId).GreaterThan(0);
        RuleFor(x => x.BranchId).GreaterThan(0);
        RuleFor(x => x.Start)
            .NotEqual(default(DateTime)).WithMessage("Start is required.")
            .Must(s => s.Second == 0 && s.Millisecond == 0).WithMessage("Start must be a whole minute.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public sealed class CancelAppointmentRequestValidator : AbstractValidator<CancelAppointmentRequest>
{
    public CancelAppointmentRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MinimumLength(3).MaximumLength(300);
    }
}

public sealed class CompleteAppointmentRequestValidator : AbstractValidator<CompleteAppointmentRequest>
{
    public CompleteAppointmentRequestValidator()
    {
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}
