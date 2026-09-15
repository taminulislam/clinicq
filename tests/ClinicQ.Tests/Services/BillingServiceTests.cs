using ClinicQ.Domain.Appointments;
using ClinicQ.Domain.Billing;
using ClinicQ.Domain.Entities;
using ClinicQ.Domain.Exceptions;
using ClinicQ.Domain.Scheduling;
using ClinicQ.Tests.Data;
using ClinicQ.Web.Data.Repositories;
using ClinicQ.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClinicQ.Tests.Services;

public class BillingServiceTests
{
    private static readonly DateTime Monday = new(2026, 3, 9, 8, 0, 0);

    private static BillingService Billing(SqliteTestDatabase db, TestClock clock)
        => new(db.Invoices, db.Appointments, db.Branches, db.Patients, new FeeCalculator(), clock);

    private static async Task<Appointment> CompletedVisitAsync(SqliteTestDatabase db, Branch branch, Doctor doctor, Patient patient, TestClock clock)
    {
        var service = new AppointmentService(db.Appointments, db.Branches, db.Doctors, db.Patients, new SlotGenerator(), clock);
        var appointment = await service.RequestAsync(new AppointmentRequest(patient.Id, doctor.Id, branch.Id, Monday.Date.AddHours(10), "Checkup", null));

        await service.MoveToAsync(appointment.Id, AppointmentStatus.Confirmed, null);
        clock.Now = Monday.Date.AddHours(10);
        await service.MoveToAsync(appointment.Id, AppointmentStatus.CheckedIn, null);
        clock.Now = Monday.Date.AddHours(10).AddMinutes(8);
        await service.MoveToAsync(appointment.Id, AppointmentStatus.InConsultation, null);
        clock.Now = Monday.Date.AddHours(10).AddMinutes(25);
        await service.MoveToAsync(appointment.Id, AppointmentStatus.Completed, null);
        return appointment;
    }

    [Fact]
    public async Task Generating_an_invoice_prices_services_and_applies_branch_tax()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var clock = new TestClock(Monday);
        var (branch, doctor, patient) = await db.SeedReferenceDataAsync(Monday);
        var appointment = await CompletedVisitAsync(db, branch, doctor, patient, clock);

        var invoice = await Billing(db, clock).GenerateInvoiceAsync(appointment.Id, new[] { new ServiceLineRequest("CONSULT"), new ServiceLineRequest("LAB-CBC") });

