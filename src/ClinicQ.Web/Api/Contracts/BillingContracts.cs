using ClinicQ.Domain.Billing;
using ClinicQ.Domain.Entities;

namespace ClinicQ.Web.Api.Contracts;

public sealed class ServiceLineDto
{
    public string ServiceCode { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
}

public sealed class GenerateInvoiceRequest
{
    public int AppointmentId { get; set; }
    public List<ServiceLineDto> Services { get; set; } = new();
    /// <summary>Optional override of the age-based discount policy (0-100).</summary>
    public decimal? DiscountPercent { get; set; }
}

public sealed class RecordPaymentRequest
{
    public decimal Amount { get; set; }
    public string Method { get; set; } = "Card";
    public string? Reference { get; set; }
}

public sealed record InvoiceLineResponse(string ServiceCode, string Description, int Quantity, decimal UnitPrice, decimal LineTotal);

public sealed record PaymentResponse(int Id, decimal Amount, string Method, string? Reference, DateTime PaidAt, string ReceivedBy)
{
    public static PaymentResponse From(Payment p) => new(p.Id, p.Amount, p.Method.ToString(), p.Reference, p.PaidAt, p.ReceivedBy);
}

public sealed record InvoiceResponse(
    int Id,
    string InvoiceNumber,
    int AppointmentId,
    int PatientId,
    int BranchId,
    string Status,
    decimal Subtotal,
    decimal DiscountPercent,
    decimal DiscountAmount,
    decimal TaxRatePercent,
    decimal TaxAmount,
    decimal Total,
    decimal AmountPaid,
    decimal Balance,
    DateTime IssuedAt,
    DateTime DueDate,
    DateTime? PaidAt,
    IReadOnlyList<InvoiceLineResponse> Lines)
{
    public static InvoiceResponse From(Invoice i) => new(
        i.Id, i.InvoiceNumber, i.AppointmentId, i.PatientId, i.BranchId, i.Status.ToString(), i.Subtotal, i.DiscountPercent,
        i.DiscountAmount, i.TaxRatePercent, i.TaxAmount, i.Total, i.AmountPaid, i.Balance, i.IssuedAt, i.DueDate, i.PaidAt,
        i.Lines.Select(l => new InvoiceLineResponse(l.ServiceCode, l.Description, l.Quantity, l.UnitPrice, l.LineTotal)).ToList());
}

public sealed class ReconcileRequest
{
    public DateTime? PeriodStart { get; set; }
    public DateTime? PeriodEnd { get; set; }
}

public sealed record ReconciliationRunResponse(
    int Id, int BranchId, DateTime RunAt, DateTime PeriodStart, DateTime PeriodEnd, int InvoiceCount,
    decimal TotalInvoiced, decimal TotalPaid, decimal Outstanding, int UnbilledCompletedAppointments, string Notes)
{
    public static ReconciliationRunResponse From(ReconciliationRun r) => new(
        r.Id, r.BranchId, r.RunAt, r.PeriodStart, r.PeriodEnd, r.InvoiceCount, r.TotalInvoiced, r.TotalPaid,
        r.Outstanding, r.UnbilledCompletedAppointments, r.Notes);
}
