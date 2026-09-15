using ClinicQ.Domain.Exceptions;

namespace ClinicQ.Domain.Billing;

/// <summary>
/// Prices a set of services against a branch fee schedule and applies discount then tax.
/// All monetary values are rounded half-away-from-zero to two decimals.
/// </summary>
public sealed class FeeCalculator
{
    public InvoiceCalculation Calculate(
        IReadOnlyCollection<FeeScheduleItem> schedule,
        IEnumerable<ServiceLineRequest> services,
        decimal discountPercent = 0m,
        decimal taxRatePercent = 0m)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(services);

        if (discountPercent is < 0m or > 100m)
        {
            throw new DomainException("Discount percent must be between 0 and 100.");
        }

        if (taxRatePercent < 0m)
        {
            throw new DomainException("Tax rate cannot be negative.");
        }

        var lookup = schedule
            .Where(s => s.IsActive)
            .GroupBy(s => s.ServiceCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.EffectiveFrom).First(), StringComparer.OrdinalIgnoreCase);

        var lines = new List<CalculatedLine>();
        foreach (var request in services)
        {
            if (request.Quantity <= 0)
            {
                throw new DomainException($"Quantity for service '{request.ServiceCode}' must be greater than zero.");
            }

            if (!lookup.TryGetValue(request.ServiceCode, out var item))
            {
                throw new DomainException($"Service code '{request.ServiceCode}' is not on the fee schedule for this branch.");
            }

            var lineTotal = Round(item.Amount * request.Quantity);
            lines.Add(new CalculatedLine(item.ServiceCode, item.Description, request.Quantity, item.Amount, lineTotal));
        }

        if (lines.Count == 0)
        {
            throw new DomainException("An invoice must contain at least one service line.");
        }

        var subtotal = Round(lines.Sum(l => l.LineTotal));
        var discount = Round(subtotal * discountPercent / 100m);
        var taxable = subtotal - discount;
        var tax = Round(taxable * taxRatePercent / 100m);
        var total = Round(taxable + tax);

        return new InvoiceCalculation(lines, subtotal, discountPercent, discount, taxRatePercent, tax, total);
    }

    public static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
