using ClinicQ.Domain.Appointments;
using ClinicQ.Domain.Exceptions;
using ClinicQ.Domain.Scheduling;
using ClinicQ.Web.Data.Repositories;
using ClinicQ.Web.Infrastructure;

namespace ClinicQ.Web.Services;

public sealed record AppointmentRequest(int PatientId, int DoctorId, int BranchId, DateTime Start, string Reason, string? Notes);

/// <summary>
/// Application service for booking and moving appointments through their lifecycle.
/// Slot rules are enforced here; status transitions are enforced by the domain.
/// </summary>
public sealed class AppointmentService
{
    private readonly IAppointmentRepository _appointments;
    private readonly IBranchRepository _branches;
    private readonly IDoctorRepository _doctors;
    private readonly IPatientRepository _patients;
    private readonly SlotGenerator _slots;
    private readonly IClock _clock;

    public AppointmentService(
        IAppointmentRepository appointments,
        IBranchRepository branches,
        IDoctorRepository doctors,
        IPatientRepository patients,
        SlotGenerator slots,
        IClock clock)
    {
        _appointments = appointments;
        _branches = branches;
        _doctors = doctors;
        _patients = patients;
        _slots = slots;
        _clock = clock;
    }

    public async Task<Appointment> RequestAsync(AppointmentRequest request, CancellationToken cancellationToken = default)
    {
        var branch = await _branches.GetByIdAsync(request.BranchId, cancellationToken)
                     ?? throw new EntityNotFoundException("Branch", request.BranchId);
        var doctor = await _doctors.GetByIdAsync(request.DoctorId, cancellationToken)
                     ?? throw new EntityNotFoundException("Doctor", request.DoctorId);
        _ = await _patients.GetByIdAsync(request.PatientId, cancellationToken)
            ?? throw new EntityNotFoundException("Patient", request.PatientId);

        if (doctor.BranchId != branch.Id)
        {
            throw new DomainException($"{doctor.FullName} does not practise at {branch.Name}.");
        }

        if (!doctor.IsActive || !branch.IsActive)
        {
            throw new DomainException("The selected doctor or branch is not accepting appointments.");
        }

        var rule = await _branches.GetSlotRuleAsync(branch.Id, cancellationToken)
                   ?? throw new DomainException($"No slot rule is configured for {branch.Name}.");

        var now = _clock.Now;
        var start = request.Start;
        if (!rule.IsWorkingDay(start.DayOfWeek))
        {
            throw new DomainException($"{branch.Name} is closed on {start.DayOfWeek}s.");
        }

        if (!_slots.IsOnSlotBoundary(rule, start))
        {
            throw new DomainException($"Appointments at {branch.Name} must start on a {rule.SlotDurationMinutes}-minute boundary between {rule.OpenTime:HH\\:mm} and {rule.CloseTime:HH\\:mm}.");
        }

        if (start < now.AddHours(rule.MinLeadTimeHours))
        {
            throw new DomainException($"Appointments must be requested at least {rule.MinLeadTimeHours} hour(s) in advance.");
        }

        if (DateOnly.FromDateTime(start) > _clock.Today.AddDays(rule.MaxAdvanceDays))
        {
            throw new DomainException($"Appointments can be booked at most {rule.MaxAdvanceDays} days ahead.");
        }

        var booked = await _appointments.GetBookedCountsAsync(doctor.Id, DateOnly.FromDateTime(start), cancellationToken);
        booked.TryGetValue(start, out var taken);
        if (taken >= rule.MaxBookingsPerSlot)
        {
            throw new DomainException($"{doctor.FullName} is fully booked at {start:HH:mm} on {start:d MMM yyyy}.");
        }

        var appointment = new Appointment
        {
            PatientId = request.PatientId,
            DoctorId = doctor.Id,
            BranchId = branch.Id,
            ScheduledStart = start,
            ScheduledEnd = start.AddMinutes(rule.SlotDurationMinutes),
            Status = AppointmentStatus.Requested,
            Reason = request.Reason.Trim(),
            Notes = request.Notes?.Trim(),
            CreatedAt = now
        };

        await _appointments.CreateAsync(appointment, cancellationToken);
        return appointment;
    }

    public Task<Appointment> ConfirmAsync(int id, CancellationToken ct = default)
        => TransitionAsync(id, a => a.Confirm(_clock.Now), ct);

    public Task<Appointment> CheckInAsync(int id, CancellationToken ct = default)
        => TransitionAsync(id, a => a.CheckIn(_clock.Now), ct);

    public Task<Appointment> StartConsultationAsync(int id, CancellationToken ct = default)
        => TransitionAsync(id, a => a.StartConsultation(_clock.Now), ct);

    public Task<Appointment> CompleteAsync(int id, string? notes, CancellationToken ct = default)
        => TransitionAsync(id, a => a.Complete(_clock.Now, notes), ct);

    public Task<Appointment> CancelAsync(int id, string reason, CancellationToken ct = default)
        => TransitionAsync(id, a => a.Cancel(_clock.Now, reason), ct);

    public Task<Appointment> MarkNoShowAsync(int id, CancellationToken ct = default)
        => TransitionAsync(id, a => a.MarkNoShow(_clock.Now), ct);

    public async Task<Appointment> GetRequiredAsync(int id, CancellationToken cancellationToken = default)
        => await _appointments.GetByIdAsync(id, cancellationToken) ?? throw new EntityNotFoundException("Appointment", id);

    private async Task<Appointment> TransitionAsync(int id, Action<Appointment> action, CancellationToken cancellationToken)
    {
        var appointment = await GetRequiredAsync(id, cancellationToken);
        action(appointment);
        await _appointments.UpdateAsync(appointment, cancellationToken);
        return appointment;
    }
}
