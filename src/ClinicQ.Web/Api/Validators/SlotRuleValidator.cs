using System.Globalization;
using ClinicQ.Web.Api.Contracts;
using FluentValidation;

namespace ClinicQ.Web.Api.Validators;

public sealed class UpdateSlotRuleRequestValidator : AbstractValidator<UpdateSlotRuleRequest>
{
    public UpdateSlotRuleRequestValidator()
    {
        RuleFor(x => x.OpenTime).NotEmpty().Must(BeTime).WithMessage("OpenTime must be HH:mm.");
        RuleFor(x => x.CloseTime).NotEmpty().Must(BeTime).WithMessage("CloseTime must be HH:mm.");
        RuleFor(x => x)
            .Must(x => BeTime(x.OpenTime) && BeTime(x.CloseTime) && Parse(x.CloseTime) > Parse(x.OpenTime))
            .WithMessage("CloseTime must be after OpenTime.")
            .When(x => BeTime(x.OpenTime) && BeTime(x.CloseTime));
        RuleFor(x => x.SlotDurationMinutes).InclusiveBetween(5, 240);
        RuleFor(x => x.WorkingDaysMask).InclusiveBetween(1, 127);
        RuleFor(x => x.MaxBookingsPerSlot).InclusiveBetween(1, 10);
        RuleFor(x => x.MinLeadTimeHours).InclusiveBetween(0, 168);
        RuleFor(x => x.MaxAdvanceDays).InclusiveBetween(1, 365);
    }

    private static bool BeTime(string value) => TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private static TimeOnly Parse(string value) => TimeOnly.ParseExact(value, "HH:mm", CultureInfo.InvariantCulture);
}
