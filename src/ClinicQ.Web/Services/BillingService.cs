using ClinicQ.Domain.Appointments;
using ClinicQ.Domain.Billing;
using ClinicQ.Domain.Exceptions;
using ClinicQ.Web.Data.Repositories;
using ClinicQ.Web.Infrastructure;

namespace ClinicQ.Web.Services;

/// <summary>
/// Generates invoices from completed appointments using the branch fee schedule and records payments.
/// </summary>
public sealed class BillingService
{
    private readonly IInvoiceRepository _invoices;
    private readonly IAppointmentRepository _appointments;
    private readonly IBranchRepository _branches;
    private readonly IPatientRepository _patients;
    private readonly FeeCalculator _calculator;
    private readonly IClock _clock;

    public BillingService(
        IInvoiceRepository invoices,
        IAppointmentRepository appointments,
        IBranchRepository branches,
        IPatientRepository patients,
        FeeCalculator calculator,
        IClock clock)
    {
        _invoices = invoices;
        _appointments = appointments;
        _branches = branches;
        _patients = patients;
        _calculator = calculator;
        _clock = clock;
    }

    public async Task<Invoice> GenerateInvoiceAsync(int appointmentId, IReadOnlyList<ServiceLineRequest> services, decimal? discountPercentOverride = null, CancellationToken cancellationToken = default)
    {
        var appointment = await _appointments.GetByIdAsync(appointmentId, cancellationToken)
                          ?? throw new EntityNotFoundException("Appointment", appointmentId);

        if (appointment.Status != AppointmentStatus.Completed)
        {
            throw new DomainException($"Only completed appointments can be invoiced (current status: {appointment.Status}).");
        }

        var existing = await _invoices.GetByAppointmentIdAsync(appointmentId, cancellationToken);
        if (existing is not null)
        {
            throw new DomainException($"Appointment {appointmentId} already has invoice {existing.InvoiceNumber}.");
        }

        var branch = await _branches.GetByIdAsync(appointment.BranchId, cancellationToken)
                     ?? throw new EntityNotFoundException("Branch", appointment.BranchId);
        var patient = await _patients.GetByIdAsync(appointment.PatientId, cancellationToken)
                      ?? throw new EntityNotFoundException("Patient", appointment.PatientId);
        var schedule = await _branches.GetFeeScheduleAsync(branch.Id, cancellationToken);

        var issuedAt = _clock.Now;
        var discount = discountPercentOverride ?? DiscountPolicy.ForPatientAge(patient.AgeOn(issuedAt));
        var calculation = _calculator.Calculate(schedule, services, discount, branch.TaxRatePercent);
        var number = await _invoices.NextInvoiceNumberAsync(branch.Code, issuedAt, cancellationToken);
        var invoice = Invoice.FromCalculation(calculation, appointment.Id, patient.Id, branch.Id, issuedAt, number);

        await _invoices.CreateAsync(invoice, cancellationToken);
        return invoice;
    }

    public async Task<Invoice> RecordPaymentAsync(int invoiceId, decimal amount, PaymentMethod method, string? reference, string receivedBy, CancellationToken cancellationToken = default)
    {
        var invoice = await GetRequiredAsync(invoiceId, cancellationToken);
        var paidAt = _clock.Now;

        invoice.ApplyPayment(amount, paidAt);
        await _invoices.AddPaymentAsync(new Payment
        {
            InvoiceId = invoice.Id,
            Amount = amount,
            Method = method,
            Reference = reference,
            PaidAt = paidAt,
            ReceivedBy = receivedBy
        }, cancellationToken);
        await _invoices.UpdateAsync(invoice, cancellationToken);
        return invoice;
    }

    public async Task<Invoice> VoidAsync(int invoiceId, CancellationToken cancellationToken = default)
    {
        var invoice = await GetRequiredAsync(invoiceId, cancellationToken);
        invoice.Void();
        await _invoices.UpdateAsync(invoice, cancellationToken);
        return invoice;
    }

    public async Task<Invoice> GetRequiredAsync(int invoiceId, CancellationToken cancellationToken = default)
        => await _invoices.GetByIdAsync(invoiceId, cancellationToken) ?? throw new EntityNotFoundException("Invoice", invoiceId);
}
