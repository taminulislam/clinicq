namespace ClinicQ.Domain.Billing;

public sealed record CalculatedLine(string ServiceCode, string Description, int Quantity, decimal UnitPrice, decimal LineTotal);

/// <summary>
/// Output of <see cref="FeeCalculator"/>: priced lines plus rounded totals.
/// </summary>
public sealed record InvoiceCalculation(
    IReadOnlyList<CalculatedLine> Lines,
    decimal Subtotal,
    decimal DiscountPercent,
    decimal DiscountAmount,
    decimal TaxRatePercent,
    decimal TaxAmount,
    decimal Total);
