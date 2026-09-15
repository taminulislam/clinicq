using ClinicQ.Domain.Exceptions;

namespace ClinicQ.Domain.Billing;

/// <summary>
/// Billing document generated from a completed appointment. Payments move it through
/// Issued -> PartiallyPaid -> Paid; Void is only allowed while nothing has been paid.
/// </summary>
public class Invoice
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public int AppointmentId { get; set; }
    public int PatientId { get; set; }
    public int BranchId { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Issued;
    public decimal Subtotal { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxRatePercent { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public decimal AmountPaid { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? PaidAt { get; set; }
    public List<InvoiceLine> Lines { get; set; } = new();

    public decimal Balance => FeeCalculator.Round(Total - AmountPaid);

    public static Invoice FromCalculation(InvoiceCalculation calc, int appointmentId, int patientId, int branchId, DateTime issuedAt, string invoiceNumber)
    {
        var invoice = new Invoice
        {
            InvoiceNumber = invoiceNumber,
            AppointmentId = appointmentId,
            PatientId = patientId,
            BranchId = branchId,
            Status = InvoiceStatus.Issued,
            Subtotal = calc.Subtotal,
            DiscountPercent = calc.DiscountPercent,
            DiscountAmount = calc.DiscountAmount,
            TaxRatePercent = calc.TaxRatePercent,
            TaxAmount = calc.TaxAmount,
            Total = calc.Total,
            IssuedAt = issuedAt,
            DueDate = issuedAt.AddDays(30)
        };

        invoice.Lines.AddRange(calc.Lines.Select(l => new InvoiceLine
        {
            ServiceCode = l.ServiceCode,
            Description = l.Description,
            Quantity = l.Quantity,
            UnitPrice = l.UnitPrice,
            LineTotal = l.LineTotal
        }));

        return invoice;
    }

    public void ApplyPayment(decimal amount, DateTime paidAt)
    {
        if (amount <= 0m)
        {
            throw new DomainException("Payment amount must be greater than zero.");
        }

        if (Status == InvoiceStatus.Void)
        {
            throw new DomainException($"Invoice {InvoiceNumber} is void and cannot accept payments.");
        }

        if (Status == InvoiceStatus.Paid)
        {
            throw new DomainException($"Invoice {InvoiceNumber} is already paid in full.");
        }

        if (amount > Balance)
        {
            throw new DomainException($"Payment of {amount:0.00} exceeds the outstanding balance of {Balance:0.00}.");
        }

        AmountPaid = FeeCalculator.Round(AmountPaid + amount);
        if (Balance == 0m)
        {
            Status = InvoiceStatus.Paid;
            PaidAt = paidAt;
        }
        else
        {
            Status = InvoiceStatus.PartiallyPaid;
        }
    }

    public void Void()
    {
        if (AmountPaid > 0m)
        {
            throw new DomainException("An invoice with recorded payments cannot be voided.");
        }

        Status = InvoiceStatus.Void;
    }
}
