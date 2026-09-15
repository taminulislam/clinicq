namespace ClinicQ.Domain.Scheduling;

/// <summary>
/// Booking rules configured per branch: opening hours, slot length, capacity and lead-time constraints.
/// </summary>
public class BranchSlotRule
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public TimeOnly OpenTime { get; set; } = new(8, 0);
    public TimeOnly CloseTime { get; set; } = new(17, 0);
    public int SlotDurationMinutes { get; set; } = 30;
    /// <summary>Bitmask of working days, see <see cref="WorkingDays"/>.</summary>
    public int WorkingDaysMask { get; set; } = WorkingDays.MondayToFriday;
    /// <summary>Maximum bookings a single doctor can hold in one slot (allows overbooking when &gt; 1).</summary>
    public int MaxBookingsPerSlot { get; set; } = 1;
    /// <summary>Minimum hours between "now" and the slot start for a new booking.</summary>
    public int MinLeadTimeHours { get; set; } = 1;
    /// <summary>How far ahead patients may book.</summary>
    public int MaxAdvanceDays { get; set; } = 60;

    public bool IsWorkingDay(DayOfWeek day) => WorkingDays.Contains(WorkingDaysMask, day);
}
