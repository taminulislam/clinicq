namespace ClinicQ.Web.Infrastructure;

/// <summary>
/// Abstracts "now" so scheduling rules and jobs are testable. All clinic timestamps are kept in the
/// clinic's local time zone (all branches operate in one zone) rather than UTC.
/// </summary>
public interface IClock
{
    DateTime Now { get; }
    DateOnly Today => DateOnly.FromDateTime(Now);
}

public sealed class SystemClock : IClock
{
    private readonly TimeZoneInfo _timeZone;

    public SystemClock(IConfiguration configuration)
    {
        var id = configuration["Clinic:TimeZoneId"];
        _timeZone = ResolveTimeZone(id);
    }

    public DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _timeZone);

    private static TimeZoneInfo ResolveTimeZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return TimeZoneInfo.Local;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Local;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Local;
        }
    }
}
