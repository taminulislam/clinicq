namespace ClinicQ.Domain.Scheduling;

/// <summary>
/// A bookable window for one doctor on one day, with remaining capacity.
/// </summary>
public sealed record AppointmentSlot(DateTime Start, DateTime End, int Capacity, int Booked)
{
    public int Remaining => Math.Max(0, Capacity - Booked);
    public bool IsAvailable => Remaining > 0;
}
