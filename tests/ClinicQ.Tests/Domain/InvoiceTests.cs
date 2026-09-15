using ClinicQ.Domain.Billing;
using ClinicQ.Domain.Exceptions;

namespace ClinicQ.Tests.Domain;

public class InvoiceTests
{
    private static readonly DateTime Issued = new(2026, 5, 4, 10, 0, 0);

    private static Invoice NewInvoice(decimal total = 100m)
    {
        var calculation = new InvoiceCalculation(
            new[] { new CalculatedLine("CONSULT", "Consultation", 1, total, total) },
            total, 0m, 0m, 0m, 0m, total);

        return Invoice.FromCalculation(calculation, appointmentId: 7, patientId: 3, branchId: 1, Issued, "INV-SPI-202605-00001");
    }

    [Fact]
    public void FromCalculation_copies_totals_lines_and_sets_a_due_date()
    {
        var invoice = NewInvoice(250m);

        Assert.Equal(InvoiceStatus.Issued, invoice.Status);
        Assert.Equal(250m, invoice.Total);
        Assert.Equal(250m, invoice.Balance);
        Assert.Equal(Issued.AddDays(30), invoice.DueDate);
        Assert.Single(invoice.Lines);
        Assert.Equal("CONSULT", invoice.Lines[0].ServiceCode);
    }

    [Fact]
    public void A_partial_payment_leaves_a_balance()
    {
        var invoice = NewInvoice();
        invoice.ApplyPayment(40m, Issued.AddHours(1));

        Assert.Equal(InvoiceStatus.PartiallyPaid, invoice.Status);
        Assert.Equal(40m, invoice.AmountPaid);
        Assert.Equal(60m, invoice.Balance);
        Assert.Null(invoice.PaidAt);
    }

    [Fact]
    public void Paying_the_balance_settles_the_invoice()
    {
        var invoice = NewInvoice();
        invoice.ApplyPayment(40m, Issued.AddHours(1));
        invoice.ApplyPayment(60m, Issued.AddHours(2));

        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.Equal(0m, invoice.Balance);
        Assert.Equal(Issued.AddHours(2), invoice.PaidAt);
    }

    [Fact]
    public void Overpayment_is_rejected()
    {
        var invoice = NewInvoice();

        var ex = Assert.Throws<DomainException>(() => invoice.ApplyPayment(100.01m, Issued));
        Assert.Contains("exceeds the outstanding balance", ex.Message);
        Assert.Equal(0m, invoice.AmountPaid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Payments_must_be_positive(decimal amount)
        => Assert.Throws<DomainException>(() => NewInvoice().ApplyPayment(amount, Issued));

    [Fact]
    public void A_settled_invoice_takes_no_further_payments()
    {
        var invoice = NewInvoice();
        invoice.ApplyPayment(100m, Issued);

        Assert.Throws<DomainException>(() => invoice.ApplyPayment(1m, Issued));
    }

    [Fact]
    public void An_unpaid_invoice_can_be_voided_and_then_rejects_payments()
    {
        var invoice = NewInvoice();
        invoice.Void();

        Assert.Equal(InvoiceStatus.Void, invoice.Status);
        Assert.Throws<DomainException>(() => invoice.ApplyPayment(10m, Issued));
    }

    [Fact]
    public void An_invoice_with_payments_cannot_be_voided()
    {
        var invoice = NewInvoice();
        invoice.ApplyPayment(10m, Issued);

        var ex = Assert.Throws<DomainException>(() => invoice.Void());
        Assert.Contains("recorded payments", ex.Message);
    }
}
