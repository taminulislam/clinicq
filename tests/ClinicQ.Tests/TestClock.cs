using ClinicQ.Web.Infrastructure;

namespace ClinicQ.Tests;

/// <summary>Deterministic clock so slot rules and job windows are testable.</summary>
public sealed class TestClock : IClock
{
    public TestClock(DateTime now)
    {
        Now = now;
    }

    public DateTime Now { get; set; }
}
