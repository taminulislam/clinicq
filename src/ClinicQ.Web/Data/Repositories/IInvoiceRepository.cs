using ClinicQ.Domain.Billing;
using ClinicQ.Web.Data.Models;

namespace ClinicQ.Web.Data.Repositories;

public interface IInvoiceRepository
{
    Task<Invoice?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Invoice?> GetByAppointmentIdAsync(int appointmentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InvoiceListItem>> ListAsync(int? branchId = null, InvoiceStatus? status = null, int? patientId = null, int limit = 200, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(Invoice invoice, CancellationToken cancellationToken = default);
    Task UpdateAsync(Invoice invoice, CancellationToken cancellationToken = default);
    Task<int> AddPaymentAsync(Payment payment, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Payment>> GetPaymentsAsync(int invoiceId, CancellationToken cancellationToken = default);
    Task<string> NextInvoiceNumberAsync(string branchCode, DateTime issuedAt, CancellationToken cancellationToken = default);
}
