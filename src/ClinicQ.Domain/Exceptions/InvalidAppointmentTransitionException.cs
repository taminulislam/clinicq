using ClinicQ.Domain.Appointments;

namespace ClinicQ.Domain.Exceptions;

/// <summary>
/// Raised when an appointment is asked to move to a status the state machine does not allow.
/// </summary>
public sealed class InvalidAppointmentTransitionException : DomainException
{
    public AppointmentStatus From { get; }
    public AppointmentStatus To { get; }

    public InvalidAppointmentTransitionException(AppointmentStatus from, AppointmentStatus to)
        : base($"Appointment cannot move from '{from}' to '{to}'.")
    {
        From = from;
        To = to;
    }
}
