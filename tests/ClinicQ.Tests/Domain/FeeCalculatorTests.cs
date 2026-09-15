using ClinicQ.Domain.Billing;
using ClinicQ.Domain.Exceptions;

namespace ClinicQ.Tests.Domain;

public class FeeCalculatorTests
{
    private readonly FeeCalculator _calculator = new();

    private static List<FeeScheduleItem> Schedule() => new()
    {
        new FeeScheduleItem { BranchId = 1, ServiceCode = "CONSULT", Description = "Consultation", Amount = 120m, IsActive = true, EffectiveFrom = new DateTime(2024, 1, 1) },
        new FeeScheduleItem { BranchId = 1, ServiceCode = "LAB-CBC", Description = "Blood count", Amount = 45m, IsActive = true, EffectiveFrom = new DateTime(2024, 1, 1) },
        new FeeScheduleItem { BranchId = 1, ServiceCode = "RETIRED", Description = "Withdrawn", Amount = 10m, IsActive = false, EffectiveFrom = new DateTime(2024, 1, 1) }
    };

    [Fact]
    public void Prices_lines_from_the_schedule_and_totals_them()
    {
        var result = _calculator.Calculate(Schedule(), new[] { new ServiceLineRequest("CONSULT"), new ServiceLineRequest("LAB-CBC", 2) });

        Assert.Equal(2, result.Lines.Count);
        Assert.Equal(90m, result.Lines.Single(l => l.ServiceCode == "LAB-CBC").LineTotal);
        Assert.Equal(210m, result.Subtotal);
        Assert.Equal(210m, result.Total);
        Assert.Equal(0m, result.TaxAmount);
    }

    [Fact]
    public void Applies_the_discount_before_tax()
    {
        // 120 - 10% = 108, tax 8% of 108 = 8.64, total 116.64
        var result = _calculator.Calculate(Schedule(), new[] { new ServiceLineRequest("CONSULT") }, discountPercent: 10m, taxRatePercent: 8m);

        Assert.Equal(120m, result.Subtotal);
        Assert.Equal(12m, result.DiscountAmount);
        Assert.Equal(8.64m, result.TaxAmount);
        Assert.Equal(116.64m, result.Total);
    }

    [Fact]
    public void Rounds_money_to_two_decimals_away_from_zero()
    {
        var schedule = new List<FeeScheduleItem>
        {
            new() { ServiceCode = "ODD", Description = "Odd price", Amount = 33.333m, IsActive = true, EffectiveFrom = new DateTime(2024, 1, 1) }
        };

        var result = _calculator.Calculate(schedule, new[] { new ServiceLineRequest("ODD", 3) }, taxRatePercent: 7.25m);

        Assert.Equal(100m, result.Subtotal);      // 99.999 rounded
        Assert.Equal(7.25m, result.TaxAmount);
        Assert.Equal(107.25m, result.Total);
    }

    [Fact]
    public void Uses_the_latest_effective_price_for_a_code()
    {
        var schedule = Schedule();
        schedule.Add(new FeeScheduleItem { ServiceCode = "CONSULT", Description = "Consultation", Amount = 150m, IsActive = true, EffectiveFrom = new DateTime(2026, 1, 1) });

        var result = _calculator.Calculate(schedule, new[] { new ServiceLineRequest("consult") });

        Assert.Equal(150m, result.Total);
    }

    [Fact]
    public void Unknown_or_inactive_service_codes_are_rejected()
    {
        Assert.Throws<DomainException>(() => _calculator.Calculate(Schedule(), new[] { new ServiceLineRequest("NOPE") }));
        Assert.Throws<DomainException>(() => _calculator.Calculate(Schedule(), new[] { new ServiceLineRequest("RETIRED") }));
    }

    [Fact]
    public void An_invoice_needs_at_least_one_line()
        => Assert.Throws<DomainException>(() => _calculator.Calculate(Schedule(), Array.Empty<ServiceLineRequest>()));

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void Quantity_must_be_positive(int quantity)
        => Assert.Throws<DomainException>(() => _calculator.Calculate(Schedule(), new[] { new ServiceLineRequest("CONSULT", quantity) }));

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Discount_must_be_a_percentage(decimal discount)
        => Assert.Throws<DomainException>(() => _calculator.Calculate(Schedule(), new[] { new ServiceLineRequest("CONSULT") }, discount));

    [Fact]
    public void Tax_rate_cannot_be_negative()
        => Assert.Throws<DomainException>(() => _calculator.Calculate(Schedule(), new[] { new ServiceLineRequest("CONSULT") }, 0m, -1m));
}
