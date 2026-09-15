using System.Globalization;
using ClinicQ.Domain.Entities;
using ClinicQ.Domain.Exceptions;
using ClinicQ.Domain.Scheduling;
using ClinicQ.Web.Api.Contracts;
using ClinicQ.Web.Data.Repositories;
using ClinicQ.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicQ.Web.Api.V1;

public sealed class BranchesController : ApiControllerBase
{
    private readonly IBranchRepository _branches;
    private readonly ISlotRepository _slots;
    private readonly IClock _clock;

    public BranchesController(IBranchRepository branches, ISlotRepository slots, IClock clock)
    {
        _branches = branches;
        _slots = slots;
        _clock = clock;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BranchResponse>>> List(CancellationToken cancellationToken)
        => Ok((await _branches.GetAllAsync(cancellationToken)).Select(BranchResponse.From).ToList());

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BranchResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var branch = await _branches.GetByIdAsync(id, cancellationToken) ?? throw new EntityNotFoundException("Branch", id);
        return Ok(BranchResponse.From(branch));
    }

    /// <summary>Available slots for a doctor on a date (yyyy-MM-dd). Uses usp_GetAvailableSlots on SQL Server.</summary>
    [HttpGet("{id:int}/slots")]
    public async Task<ActionResult<IReadOnlyList<SlotResponse>>> Slots(int id, [FromQuery] int doctorId, [FromQuery] string? date, CancellationToken cancellationToken)
    {
        if (doctorId <= 0)
        {
            return BadRequest(new ProblemDetails { Status = 400, Title = "doctorId is required" });
        }

        var day = _clock.Today;
        if (!string.IsNullOrWhiteSpace(date) && !DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day))
        {
            return BadRequest(new ProblemDetails { Status = 400, Title = "date must be yyyy-MM-dd" });
        }

        var slots = await _slots.GetAvailableSlotsAsync(id, doctorId, day, _clock.Now, cancellationToken);
        return Ok(slots.Select(SlotResponse.From).ToList());
    }

    [HttpGet("{id:int}/fees")]
    public async Task<ActionResult<IReadOnlyList<FeeScheduleItemResponse>>> Fees(int id, CancellationToken cancellationToken)
    {
        _ = await _branches.GetByIdAsync(id, cancellationToken) ?? throw new EntityNotFoundException("Branch", id);
        var fees = await _branches.GetFeeScheduleAsync(id, cancellationToken);
        return Ok(fees.Select(FeeScheduleItemResponse.From).ToList());
    }

    [HttpGet("{id:int}/slot-rule")]
    public async Task<ActionResult<SlotRuleResponse>> SlotRule(int id, CancellationToken cancellationToken)
    {
        var rule = await _branches.GetSlotRuleAsync(id, cancellationToken) ?? throw new EntityNotFoundException("BranchSlotRule", id);
        return Ok(SlotRuleResponse.From(rule));
    }

    [HttpPut("{id:int}/slot-rule")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<SlotRuleResponse>> UpdateSlotRule(int id, [FromBody] UpdateSlotRuleRequest request, CancellationToken cancellationToken)
    {
        _ = await _branches.GetByIdAsync(id, cancellationToken) ?? throw new EntityNotFoundException("Branch", id);

        var rule = await _branches.GetSlotRuleAsync(id, cancellationToken) ?? new BranchSlotRule { BranchId = id };
        rule.OpenTime = TimeOnly.ParseExact(request.OpenTime, "HH:mm", CultureInfo.InvariantCulture);
        rule.CloseTime = TimeOnly.ParseExact(request.CloseTime, "HH:mm", CultureInfo.InvariantCulture);
        rule.SlotDurationMinutes = request.SlotDurationMinutes;
        rule.WorkingDaysMask = request.WorkingDaysMask;
        rule.MaxBookingsPerSlot = request.MaxBookingsPerSlot;
        rule.MinLeadTimeHours = request.MinLeadTimeHours;
        rule.MaxAdvanceDays = request.MaxAdvanceDays;

        await _branches.UpsertSlotRuleAsync(rule, cancellationToken);
        return Ok(SlotRuleResponse.From(rule));
    }
}
