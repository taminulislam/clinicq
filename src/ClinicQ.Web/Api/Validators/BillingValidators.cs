using ClinicQ.Domain.Billing;
using ClinicQ.Web.Api.Contracts;
using FluentValidation;

namespace ClinicQ.Web.Api.Validators;

public sealed class ServiceLineDtoValidator : AbstractValidator<ServiceLineDto>
{
    public ServiceLineDtoValidator()
    {
        RuleFor(x => x.ServiceCode).NotEmpty().MaximumLength(30).Matches("^[A-Za-z0-9-]+$");
        RuleFor(x => x.Quantity).InclusiveBetween(1, 50);
    }
}

public sealed class GenerateInvoiceRequestValidator : AbstractValidator<GenerateInvoiceRequest>
{
    public GenerateInvoiceRequestValidator()
    {
        RuleFor(x => x.AppointmentId).GreaterThan(0);
        RuleFor(x => x.Services).NotEmpty().WithMessage("At least one service line is required.");
        RuleForEach(x => x.Services).SetValidator(new ServiceLineDtoValidator());
        RuleFor(x => x.DiscountPercent).InclusiveBetween(0, 100).When(x => x.DiscountPercent.HasValue);
    }
}

public sealed class RecordPaymentRequestValidator : AbstractValidator<RecordPaymentRequest>
{
    public RecordPaymentRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0).LessThanOrEqualTo(100_000);
        RuleFor(x => x.Method)
            .NotEmpty()
            .Must(m => Enum.TryParse<PaymentMethod>(m, true, out _))
            .WithMessage($"Method must be one of: {string.Join(", ", Enum.GetNames<PaymentMethod>())}.");
        RuleFor(x => x.Reference).MaximumLength(100);
    }
}

public sealed class ReconcileRequestValidator : AbstractValidator<ReconcileRequest>
{
    public ReconcileRequestValidator()
    {
        RuleFor(x => x.PeriodEnd)
            .GreaterThan(x => x.PeriodStart!.Value)
            .When(x => x.PeriodStart.HasValue && x.PeriodEnd.HasValue)
            .WithMessage("PeriodEnd must be after PeriodStart.");
    }
}