        Assert.Equal(165m, invoice.Subtotal);            // 120 + 45
        Assert.Equal(0m, invoice.DiscountAmount);        // adult patient
        Assert.Equal(16.50m, invoice.TaxAmount);         // 10% branch tax
        Assert.Equal(181.50m, invoice.Total);
        Assert.StartsWith("INV-TST-", invoice.InvoiceNumber);
        Assert.Equal(InvoiceStatus.Issued, invoice.Status);
    }

    [Fact]
    public async Task The_age_based_discount_policy_is_applied_by_default()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var clock = new TestClock(Monday);
        var (branch, doctor, patient) = await db.SeedReferenceDataAsync(Monday);

        var senior = await db.Patients.GetByIdAsync(patient.Id);
        senior!.DateOfBirth = Monday.AddYears(-70);
        await db.Patients.UpdateAsync(senior);

        var appointment = await CompletedVisitAsync(db, branch, doctor, senior, clock);
        var invoice = await Billing(db, clock).GenerateInvoiceAsync(appointment.Id, new[] { new ServiceLineRequest("CONSULT") });

        Assert.Equal(10m, invoice.DiscountPercent);
        Assert.Equal(12m, invoice.DiscountAmount);
        Assert.Equal(118.80m, invoice.Total);   // (120 - 12) * 1.10
    }

    [Fact]
    public async Task A_discount_override_replaces_the_policy()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var clock = new TestClock(Monday);
        var (branch, doctor, patient) = await db.SeedReferenceDataAsync(Monday);
        var appointment = await CompletedVisitAsync(db, branch, doctor, patient, clock);

        var invoice = await Billing(db, clock).GenerateInvoiceAsync(appointment.Id, new[] { new ServiceLineRequest("CONSULT") }, discountPercentOverride: 25m);

        Assert.Equal(25m, invoice.DiscountPercent);
        Assert.Equal(30m, invoice.DiscountAmount);
    }

    [Fact]
    public async Task Only_completed_visits_can_be_invoiced()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var clock = new TestClock(Monday);
        var (branch, doctor, patient) = await db.SeedReferenceDataAsync(Monday);
        var appointments = new AppointmentService(db.Appointments, db.Branches, db.Doctors, db.Patients, new SlotGenerator(), clock);
        var pending = await appointments.RequestAsync(new AppointmentRequest(patient.Id, doctor.Id, branch.Id, Monday.Date.AddHours(10), "Checkup", null));

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            Billing(db, clock).GenerateInvoiceAsync(pending.Id, new[] { new ServiceLineRequest("CONSULT") }));
        Assert.Contains("Only completed appointments", ex.Message);
    }

    [Fact]
    public async Task A_visit_cannot_be_invoiced_twice()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var clock = new TestClock(Monday);
        var (branch, doctor, patient) = await db.SeedReferenceDataAsync(Monday);
        var appointment = await CompletedVisitAsync(db, branch, doctor, patient, clock);
        var billing = Billing(db, clock);

        await billing.GenerateInvoiceAsync(appointment.Id, new[] { new ServiceLineRequest("CONSULT") });

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            billing.GenerateInvoiceAsync(appointment.Id, new[] { new ServiceLineRequest("CONSULT") }));
        Assert.Contains("already has invoice", ex.Message);
    }

    [Fact]
    public async Task Recording_payments_settles_the_invoice_and_stores_the_receipts()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var clock = new TestClock(Monday);
        var (branch, doctor, patient) = await db.SeedReferenceDataAsync(Monday);
        var appointment = await CompletedVisitAsync(db, branch, doctor, patient, clock);
        var billing = Billing(db, clock);
        var invoice = await billing.GenerateInvoiceAsync(appointment.Id, new[] { new ServiceLineRequest("FOLLOWUP") }); // 75 + 10% = 82.50

        var partial = await billing.RecordPaymentAsync(invoice.Id, 32.50m, PaymentMethod.Cash, "RCPT-1", "reception");
        Assert.Equal(InvoiceStatus.PartiallyPaid, partial.Status);
        Assert.Equal(50m, partial.Balance);

        var settled = await billing.RecordPaymentAsync(invoice.Id, 50m, PaymentMethod.Card, "RCPT-2", "reception");
        Assert.Equal(InvoiceStatus.Paid, settled.Status);
        Assert.Equal(0m, settled.Balance);

        Assert.Equal(2, (await db.Invoices.GetPaymentsAsync(invoice.Id)).Count);
        Assert.Equal(InvoiceStatus.Paid, (await db.Invoices.GetByIdAsync(invoice.Id))!.Status);
    }

    [Fact]
    public async Task Overpayment_is_refused_and_nothing_is_recorded()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var clock = new TestClock(Monday);
        var (branch, doctor, patient) = await db.SeedReferenceDataAsync(Monday);
        var appointment = await CompletedVisitAsync(db, branch, doctor, patient, clock);
        var billing = Billing(db, clock);
        var invoice = await billing.GenerateInvoiceAsync(appointment.Id, new[] { new ServiceLineRequest("FOLLOWUP") });

        await Assert.ThrowsAsync<DomainException>(() => billing.RecordPaymentAsync(invoice.Id, 1000m, PaymentMethod.Card, null, "reception"));

        Assert.Empty(await db.Invoices.GetPaymentsAsync(invoice.Id));
        Assert.Equal(0m, (await db.Invoices.GetByIdAsync(invoice.Id))!.AmountPaid);
    }

    [Fact]
    public async Task Voiding_frees_the_visit_to_be_invoiced_again()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var clock = new TestClock(Monday);
        var (branch, doctor, patient) = await db.SeedReferenceDataAsync(Monday);
        var appointment = await CompletedVisitAsync(db, branch, doctor, patient, clock);
        var billing = Billing(db, clock);
        var first = await billing.GenerateInvoiceAsync(appointment.Id, new[] { new ServiceLineRequest("CONSULT") });

        await billing.VoidAsync(first.Id);

        var replacement = await billing.GenerateInvoiceAsync(appointment.Id, new[] { new ServiceLineRequest("FOLLOWUP") });
        Assert.NotEqual(first.Id, replacement.Id);
        Assert.Equal(InvoiceStatus.Void, (await db.Invoices.GetByIdAsync(first.Id))!.Status);
    }

    [Fact]
    public async Task The_nightly_job_reconciles_every_branch()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var clock = new TestClock(Monday);
        var (branch, doctor, patient) = await db.SeedReferenceDataAsync(Monday);
        var appointment = await CompletedVisitAsync(db, branch, doctor, patient, clock);
        await Billing(db, clock).GenerateInvoiceAsync(appointment.Id, new[] { new ServiceLineRequest("CONSULT") });

        var job = new ClinicQ.Web.Jobs.BillingReconciliationJob(db.Branches, db.Reconciliation, clock, NullLogger<ClinicQ.Web.Jobs.BillingReconciliationJob>.Instance);
        var runs = await job.RunForPeriodAsync(Monday.Date, Monday.Date.AddDays(1));

        var run = Assert.Single(runs);
        Assert.Equal(branch.Id, run.BranchId);
        Assert.Equal(1, run.InvoiceCount);
        Assert.Equal(132m, run.TotalInvoiced);        // 120 + 10% tax
        Assert.Equal(132m, run.Outstanding);          // nothing paid yet
        Assert.Equal(0, run.UnbilledCompletedAppointments);
    }
}
