using ClinicQ.Web.Api.Contracts;
using FluentValidation;

namespace ClinicQ.Web.Api.Validators;

public abstract class PatientRequestValidator<T> : AbstractValidator<T> where T : PatientRequest
{
    private static readonly string[] Genders = { "Female", "Male", "Other", "Unknown" };

    protected PatientRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(80);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(80);
        RuleFor(x => x.DateOfBirth)
            .NotEqual(default(DateTime))
            .LessThanOrEqualTo(_ => DateTime.Today).WithMessage("Date of birth cannot be in the future.")
            .GreaterThan(_ => DateTime.Today.AddYears(-130)).WithMessage("Date of birth is not plausible.");
        RuleFor(x => x.Gender).NotEmpty().Must(g => Genders.Contains(g, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Gender must be one of: {string.Join(", ", Genders)}.");
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(30).Matches(@"^[0-9+()\-\s.]+$").WithMessage("Phone contains invalid characters.");
        RuleFor(x => x.AddressLine).MaximumLength(200);
        RuleFor(x => x.City).MaximumLength(100);
        RuleFor(x => x.State).MaximumLength(50);
        RuleFor(x => x.PostalCode).MaximumLength(20);
        RuleFor(x => x.Allergies).MaximumLength(500);
    }
}

public sealed class CreatePatientRequestValidator : PatientRequestValidator<CreatePatientRequest>
{
}

public sealed class UpdatePatientRequestValidator : PatientRequestValidator<UpdatePatientRequest>
{
}
