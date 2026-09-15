using ClinicQ.Domain.Appointments;
using ClinicQ.Domain.Billing;
using ClinicQ.Domain.Entities;
using ClinicQ.Web.Data.Models;

namespace ClinicQ.Tests.Data;

/// <summary>
/// Exercises the Dapper repositories against the SQLite fallback schema, which is what the app uses
/// when no SQL Server connection string is configured.
/// </summary>
public class RepositoryTests
{
    private static readonly DateTime Now = new(2026, 3, 9, 8, 0, 0); // a Monday

    [Fact]
    public async Task Patients_round_trip_and_are_searchable()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (_, _, patient) = await db.SeedReferenceDataAsync(Now);

        var loaded = await db.Patients.GetByIdAsync(patient.Id);
        Assert.NotNull(loaded);
        Assert.Equal("Ada", loaded!.FirstName);
        Assert.Equal(patient.Mrn, loaded.Mrn);
        Assert.Equal(new DateTime(1990, 12, 10), loaded.DateOfBirth);

        Assert.Equal(patient.Id, (await db.Patients.GetByMrnAsync(patient.Mrn))!.Id);
        Assert.Single(await db.Patients.SearchAsync("lovelace"));   // case-insensitive
        Assert.Single(await db.Patients.SearchAsync("ADA"));
        Assert.Empty(await db.Patients.SearchAsync("nobody"));
        Assert.Equal(1, await db.Patients.CountAsync());

