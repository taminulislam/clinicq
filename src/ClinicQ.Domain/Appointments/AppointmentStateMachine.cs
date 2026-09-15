using ClinicQ.Domain.Exceptions;

namespace ClinicQ.Domain.Appointments;

/// <summary>
/// Declares the legal appointment status transitions:
/// Requested -> Confirmed -> CheckedIn -> InConsultation -> Completed,
/// with Cancelled and NoShow as terminal branches.
/// </summary>
public static class AppointmentStateMachine
{
    private static readonly IReadOnlyDictionary<AppointmentStatus, IReadOnlySet<AppointmentStatus>> Transitions =
        new Dictionary<AppointmentStatus, IReadOnlySet<AppointmentStatus>>
        {
            [AppointmentStatus.Requested] = new HashSet<AppointmentStatus>
            {
                AppointmentStatus.Confirmed,
                AppointmentStatus.Cancelled
            },
            [AppointmentStatus.Confirmed] = new HashSet<AppointmentStatus>
            {
                AppointmentStatus.CheckedIn,
                AppointmentStatus.Cancelled,
                AppointmentStatus.NoShow
            },
            [AppointmentStatus.CheckedIn] = new HashSet<AppointmentStatus>
            {
                AppointmentStatus.InConsultation,
                AppointmentStatus.Cancelled
            },
            [AppointmentStatus.InConsultation] = new HashSet<AppointmentStatus>
            {
                AppointmentStatus.Completed
            },
            [AppointmentStatus.Completed] = new HashSet<AppointmentStatus>(),
            [AppointmentStatus.Cancelled] = new HashSet<AppointmentStatus>(),
            [AppointmentStatus.NoShow] = new HashSet<AppointmentStatus>()
        };

    public static bool CanTransition(AppointmentStatus from, AppointmentStatus to)
        => Transitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    public static IReadOnlyCollection<AppointmentStatus> NextStates(AppointmentStatus from)
        => Transitions.TryGetValue(from, out var allowed) ? allowed.ToArray() : Array.Empty<AppointmentStatus>();

    public static bool IsTerminal(AppointmentStatus status) => NextStates(status).Count == 0;

    /// <summary>
    /// Throws <see cref="InvalidAppointmentTransitionException"/> when the move is not permitted.
    /// </summary>
    public static void EnsureTransition(AppointmentStatus from, AppointmentStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidAppointmentTransitionException(from, to);
        }
    }
}
