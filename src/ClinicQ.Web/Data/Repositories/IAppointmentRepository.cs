using ClinicQ.Domain.Appointments;
using ClinicQ.Web.Data.Models;

namespace ClinicQ.Web.Data.Repositories;

public interface IAppointmentRepository
{
    Task<Appointment?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<AppointmentListItem?> GetListItemAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AppointmentListItem>> ListAsync(AppointmentFilter filter, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(Appointment appointment, CancellationToken cancellationToken = default);
    Task UpdateAsync(Appointment appointment, CancellationToken cancellationToken = default);

    /// <summary>Active (not cancelled / no-show) bookings for a doctor on a day, keyed by slot start.</summary>
    Task<IReadOnlyDictionary<DateTime, int>> GetBookedCountsAsync(int doctorId, DateOnly date, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Appointment>> ListByStatusInWindowAsync(AppointmentStatus status, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Appointment>> ListCompletedBetweenAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
}
