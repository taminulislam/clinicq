using ClinicQ.Domain.Appointments;

namespace ClinicQ.Web.Api.Contracts;

public sealed class CreateAppointmentRequest
{
    public int PatientId { get; set; }
    public int DoctorId { get; set; }
    public int BranchId { get; set; }
    public DateTime Start { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public sealed class CancelAppointmentRequest
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class CompleteAppointmentRequest
{
    public string? Notes { get; set; }
}

public sealed record AppointmentResponse(
    int Id,
    int PatientId,
    int DoctorId,
    int BranchId,
    DateTime ScheduledStart,
    DateTime ScheduledEnd,
    string Status,
    string Reason,
    string? Notes,
    DateTime CreatedAt,
    DateTime? ConfirmedAt,
    DateTime? CheckedInAt,
    DateTime? ConsultationStartedAt,
    DateTime? CompletedAt,
    DateTime? CancelledAt,
    string? CancellationReason,
    double? WaitMinutes,
    IReadOnlyCollection<string> AllowedTransitions)
{
    public static AppointmentResponse From(Appointment a) => new(
        a.Id, a.PatientId, a.DoctorId, a.BranchId, a.ScheduledStart, a.ScheduledEnd, a.Status.ToString(), a.Reason, a.Notes,
        a.CreatedAt, a.ConfirmedAt, a.CheckedInAt, a.ConsultationStartedAt, a.CompletedAt, a.CancelledAt, a.CancellationReason,
        a.WaitMinutes, AppointmentStateMachine.NextStates(a.Status).Select(s => s.ToString()).ToArray());
}
