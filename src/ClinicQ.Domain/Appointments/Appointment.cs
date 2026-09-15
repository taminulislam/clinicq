using ClinicQ.Domain.Exceptions;

namespace ClinicQ.Domain.Appointments;

/// <summary>
/// Aggregate root for a booked visit. All status changes go through the guarded methods below.
/// </summary>
public class Appointment
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public int DoctorId { get; set; }
    public int BranchId { get; set; }
    public DateTime ScheduledStart { get; set; }
    public DateTime ScheduledEnd { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Requested;
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public DateTime? ConsultationStartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }

    /// <summary>Minutes the patient waited between check-in and the start of consultation.</summary>
    public double? WaitMinutes =>
        CheckedInAt.HasValue && ConsultationStartedAt.HasValue
            ? (ConsultationStartedAt.Value - CheckedInAt.Value).TotalMinutes
            : null;

    public void Confirm(DateTime now)
    {
        Transition(AppointmentStatus.Confirmed);
        ConfirmedAt = now;
    }

    public void CheckIn(DateTime now)
    {
        Transition(AppointmentStatus.CheckedIn);
        CheckedInAt = now;
    }

    public void StartConsultation(DateTime now)
    {
        Transition(AppointmentStatus.InConsultation);
        ConsultationStartedAt = now;
    }

    public void Complete(DateTime now, string? notes = null)
    {
        Transition(AppointmentStatus.Completed);
        CompletedAt = now;
        if (!string.IsNullOrWhiteSpace(notes))
        {
            Notes = notes;
        }
    }

    public void Cancel(DateTime now, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("A cancellation reason is required.");
        }

        Transition(AppointmentStatus.Cancelled);
        CancelledAt = now;
        CancellationReason = reason.Trim();
    }

    public void MarkNoShow(DateTime now)
    {
        if (now < ScheduledStart)
        {
            throw new DomainException("An appointment cannot be marked as no-show before its scheduled start time.");
        }

        Transition(AppointmentStatus.NoShow);
        CancelledAt = now;
    }

    private void Transition(AppointmentStatus to)
    {
        AppointmentStateMachine.EnsureTransition(Status, to);
        Status = to;
    }
}