        loaded.City = "Chicago";
        await db.Patients.UpdateAsync(loaded);
        Assert.Equal("Chicago", (await db.Patients.GetByIdAsync(patient.Id))!.City);
    }

    [Fact]
    public async Task Mrn_generation_does_not_collide()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await db.SeedReferenceDataAsync(Now);

        var second = new Patient
        {
            Mrn = await db.Patients.NextMrnAsync(),
            FirstName = "Grace",
            LastName = "Hopper",
            DateOfBirth = new DateTime(1906, 12, 9),
            Gender = "Female",
            Email = "grace@example.com",
            Phone = "(217) 555-0124",
            CreatedAt = Now
        };
        await db.Patients.CreateAsync(second);

        Assert.Equal(2, await db.Patients.CountAsync());
        Assert.NotEqual("MRN-100001", second.Mrn);
    }

    [Fact]
    public async Task Appointments_persist_status_and_timestamps()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (branch, doctor, patient) = await db.SeedReferenceDataAsync(Now);
        var start = Now.AddDays(1).Date.AddHours(9);

        var appointment = new Appointment
        {
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            BranchId = branch.Id,
            ScheduledStart = start,
            ScheduledEnd = start.AddMinutes(30),
            Reason = "Follow-up",
            CreatedAt = Now
        };
        await db.Appointments.CreateAsync(appointment);
        Assert.True(appointment.Id > 0);

        appointment.Confirm(Now.AddMinutes(5));
        appointment.CheckIn(start.AddMinutes(-4));
        await db.Appointments.UpdateAsync(appointment);

        var reloaded = await db.Appointments.GetByIdAsync(appointment.Id);
        Assert.Equal(AppointmentStatus.CheckedIn, reloaded!.Status);
        Assert.Equal(start, reloaded.ScheduledStart);
        Assert.Equal(start.AddMinutes(-4), reloaded.CheckedInAt);
        Assert.Null(reloaded.CompletedAt);

        var listed = await db.Appointments.GetListItemAsync(appointment.Id);
        Assert.Equal("Ada Lovelace", listed!.PatientName);
        Assert.Equal("Dr. Test Example", listed.DoctorName);
        Assert.Equal("Test Clinic", listed.BranchName);
    }

    [Fact]
    public async Task Appointment_filters_narrow_by_status_and_date()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (branch, doctor, patient) = await db.SeedReferenceDataAsync(Now);
        var tomorrow = Now.AddDays(1).Date.AddHours(9);

        var confirmed = await AddAppointmentAsync(db, branch, doctor, patient, tomorrow);
        confirmed.Confirm(Now);
        await db.Appointments.UpdateAsync(confirmed);
        await AddAppointmentAsync(db, branch, doctor, patient, tomorrow.AddDays(7));

        Assert.Equal(2, (await db.Appointments.ListAsync(new AppointmentFilter { BranchId = branch.Id })).Count);
        Assert.Single(await db.Appointments.ListAsync(new AppointmentFilter { Status = AppointmentStatus.Confirmed }));
        Assert.Single(await db.Appointments.ListAsync(new AppointmentFilter { From = tomorrow, To = tomorrow.AddDays(1) }));
        Assert.Empty(await db.Appointments.ListAsync(new AppointmentFilter { DoctorId = doctor.Id + 99 }));
    }

    [Fact]
    public async Task Booked_counts_ignore_cancelled_and_no_show_appointments()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (branch, doctor, patient) = await db.SeedReferenceDataAsync(Now);
        var day = Now.AddDays(1).Date;
        var nine = day.AddHours(9);

        await AddAppointmentAsync(db, branch, doctor, patient, nine);

        var cancelled = await AddAppointmentAsync(db, branch, doctor, patient, nine.AddMinutes(30));
        cancelled.Cancel(Now, "Patient rescheduled");
        await db.Appointments.UpdateAsync(cancelled);

        var counts = await db.Appointments.GetBookedCountsAsync(doctor.Id, DateOnly.FromDateTime(day));

        Assert.Equal(1, counts[nine]);
        Assert.False(counts.ContainsKey(nine.AddMinutes(30)));
    }

    [Fact]
    public async Task Slot_repository_marks_booked_slots_unavailable()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (branch, doctor, patient) = await db.SeedReferenceDataAsync(Now);
        var day = Now.AddDays(1).Date; // Tuesday
        await AddAppointmentAsync(db, branch, doctor, patient, day.AddHours(9));

        var slots = await db.Slots.GetAvailableSlotsAsync(branch.Id, doctor.Id, DateOnly.FromDateTime(day), Now);

        Assert.Equal(6, slots.Count);                       // 09:00-12:00 in 30 minute slots
        Assert.False(slots[0].IsAvailable);                 // taken
        Assert.True(slots.Skip(1).All(s => s.IsAvailable));
    }

    [Fact]
    public async Task Invoices_persist_lines_and_payments()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (branch, doctor, patient) = await db.SeedReferenceDataAsync(Now);
        var appointment = await CompletedAppointmentAsync(db, branch, doctor, patient);

        var schedule = await db.Branches.GetFeeScheduleAsync(branch.Id);
        var calculation = new FeeCalculator().Calculate(schedule, new[] { new ServiceLineRequest("CONSULT"), new ServiceLineRequest("LAB-CBC", 2) }, 10m, branch.TaxRatePercent);
        var number = await db.Invoices.NextInvoiceNumberAsync(branch.Code, Now);
        var invoice = Invoice.FromCalculation(calculation, appointment.Id, patient.Id, branch.Id, Now, number);
        await db.Invoices.CreateAsync(invoice);

        var reloaded = await db.Invoices.GetByIdAsync(invoice.Id);
        Assert.Equal(2, reloaded!.Lines.Count);
        Assert.Equal(210m, reloaded.Subtotal);
        Assert.Equal(21m, reloaded.DiscountAmount);
        Assert.Equal(18.90m, reloaded.TaxAmount);
        Assert.Equal(207.90m, reloaded.Total);

        reloaded.ApplyPayment(200m, Now.AddHours(1));
        await db.Invoices.UpdateAsync(reloaded);
        await db.Invoices.AddPaymentAsync(new Payment
        {
            InvoiceId = reloaded.Id,
            Amount = 200m,
            Method = PaymentMethod.Card,
            Reference = "RCPT-1",
            PaidAt = Now.AddHours(1),
            ReceivedBy = "reception"
        });

        var afterPayment = await db.Invoices.GetByIdAsync(invoice.Id);
        Assert.Equal(InvoiceStatus.PartiallyPaid, afterPayment!.Status);
        Assert.Equal(7.90m, afterPayment.Balance);
        Assert.Equal(PaymentMethod.Card, (await db.Invoices.GetPaymentsAsync(invoice.Id)).Single().Method);
        Assert.Equal(invoice.Id, (await db.Invoices.GetByAppointmentIdAsync(appointment.Id))!.Id);
    }

    [Fact]
    public async Task Prescriptions_round_trip_with_their_items()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (branch, doctor, patient) = await db.SeedReferenceDataAsync(Now);
        var appointment = await CompletedAppointmentAsync(db, branch, doctor, patient);

        var prescription = new Prescription
        {
            AppointmentId = appointment.Id,
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            IssuedAt = Now,
            Diagnosis = "Sinusitis",
            Items =
            {
                new PrescriptionItem { Medication = "Amoxicillin", Dosage = "500 mg", Frequency = "Three times daily", DurationDays = 7 },
                new PrescriptionItem { Medication = "Ibuprofen", Dosage = "400 mg", Frequency = "As needed", DurationDays = 5 }
            }
        };
        await db.Prescriptions.CreateAsync(prescription);

        var byPatient = await db.Prescriptions.ListByPatientAsync(patient.Id);
        Assert.Single(byPatient);
        Assert.Equal(2, byPatient[0].Items.Count);
        Assert.Equal("Amoxicillin", byPatient[0].Items[0].Medication);
        Assert.Single(await db.Prescriptions.ListByAppointmentAsync(appointment.Id));
    }

    [Fact]
    public async Task Lab_reports_are_listed_for_a_patient()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (_, _, patient) = await db.SeedReferenceDataAsync(Now);

        await db.LabReports.CreateAsync(new LabReport
        {
            PatientId = patient.Id,
            FileName = "cbc.pdf",
            ContentType = "application/pdf",
            SizeBytes = 2048,
            StoragePath = "lab-reports/2026/03/cbc.pdf",
            TestName = "Complete blood count",
            UploadedAt = Now,
            UploadedBy = "reception"
        });

        var reports = await db.LabReports.ListByPatientAsync(patient.Id);
        Assert.Single(reports);
        Assert.Equal(2048, reports[0].SizeBytes);
        Assert.Single(await db.LabReports.ListRecentAsync());
    }

    [Fact]
    public async Task Users_are_looked_up_case_insensitively()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await db.Users.CreateAsync(new AppUser
        {
            Username = "Reception",
            DisplayName = "Front Desk",
            Email = "reception@clinicq.example",
            PasswordHash = "hash",
            Role = Roles.Receptionist
        });

        Assert.NotNull(await db.Users.GetByUsernameAsync("reception"));
        Assert.NotNull(await db.Users.GetByUsernameAsync("RECEPTION"));
        Assert.Null(await db.Users.GetByUsernameAsync("missing"));
    }

    [Fact]
    public async Task Reconciliation_totals_invoices_and_flags_unbilled_visits()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (branch, doctor, patient) = await db.SeedReferenceDataAsync(Now);

        var billed = await CompletedAppointmentAsync(db, branch, doctor, patient);
        var schedule = await db.Branches.GetFeeScheduleAsync(branch.Id);
        var calculation = new FeeCalculator().Calculate(schedule, new[] { new ServiceLineRequest("CONSULT") });
        var invoice = Invoice.FromCalculation(calculation, billed.Id, patient.Id, branch.Id, Now, "INV-TST-202603-00001");
        invoice.ApplyPayment(50m, Now);
        await db.Invoices.CreateAsync(invoice);

        // A second completed visit that was never invoiced.
        await CompletedAppointmentAsync(db, branch, doctor, patient, Now.AddHours(2));

        var run = await db.Reconciliation.ReconcileAsync(branch.Id, Now.Date, Now.Date.AddDays(1), Now.AddDays(1));

        Assert.Equal(1, run.InvoiceCount);
        Assert.Equal(120m, run.TotalInvoiced);
        Assert.Equal(50m, run.TotalPaid);
        Assert.Equal(70m, run.Outstanding);
        Assert.Equal(1, run.UnbilledCompletedAppointments);
        Assert.Contains("no invoice", run.Notes);
        Assert.Single(await db.Reconciliation.ListRunsAsync(branch.Id));
    }

    [Fact]
    public async Task Dashboard_queries_aggregate_utilization_waits_and_revenue()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (branch, doctor, patient) = await db.SeedReferenceDataAsync(Now);
        var appointment = await CompletedAppointmentAsync(db, branch, doctor, patient);

        var schedule = await db.Branches.GetFeeScheduleAsync(branch.Id);
        var calculation = new FeeCalculator().Calculate(schedule, new[] { new ServiceLineRequest("CONSULT") });
        var invoice = Invoice.FromCalculation(calculation, appointment.Id, patient.Id, branch.Id, Now, "INV-TST-202603-00002");
        invoice.ApplyPayment(120m, Now);
        await db.Invoices.CreateAsync(invoice);

        var from = Now.Date;
        var to = Now.Date.AddDays(1);

        var utilization = await db.Dashboard.GetDoctorUtilizationAsync(from, to);
        Assert.Equal(1, utilization.Single(u => u.DoctorId == doctor.Id).Completed);

        var waits = await db.Dashboard.GetWaitTimesAsync(from, to);
        Assert.Equal(10, (waits.Single().ConsultationStartedAt - waits.Single().CheckedInAt).TotalMinutes);

        var revenue = await db.Dashboard.GetBranchRevenueAsync(from, to);
        Assert.Equal(120m, revenue.Single().TotalInvoiced);
        Assert.Equal(120m, revenue.Single().TotalCollected);

        var statuses = await db.Dashboard.GetStatusCountsAsync(from, to);
        Assert.Equal(1, statuses.Single(s => s.Status == AppointmentStatus.Completed).Count);
    }

    [Fact]
    public async Task Branch_slot_rules_are_inserted_then_updated()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var (branch, _, _) = await db.SeedReferenceDataAsync(Now);

        var rule = await db.Branches.GetSlotRuleAsync(branch.Id);
        Assert.Equal(new TimeOnly(9, 0), rule!.OpenTime);
        Assert.Equal(30, rule.SlotDurationMinutes);

        rule.OpenTime = new TimeOnly(7, 30);
        rule.SlotDurationMinutes = 20;
        rule.MaxBookingsPerSlot = 2;
        await db.Branches.UpsertSlotRuleAsync(rule);

        var updated = await db.Branches.GetSlotRuleAsync(branch.Id);
        Assert.Equal(new TimeOnly(7, 30), updated!.OpenTime);
        Assert.Equal(20, updated.SlotDurationMinutes);
        Assert.Equal(2, updated.MaxBookingsPerSlot);
    }

    private static async Task<Appointment> AddAppointmentAsync(SqliteTestDatabase db, Branch branch, Doctor doctor, Patient patient, DateTime start)
    {
        var appointment = new Appointment
        {
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            BranchId = branch.Id,
            ScheduledStart = start,
            ScheduledEnd = start.AddMinutes(30),
            Reason = "Follow-up",
            CreatedAt = Now
        };
        await db.Appointments.CreateAsync(appointment);
        return appointment;
    }

    private static async Task<Appointment> CompletedAppointmentAsync(SqliteTestDatabase db, Branch branch, Doctor doctor, Patient patient, DateTime? start = null)
    {
        var slot = start ?? Now.AddHours(1);
        var appointment = await AddAppointmentAsync(db, branch, doctor, patient, slot);

        appointment.Confirm(Now);
        appointment.CheckIn(slot);
        appointment.StartConsultation(slot.AddMinutes(10));
        appointment.Complete(slot.AddMinutes(25));
        await db.Appointments.UpdateAsync(appointment);
        return appointment;
    }
}
