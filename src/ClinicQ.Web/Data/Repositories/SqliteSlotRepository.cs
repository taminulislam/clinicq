using ClinicQ.Domain.Exceptions;
using ClinicQ.Domain.Scheduling;

namespace ClinicQ.Web.Data.Repositories;

/// <summary>
/// SQLite implementation: the stored procedure is replaced by an inline booked-count query
/// (see <see cref="IAppointmentRepository.GetBookedCountsAsync"/>) and the domain <see cref="SlotGenerator"/>.
/// </summary>
public sealed class SqliteSlotRepository : ISlotRepository
{
    private readonly IBranchRepository _branches;
    private readonly IAppointmentRepository _appointments;
    private readonly SlotGenerator _generator;

    public SqliteSlotRepository(IBranchRepository branches, IAppointmentRepository appointments, SlotGenerator generator)
    {
        _branches = branches;
        _appointments = appointments;
        _generator = generator;
    }

    public async Task<IReadOnlyList<AppointmentSlot>> GetAvailableSlotsAsync(int branchId, int doctorId, DateOnly date, DateTime now, CancellationToken cancellationToken = default)
    {
        var rule = await _branches.GetSlotRuleAsync(branchId, cancellationToken)
                   ?? throw new EntityNotFoundException("BranchSlotRule", branchId);

        var booked = await _appointments.GetBookedCountsAsync(doctorId, date, cancellationToken);
        return _generator.Generate(rule, date, booked, now);
    }
}
