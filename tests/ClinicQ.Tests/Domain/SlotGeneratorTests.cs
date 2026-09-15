using ClinicQ.Domain.Exceptions;
using ClinicQ.Domain.Scheduling;

namespace ClinicQ.Tests.Domain;

public class SlotGeneratorTests
{
    private readonly SlotGenerator _generator = new();
    private static readonly DateOnly Wednesday = new(2026, 3, 11);
    private static readonly DateOnly Sunday = new(2026, 3, 15);
    private static readonly Dictionary<DateTime, int> NoBookings = new();

    private static BranchSlotRule Rule() => new()
    {
        BranchId = 1,
        OpenTime = new TimeOnly(9, 0),
        CloseTime = new TimeOnly(12, 0),
        SlotDurationMinutes = 30,
        WorkingDaysMask = WorkingDays.MondayToFriday,
        MaxBookingsPerSlot = 1,
        MinLeadTimeHours = 1,
        MaxAdvanceDays = 60
    };

    /// <summary>A reference "now" early enough that lead time never trims the grid.</summary>
    private static DateTime Yesterday(DateOnly day) => day.AddDays(-1).ToDateTime(TimeOnly.MinValue);

    [Fact]
    public void Fills_the_opening_hours_with_fixed_length_slots()
    {
        var slots = _generator.Generate(Rule(), Wednesday, NoBookings, Yesterday(Wednesday));

        Assert.Equal(6, slots.Count);
        Assert.Equal(Wednesday.ToDateTime(new TimeOnly(9, 0)), slots[0].Start);
        Assert.Equal(Wednesday.ToDateTime(new TimeOnly(11, 30)), slots[^1].Start);
        Assert.Equal(Wednesday.ToDateTime(new TimeOnly(12, 0)), slots[^1].End);
        Assert.All(slots, s => Assert.True(s.IsAvailable));
    }

    [Fact]
    public void Returns_nothing_on_a_non_working_day()
        => Assert.Empty(_generator.Generate(Rule(), Sunday, NoBookings, Yesterday(Sunday)));

    [Fact]
    public void Returns_nothing_beyond_the_advance_booking_window()
    {
        var rule = Rule();
        rule.MaxAdvanceDays = 5;
        var now = Wednesday.AddDays(-30).ToDateTime(TimeOnly.MinValue);

        Assert.Empty(_generator.Generate(rule, Wednesday, NoBookings, now));
    }

    [Fact]
    public void Hides_slots_inside_the_minimum_lead_time()
    {
        var rule = Rule();
        rule.MinLeadTimeHours = 2;
        var now = Wednesday.ToDateTime(new TimeOnly(8, 15)); // earliest bookable 10:15

        var slots = _generator.Generate(rule, Wednesday, NoBookings, now);

        Assert.Equal(new[] { "10:30", "11:00", "11:30" }, slots.Select(s => s.Start.ToString("HH:mm")));
    }

    [Fact]
    public void Reports_remaining_capacity_per_slot()
    {
        var rule = Rule();
        rule.MaxBookingsPerSlot = 2;
        var nineThirty = Wednesday.ToDateTime(new TimeOnly(9, 30));
        var booked = new Dictionary<DateTime, int>
        {
            [Wednesday.ToDateTime(new TimeOnly(9, 0))] = 2,
            [nineThirty] = 1
        };

        var slots = _generator.Generate(rule, Wednesday, booked, Yesterday(Wednesday));

        Assert.False(slots[0].IsAvailable);
        Assert.Equal(0, slots[0].Remaining);
        Assert.True(slots[1].IsAvailable);
        Assert.Equal(1, slots[1].Remaining);
    }

    [Fact]
    public void Rejects_a_rule_that_closes_before_it_opens()
    {
        var rule = Rule();
        rule.CloseTime = new TimeOnly(8, 0);

        Assert.Throws<DomainException>(() => _generator.Generate(rule, Wednesday, NoBookings, Yesterday(Wednesday)));
    }

    [Fact]
    public void Rejects_a_non_positive_slot_duration()
    {
        var rule = Rule();
        rule.SlotDurationMinutes = 0;

        Assert.Throws<DomainException>(() => _generator.Generate(rule, Wednesday, NoBookings, Yesterday(Wednesday)));
    }

    [Theory]
    [InlineData("09:00", true)]
    [InlineData("09:30", true)]
    [InlineData("09:15", false)]  // not on a 30-minute boundary
    [InlineData("08:30", false)]  // before opening
    [InlineData("11:45", false)]  // would run past closing
    public void IsOnSlotBoundary_validates_a_requested_start(string time, bool expected)
    {
        var start = Wednesday.ToDateTime(TimeOnly.Parse(time));

        Assert.Equal(expected, _generator.IsOnSlotBoundary(Rule(), start));
    }

    [Fact]
    public void WorkingDays_describes_the_mask()
    {
        Assert.Equal("Mon, Tue, Wed, Thu, Fri", WorkingDays.Describe(WorkingDays.MondayToFriday));
        Assert.True(WorkingDays.Contains(WorkingDays.MondayToSaturday, DayOfWeek.Saturday));
        Assert.False(WorkingDays.Contains(WorkingDays.MondayToSaturday, DayOfWeek.Sunday));
    }
}
