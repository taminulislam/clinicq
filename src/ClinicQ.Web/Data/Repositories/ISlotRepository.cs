using ClinicQ.Domain.Scheduling;

namespace ClinicQ.Web.Data.Repositories;

/// <summary>
/// Appointment slot lookup. Backed by the usp_GetAvailableSlots stored procedure on SQL Server and by
/// inline SQL plus <see cref="SlotGenerator"/> on SQLite.
/// </summary>
public interface ISlotRepository
{
    Task<IReadOnlyList<AppointmentSlot>> GetAvailableSlotsAsync(int branchId, int doctorId, DateOnly date, DateTime now, CancellationToken cancellationToken = default);
}
