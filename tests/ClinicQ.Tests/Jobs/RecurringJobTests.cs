using ClinicQ.Domain.Appointments;
using ClinicQ.Domain.Entities;
using ClinicQ.Tests.Data;
using ClinicQ.Web.Infrastructure.Email;
using ClinicQ.Web.Jobs;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClinicQ.Tests.Jobs;

/// <summary>Collects the messages a job would have sent.</summary>
public sealed class RecordingEmailSender : IEmailSender
{
    public List<EmailMessage> Sent { get; } = new();

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        Sent.Add(message);
        return Task.CompletedTask;
    }
}

public class RecurringJobTests
{
    private static readonly DateTime Monday = new(2026, 3, 9, 8, 0, 0);

    [Fact]
    public async Task Reminder_job_emails_only_confirmed_appointments_for_tomorrow()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (branch, doctor, patient) = await db.SeedReferenceDataAsync(Monday);
        var clock = new TestClock(Monday);
        var email = new RecordingEmailSender();
        var tomorrow = Monday.Date.AddDays(1);

        // Confirmed tomorrow -> reminded.
        var confirmed = await AddAsync(db, branch, doctor, patient, tomorrow.AddHours(9));
        confirmed.Confirm(Monday);
        await db.Appointments.UpdateAsync(confirmed);

        // Still only requested tomorrow -> not reminded.
        await AddAsync(db, branch, doctor, patient, tomorrow.AddHours(10));

        // Confirmed, but the day after tomorrow -> not reminded yet.
        var later = await AddAsync(db, branch, doctor, patient, tomorrow.AddDays(1).AddHours(9));
        later.Confirm(Monday);
        await db.Appointments.UpdateAsync(later);

        var job = new AppointmentReminderJob(db.Appointments, db.Patients, db.Doctors, db.Branches, email, clock, NullLogger<AppointmentReminderJob>.Instance);
        var sent = await job.RunAsync();

        Assert.Equal(1, sent);
        var message = Assert.Single(email.Sent);
        Assert.Equal(patient.Email, message.To);
        Assert.Contains("09:00", message.Subject);
        Assert.Contains(doctor.FullName, message.HtmlBody);
        Assert.Contains(branch.Name, message.HtmlBody);
    }

    [Fact]
    public async Task Reminder_job_sends_nothing_when_tomorrow_is_empty()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await db.SeedReferenceDataAsync(Monday);
        var email = new RecordingEmailSender();

        var job = new AppointmentReminderJob(db.Appointments, db.Patients, db.Doctors, db.Branches, email, new TestClock(Monday), NullLogger<AppointmentReminderJob>.Instance);

        Assert.Equal(0, await job.RunAsync());
        Assert.Empty(email.Sent);
    }

    [Fact]
    public async Task Follow_up_job_targets_visits_completed_a_week_ago()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (branch, doctor, patient) = await db.SeedReferenceDataAsync(Monday);
        var email = new RecordingEmailSender();
        var clock = new TestClock(Monday);

        // Completed exactly FollowUpAfterDays ago -> followed up.
        var sevenDaysAgo = Monday.Date.AddDays(-FollowUpAlertJob.FollowUpAfterDays).AddHours(10);
        await CompleteAsync(db, branch, doctor, patient, sevenDaysAgo);

        // Completed yesterday -> too early.
        await CompleteAsync(db, branch, doctor, patient, Monday.Date.AddDays(-1).AddHours(10));

        var job = new FollowUpAlertJob(db.Appointments, db.Patients, db.Doctors, email, clock, NullLogger<FollowUpAlertJob>.Instance);
        var sent = await job.RunAsync();

        Assert.Equal(1, sent);
        Assert.Contains("How are you feeling", Assert.Single(email.Sent).Subject);
    }

    private static async Task<Appointment> AddAsync(SqliteTestDatabase db, Branch branch, Doctor doctor, Patient patient, DateTime start)
    {
        var appointment = new Appointment
        {
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            BranchId = branch.Id,
            ScheduledStart = start,
            ScheduledEnd = start.AddMinutes(30),
            Reason = "Follow-up",
            CreatedAt = Monday.AddDays(-2)
        };
        await db.Appointments.CreateAsync(appointment);
        return appointment;
    }

    private static async Task CompleteAsync(SqliteTestDatabase db, Branch branch, Doctor doctor, Patient patient, DateTime start)
    {
        var appointment = await AddAsync(db, branch, doctor, patient, start);
        appointment.Confirm(start.AddDays(-1));
        appointment.CheckIn(start);
        appointment.StartConsultation(start.AddMinutes(5));
        appointment.Complete(start.AddMinutes(20));
        await db.Appointments.UpdateAsync(appointment);
    }
}
