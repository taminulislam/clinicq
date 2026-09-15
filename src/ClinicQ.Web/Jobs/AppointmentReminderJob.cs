using ClinicQ.Domain.Appointments;
using ClinicQ.Web.Data.Repositories;
using ClinicQ.Web.Infrastructure;
using ClinicQ.Web.Infrastructure.Email;

namespace ClinicQ.Web.Jobs;

/// <summary>
/// Hangfire recurring job: emails every patient with a confirmed appointment tomorrow.
/// </summary>
public sealed class AppointmentReminderJob
{
    private readonly IAppointmentRepository _appointments;
    private readonly IPatientRepository _patients;
    private readonly IDoctorRepository _doctors;
    private readonly IBranchRepository _branches;
    private readonly IEmailSender _email;
    private readonly IClock _clock;
    private readonly ILogger<AppointmentReminderJob> _logger;

    public AppointmentReminderJob(
        IAppointmentRepository appointments,
        IPatientRepository patients,
        IDoctorRepository doctors,
        IBranchRepository branches,
        IEmailSender email,
        IClock clock,
        ILogger<AppointmentReminderJob> logger)
    {
        _appointments = appointments;
        _patients = patients;
        _doctors = doctors;
        _branches = branches;
        _email = email;
        _clock = clock;
        _logger = logger;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var tomorrow = _clock.Today.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var due = await _appointments.ListByStatusInWindowAsync(AppointmentStatus.Confirmed, tomorrow, tomorrow.AddDays(1), cancellationToken);
        var sent = 0;

        foreach (var appointment in due)
        {
            var patient = await _patients.GetByIdAsync(appointment.PatientId, cancellationToken);
            var doctor = await _doctors.GetByIdAsync(appointment.DoctorId, cancellationToken);
            var branch = await _branches.GetByIdAsync(appointment.BranchId, cancellationToken);
            if (patient is null || doctor is null || branch is null || string.IsNullOrWhiteSpace(patient.Email))
            {
                continue;
            }

            var subject = $"Reminder: your appointment on {appointment.ScheduledStart:dddd d MMMM} at {appointment.ScheduledStart:HH:mm}";
            var body = $"""
                <p>Hello {patient.FirstName},</p>
                <p>This is a reminder of your appointment with <strong>{doctor.FullName}</strong> at <strong>{branch.Name}</strong>
                on {appointment.ScheduledStart:dddd d MMMM yyyy} at {appointment.ScheduledStart:HH:mm}.</p>
                <p>{branch.AddressLine}, {branch.City} {branch.State} {branch.PostalCode}. Call {branch.Phone} if you need to reschedule.</p>
                <p>ClinicQ</p>
                """;

            await _email.SendAsync(new EmailMessage(patient.Email, subject, body), cancellationToken);
            sent++;
        }

        _logger.LogInformation("Appointment reminders sent: {Count} of {Total} confirmed for {Date:d}", sent, due.Count, tomorrow);
        return sent;
    }
}
