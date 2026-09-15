using ClinicQ.Web.Data.Repositories;
using ClinicQ.Web.Infrastructure;
using ClinicQ.Web.Infrastructure.Email;

namespace ClinicQ.Web.Jobs;

/// <summary>
/// Hangfire recurring job: sends a follow-up check-in email to patients whose visit completed N days ago.
/// </summary>
public sealed class FollowUpAlertJob
{
    public const int FollowUpAfterDays = 7;

    private readonly IAppointmentRepository _appointments;
    private readonly IPatientRepository _patients;
    private readonly IDoctorRepository _doctors;
    private readonly IEmailSender _email;
    private readonly IClock _clock;
    private readonly ILogger<FollowUpAlertJob> _logger;

    public FollowUpAlertJob(
        IAppointmentRepository appointments,
        IPatientRepository patients,
        IDoctorRepository doctors,
        IEmailSender email,
        IClock clock,
        ILogger<FollowUpAlertJob> logger)
    {
        _appointments = appointments;
        _patients = patients;
        _doctors = doctors;
        _email = email;
        _clock = clock;
        _logger = logger;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var day = _clock.Today.AddDays(-FollowUpAfterDays).ToDateTime(TimeOnly.MinValue);
        var completed = await _appointments.ListCompletedBetweenAsync(day, day.AddDays(1), cancellationToken);
        var sent = 0;

        foreach (var appointment in completed)
        {
            var patient = await _patients.GetByIdAsync(appointment.PatientId, cancellationToken);
            var doctor = await _doctors.GetByIdAsync(appointment.DoctorId, cancellationToken);
            if (patient is null || doctor is null || string.IsNullOrWhiteSpace(patient.Email))
            {
                continue;
            }

            var body = $"""
                <p>Hello {patient.FirstName},</p>
                <p>It has been a week since your visit with {doctor.FullName} regarding "{appointment.Reason}".</p>
                <p>If your symptoms have not improved, or if you have questions about your treatment plan,
                please book a follow-up appointment through the ClinicQ portal or call your branch.</p>
                <p>ClinicQ</p>
                """;

            await _email.SendAsync(new EmailMessage(patient.Email, "How are you feeling after your visit?", body), cancellationToken);
            sent++;
        }

        _logger.LogInformation("Follow-up alerts sent: {Count} for visits completed on {Date:d}", sent, day);
        return sent;
    }
}
