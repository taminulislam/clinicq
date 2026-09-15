namespace ClinicQ.Domain.Appointments;

/// <summary>
/// Lifecycle of a clinic appointment. Transitions are governed by <see cref="AppointmentStateMachine"/>.
/// </summary>
public enum AppointmentStatus
{
    Requested = 0,
    Confirmed = 1,
    CheckedIn = 2,
    InConsultation = 3,
    Completed = 4,
    Cancelled = 5,
    NoShow = 6
}
