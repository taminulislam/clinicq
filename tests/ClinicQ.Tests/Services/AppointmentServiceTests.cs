using ClinicQ.Domain.Appointments;
using ClinicQ.Domain.Exceptions;
using ClinicQ.Domain.Scheduling;
using ClinicQ.Tests.Data;
using ClinicQ.Web.Services;

namespace ClinicQ.Tests.Services;

/// <summary>Booking rules enforced by the application service on top of the SQLite repositories.</summary>
public class AppointmentServiceTests
{
    private static readonly DateTime Monday8Am = new(2026, 3, 9, 8, 0, 0);

    private static AppointmentService Service(SqliteTestDatabase db, TestClock clock)
        => new(db.Appointments, db.Branches, db.Doctors, db.Patients, new SlotGenerator(), clock);

    [Fact]
    public async Task Requesting_a_valid_slot_creates_a_requested_appointment()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (branch, doctor, patient) = await db.SeedReferenceDataAsync(Monday8Am);
        var clock = new TestClock(Monday8Am);
        var start = Monday8Am.Date.AddHours(10);

        var appointment = await Service(db, clock).RequestAsync(
            new AppointmentRequest(patient.Id, doctor.Id, branch.Id, start, "  Chest pain  ", null));

        Assert.True(appointment.Id > 0);
        Assert.Equal(AppointmentStatus.Requested, appointment.Status);
        Assert.Equal(start, appointment.ScheduledStart);
        Assert.Equal(start.AddMinutes(30), appointment.ScheduledEnd);   // slot length from the branch rule
        Assert.Equal("Chest pain", appointment.Reason);                  // trimmed
        Assert.NotNull(await db.Appointments.GetByIdAsync(appointment.Id));
    }

    [Fact]
    public async Task A_slot_cannot_be_double_booked()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (branch, doctor, patient) = await db.SeedReferenceDataAsync(Monday8Am);
        var service = Service(db, new TestClock(Monday8Am));
        var start = Monday8Am.Date.AddHours(10);

        await service.RequestAsync(new AppointmentRequest(patient.Id, doctor.Id, branch.Id, start, "First", null));

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.RequestAsync(new AppointmentRequest(patient.Id, doctor.Id, branch.Id, start, "Second", null)));
        Assert.Contains("fully booked", ex.Message);
    }

    [Fact]
    public async Task Overbooking_is_allowed_up_to_the_branch_capacity()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (branch, doctor, patient) = await db.SeedReferenceDataAsync(Monday8Am);
        var rule = await db.Branches.GetSlotRuleAsync(branch.Id);
        rule!.MaxBookingsPerSlot = 2;
        await db.Branches.UpsertSlotRuleAsync(rule);

        var service = Service(db, new TestClock(Monday8Am));
        var start = Monday8Am.Date.AddHours(10);

        await service.RequestAsync(new AppointmentRequest(patient.Id, doctor.Id, branch.Id, start, "First", null));
        var second = await service.RequestAsync(new AppointmentRequest(patient.Id, doctor.Id, branch.Id, start, "Second", null));

        Assert.True(second.Id > 0);
        await Assert.ThrowsAsync<DomainException>(() =>
            service.RequestAsync(new AppointmentRequest(patient.Id, doctor.Id, branch.Id, start, "Third", null)));
    }

    [Fact]
    public async Task A_cancelled_booking_frees_the_slot_again()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (branch, doctor, patient) = await db.SeedReferenceDataAsync(Monday8Am);
        var service = Service(db, new TestClock(Monday8Am));
        var start = Monday8Am.Date.AddHours(10);

        var first = await service.RequestAsync(new AppointmentRequest(patient.Id, doctor.Id, branch.Id, start, "First", null));
        await service.CancelAsync(first.Id, "Patient rescheduled");

        var replacement = await service.RequestAsync(new AppointmentRequest(patient.Id, doctor.Id, branch.Id, start, "Replacement", null));
        Assert.True(replacement.Id > 0);
    }

    [Fact]
    public async Task Bookings_must_land_on_a_slot_boundary_within_opening_hours()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (branch, doctor, patient) = await db.SeedReferenceDataAsync(Monday8Am);
        var service = Service(db, new TestClock(Monday8Am));

        foreach (var start in new[] { Monday8Am.Date.AddHours(10).AddMinutes(15), Monday8Am.Date.AddHours(8), Monday8Am.Date.AddHours(13) })
        {
            var ex = await Assert.ThrowsAsync<DomainException>(() =>
                service.RequestAsync(new AppointmentRequest(patient.Id, doctor.Id, branch.Id, start, "Bad slot", null)));
            Assert.Contains("boundary", ex.Message);
        }
    }

    [Fact]
    public async Task The_branch_must_be_open_that_day()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (branch, doctor, patient) = await db.SeedReferenceDataAsync(Monday8Am);
        var service = Service(db, new TestClock(Monday8Am));
        var sunday = Monday8Am.Date.AddDays(6).AddHours(10);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.RequestAsync(new AppointmentRequest(patient.Id, doctor.Id, branch.Id, sunday, "Weekend", null)));
        Assert.Contains("closed on Sunday", ex.Message);
    }

    [Fact]
    public async Task Minimum_lead_time_is_enforced()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (branch, doctor, patient) = await db.SeedReferenceDataAsync(Monday8Am);
        var clock = new TestClock(Monday8Am.Date.AddHours(9).AddMinutes(45)); // 09:45, rule requires 1 hour
        var service = Service(db, clock);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.RequestAsync(new AppointmentRequest(patient.Id, doctor.Id, branch.Id, Monday8Am.Date.AddHours(10), "Too soon", null)));
        Assert.Contains("in advance", ex.Message);
    }

    [Fact]
    public async Task Bookings_cannot_exceed_the_advance_window()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (branch, doctor, patient) = await db.SeedReferenceDataAsync(Monday8Am);
        var service = Service(db, new TestClock(Monday8Am));
        var tooFar = Monday8Am.Date.AddDays(35).AddHours(10); // rule allows 30 days

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.RequestAsync(new AppointmentRequest(patient.Id, doctor.Id, branch.Id, tooFar, "Too far", null)));
        Assert.Contains("at most 30 days ahead", ex.Message);
    }

    [Fact]
    public async Task Unknown_patients_doctors_and_branches_are_rejected()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (branch, doctor, patient) = await db.SeedReferenceDataAsync(Monday8Am);
        var service = Service(db, new TestClock(Monday8Am));
        var start = Monday8Am.Date.AddHours(10);

        await Assert.ThrowsAsync<EntityNotFoundException>(() => service.RequestAsync(new AppointmentRequest(999, doctor.Id, branch.Id, start, "x", null)));
        await Assert.ThrowsAsync<EntityNotFoundException>(() => service.RequestAsync(new AppointmentRequest(patient.Id, 999, branch.Id, start, "x", null)));
        await Assert.ThrowsAsync<EntityNotFoundException>(() => service.RequestAsync(new AppointmentRequest(patient.Id, doctor.Id, 999, start, "x", null)));
    }

    [Fact]
    public async Task MoveTo_walks_the_lifecycle_and_persists_each_step()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (branch, doctor, patient) = await db.SeedReferenceDataAsync(Monday8Am);
        var clock = new TestClock(Monday8Am);
        var service = Service(db, clock);
        var start = Monday8Am.Date.AddHours(10);
        var appointment = await service.RequestAsync(new AppointmentRequest(patient.Id, doctor.Id, branch.Id, start, "Checkup", null));

        await service.MoveToAsync(appointment.Id, AppointmentStatus.Confirmed, null);
        clock.Now = start.AddMinutes(-5);
        await service.MoveToAsync(appointment.Id, AppointmentStatus.CheckedIn, null);
        clock.Now = start.AddMinutes(6);
        await service.MoveToAsync(appointment.Id, AppointmentStatus.InConsultation, null);
        clock.Now = start.AddMinutes(20);
        var completed = await service.MoveToAsync(appointment.Id, AppointmentStatus.Completed, "All clear");

        Assert.Equal(AppointmentStatus.Completed, completed.Status);
        Assert.Equal("All clear", completed.Notes);
        Assert.Equal(11, completed.WaitMinutes!.Value, 3);

        var stored = await db.Appointments.GetByIdAsync(appointment.Id);
        Assert.Equal(AppointmentStatus.Completed, stored!.Status);
    }

    [Fact]
    public async Task MoveTo_rejects_an_illegal_jump()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (branch, doctor, patient) = await db.SeedReferenceDataAsync(Monday8Am);
        var service = Service(db, new TestClock(Monday8Am));
        var appointment = await service.RequestAsync(
            new AppointmentRequest(patient.Id, doctor.Id, branch.Id, Monday8Am.Date.AddHours(10), "Checkup", null));

        await Assert.ThrowsAsync<InvalidAppointmentTransitionException>(
            () => service.MoveToAsync(appointment.Id, AppointmentStatus.Completed, null));
        await Assert.ThrowsAsync<DomainException>(
            () => service.MoveToAsync(appointment.Id, AppointmentStatus.Requested, null));
    }

    [Fact]
    public async Task Terminal_appointments_cannot_be_edited()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (branch, doctor, patient) = await db.SeedReferenceDataAsync(Monday8Am);
        var service = Service(db, new TestClock(Monday8Am));
        var appointment = await service.RequestAsync(
            new AppointmentRequest(patient.Id, doctor.Id, branch.Id, Monday8Am.Date.AddHours(10), "Checkup", null));

        var edited = await service.UpdateDetailsAsync(appointment.Id, "Updated reason", "Call before arrival");
        Assert.Equal("Updated reason", edited.Reason);

        await service.CancelAsync(appointment.Id, "Patient rescheduled");
        var ex = await Assert.ThrowsAsync<DomainException>(() => service.UpdateDetailsAsync(appointment.Id, "Nope", null));
        Assert.Contains("no longer be edited", ex.Message);
    }
}
