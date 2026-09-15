namespace ClinicQ.Domain.Billing;

public class Payment
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public string? Reference { get; set; }
    public DateTime PaidAt { get; set; }
    public string ReceivedBy { get; set; } = string.Empty;
}
