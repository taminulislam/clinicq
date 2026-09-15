namespace ClinicQ.Domain.Scheduling;

/// <summary>
/// Bit flags for the days a branch accepts appointments.
/// </summary>
public static class WorkingDays
{
    public const int Monday = 1;
    public const int Tuesday = 2;
    public const int Wednesday = 4;
    public const int Thursday = 8;
    public const int Friday = 16;
    public const int Saturday = 32;
    public const int Sunday = 64;

    public const int MondayToFriday = Monday | Tuesday | Wednesday | Thursday | Friday;
    public const int MondayToSaturday = MondayToFriday | Saturday;
    public const int EveryDay = MondayToSaturday | Sunday;

    public static int FlagFor(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => Monday,
        DayOfWeek.Tuesday => Tuesday,
        DayOfWeek.Wednesday => Wednesday,
        DayOfWeek.Thursday => Thursday,
        DayOfWeek.Friday => Friday,
        DayOfWeek.Saturday => Saturday,
        DayOfWeek.Sunday => Sunday,
        _ => 0
    };

    public static bool Contains(int mask, DayOfWeek day) => (mask & FlagFor(day)) != 0;

    public static string Describe(int mask)
    {
        var names = new List<string>();
        foreach (var day in new[]
                 {
                     DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
                     DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
                 })
        {
            if (Contains(mask, day))
            {
                names.Add(day.ToString()[..3]);
            }
        }

        return names.Count == 0 ? "None" : string.Join(", ", names);
    }
}
