using ClinicQ.Domain.Exceptions;

namespace ClinicQ.Domain.Scheduling;

/// <summary>
/// Produces the bookable slots for a doctor on a given date from the branch rule and existing bookings.
/// The SQL Server implementation performs the same calculation inside usp_GetAvailableSlots; this class
/// is the provider-independent reference used by the SQLite fallback and by unit tests.
/// </summary>
public sealed class SlotGenerator
{
    public IReadOnlyList<AppointmentSlot> Generate(
        BranchSlotRule rule,
        DateOnly date,
        IReadOnlyDictionary<DateTime, int> bookedCountsByStart,
        DateTime now)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(bookedCountsByStart);

        if (rule.SlotDurationMinutes <= 0)
        {
            throw new DomainException("Slot duration must be a positive number of minutes.");
        }

        if (rule.CloseTime <= rule.OpenTime)
        {
            throw new DomainException("Branch close time must be after its open time.");
        }

        if (!rule.IsWorkingDay(date.DayOfWeek))
        {
            return Array.Empty<AppointmentSlot>();
        }

        if (date > DateOnly.FromDateTime(now).AddDays(rule.MaxAdvanceDays))
        {
            return Array.Empty<AppointmentSlot>();
        }

        var earliestBookable = now.AddHours(rule.MinLeadTimeHours);
        var slots = new List<AppointmentSlot>();
        var cursor = date.ToDateTime(rule.OpenTime);
        var close = date.ToDateTime(rule.CloseTime);
        var duration = TimeSpan.FromMinutes(rule.SlotDurationMinutes);

        while (cursor + duration <= close)
        {
            if (cursor >= earliestBookable)
            {
                bookedCountsByStart.TryGetValue(cursor, out var booked);
                slots.Add(new AppointmentSlot(cursor, cursor + duration, rule.MaxBookingsPerSlot, booked));
            }

            cursor += duration;
        }

        return slots;
    }

    /// <summary>
    /// Validates that a requested start time lands exactly on a slot boundary for the rule.
    /// </summary>
    public bool IsOnSlotBoundary(BranchSlotRule rule, DateTime start)
    {
        var open = DateOnly.FromDateTime(start).ToDateTime(rule.OpenTime);
        var close = DateOnly.FromDateTime(start).ToDateTime(rule.CloseTime);
        if (start < open || start.AddMinutes(rule.SlotDurationMinutes) > close)
        {
            return false;
        }

        var offset = (start - open).TotalMinutes;
        return Math.Abs(offset % rule.SlotDurationMinutes) < 0.0001;
    }
}
