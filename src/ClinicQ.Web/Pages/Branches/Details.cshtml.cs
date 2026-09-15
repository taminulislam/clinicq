using System.Globalization;
using ClinicQ.Domain.Billing;
using ClinicQ.Domain.Entities;
using ClinicQ.Domain.Scheduling;
using ClinicQ.Web.Api.Contracts;
using ClinicQ.Web.Data.Repositories;
using ClinicQ.Web.Infrastructure;
using ClinicQ.Web.Ui;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClinicQ.Web.Pages.Branches;

/// <summary>Per-branch configuration: slot rules (who can book when) and the fee schedule used for invoicing.</summary>
public sealed class DetailsModel : PageModel
{
    private readonly IBranchRepository _branches;
    private readonly IDoctorRepository _doctors;
    private readonly IValidator<UpdateSlotRuleRequest> _ruleValidator;
    private readonly IValidator<FeeItemInput> _feeValidator;
    private readonly IClock _clock;

    public DetailsModel(
        IBranchRepository branches,
        IDoctorRepository doctors,
        IValidator<UpdateSlotRuleRequest> ruleValidator,
        IValidator<FeeItemInput> feeValidator,
        IClock clock)
    {
        _branches = branches;
        _doctors = doctors;
        _ruleValidator = ruleValidator;
        _feeValidator = feeValidator;
        _clock = clock;
    }

    public Branch Branch { get; private set; } = new();
    public IReadOnlyList<Doctor> Doctors { get; private set; } = Array.Empty<Doctor>();
    public IReadOnlyList<FeeScheduleItem> Fees { get; private set; } = Array.Empty<FeeScheduleItem>();
    public bool IsAdmin => User.IsInRole(Roles.Admin);

    public UpdateSlotRuleRequest Rule { get; set; } = new();
    public FeeItemInput Fee { get; set; } = new();

    public static IReadOnlyList<(DayOfWeek Day, int Flag)> Days { get; } = new[]
    {
        (DayOfWeek.Monday, WorkingDays.Monday), (DayOfWeek.Tuesday, WorkingDays.Tuesday), (DayOfWeek.Wednesday, WorkingDays.Wednesday),
        (DayOfWeek.Thursday, WorkingDays.Thursday), (DayOfWeek.Friday, WorkingDays.Friday), (DayOfWeek.Saturday, WorkingDays.Saturday),
        (DayOfWeek.Sunday, WorkingDays.Sunday)
    };

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(id, cancellationToken))
        {
            return NotFound();
        }

        var rule = await _branches.GetSlotRuleAsync(id, cancellationToken) ?? new BranchSlotRule { BranchId = id };
        Rule = new UpdateSlotRuleRequest
        {
            OpenTime = rule.OpenTime.ToString("HH:mm", CultureInfo.InvariantCulture),
            CloseTime = rule.CloseTime.ToString("HH:mm", CultureInfo.InvariantCulture),
            SlotDurationMinutes = rule.SlotDurationMinutes,
            WorkingDaysMask = rule.WorkingDaysMask,
            MaxBookingsPerSlot = rule.MaxBookingsPerSlot,
            MinLeadTimeHours = rule.MinLeadTimeHours,
            MaxAdvanceDays = rule.MaxAdvanceDays
        };
        return Page();
    }

    /// <summary>Save slot rules. Working days arrive as a list of flags from checkboxes and are OR-ed into the mask.</summary>
    public async Task<IActionResult> OnPostRuleAsync(int id, [FromForm] UpdateSlotRuleRequest rule, [FromForm] int[] days, CancellationToken cancellationToken)
    {
        if (!IsAdmin)
        {
            return Forbid();
        }

        if (!await LoadAsync(id, cancellationToken))
        {
            return NotFound();
        }

        rule.WorkingDaysMask = days.Aggregate(0, (mask, flag) => mask | flag);
        var result = await _ruleValidator.ValidateAsync(rule, cancellationToken);
        if (!result.IsValid)
        {
            this.FlashError(string.Join(" ", result.Errors.Select(e => e.ErrorMessage)));
            return RedirectToPage(new { id });
        }

        await _branches.UpsertSlotRuleAsync(new BranchSlotRule
        {
            BranchId = id,
            OpenTime = TimeOnly.ParseExact(rule.OpenTime, "HH:mm", CultureInfo.InvariantCulture),
            CloseTime = TimeOnly.ParseExact(rule.CloseTime, "HH:mm", CultureInfo.InvariantCulture),
            SlotDurationMinutes = rule.SlotDurationMinutes,
            WorkingDaysMask = rule.WorkingDaysMask,
            MaxBookingsPerSlot = rule.MaxBookingsPerSlot,
            MinLeadTimeHours = rule.MinLeadTimeHours,
            MaxAdvanceDays = rule.MaxAdvanceDays
        }, cancellationToken);

        this.Flash($"Slot rules for {Branch.Name} saved.");
        return RedirectToPage(new { id });
    }

    /// <summary>Add a fee (a new effective-dated row supersedes the previous price for the same code).</summary>
    public async Task<IActionResult> OnPostFeeAsync(int id, [FromForm] FeeItemInput fee, CancellationToken cancellationToken)
    {
        if (!IsAdmin)
        {
            return Forbid();
        }

        if (!await LoadAsync(id, cancellationToken))
        {
            return NotFound();
        }

        var result = await _feeValidator.ValidateAsync(fee, cancellationToken);
        if (!result.IsValid)
        {
            this.FlashError(string.Join(" ", result.Errors.Select(e => e.ErrorMessage)));
            return RedirectToPage(new { id });
        }

        var code = fee.ServiceCode.Trim().ToUpperInvariant();
        var effective = _clock.Now;
        effective = new DateTime(effective.Year, effective.Month, effective.Day, effective.Hour, effective.Minute, effective.Second);
        await _branches.AddFeeScheduleItemAsync(new FeeScheduleItem
        {
            BranchId = id,
            ServiceCode = code,
            Description = fee.Description.Trim(),
            Amount = FeeCalculator.Round(fee.Amount),
            IsActive = true,
            EffectiveFrom = effective
        }, cancellationToken);

        this.Flash($"{code} priced at {fee.Amount:C2} from today.");
        return RedirectToPage(new { id });
    }

    private async Task<bool> LoadAsync(int id, CancellationToken cancellationToken)
    {
        var branch = await _branches.GetByIdAsync(id, cancellationToken);
        if (branch is null)
        {
            return false;
        }

        Branch = branch;
        Doctors = await _doctors.GetAllAsync(id, cancellationToken);
        Fees = await _branches.GetFeeScheduleAsync(id, cancellationToken);
        return true;
    }
}

public sealed class FeeItemInput
{
    public string ServiceCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public sealed class FeeItemInputValidator : AbstractValidator<FeeItemInput>
{
    public FeeItemInputValidator()
    {
        RuleFor(x => x.ServiceCode).NotEmpty().MaximumLength(30).Matches("^[A-Za-z0-9-]+$").WithMessage("Service code may contain letters, digits and dashes only.");
        RuleFor(x => x.Description).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0).LessThanOrEqualTo(100_000);
    }
}
