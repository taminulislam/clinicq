using ClinicQ.Domain.Appointments;
using ClinicQ.Domain.Billing;

namespace ClinicQ.Web.Data.Models;

/// <summary>Appointment row joined with patient, doctor and branch names for lists.</summary>
public sealed class AppointmentListItem
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientMrn { get; set; } = string.Empty;
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public DateTime ScheduledStart { get; set; }
    public DateTime ScheduledEnd { get; set; }
    public AppointmentStatus Status { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class AppointmentFilter
{
    public int? BranchId { get; set; }
    public int? DoctorId { get; set; }
    public int? PatientId { get; set; }
    public AppointmentStatus? Status { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Offset { get; set; }
    public int Limit { get; set; } = 200;
}

public sealed class InvoiceListItem
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public int AppointmentId { get; set; }
    public int PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public InvoiceStatus Status { get; set; }
    public decimal Total { get; set; }
    public decimal AmountPaid { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime DueDate { get; set; }
    public decimal Balance => Total - AmountPaid;
}

public sealed class DoctorUtilizationRow
{
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string Specialty { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public int Completed { get; set; }
    public int Scheduled { get; set; }
    public int NoShows { get; set; }
}

public sealed class BranchRevenueRow
{
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public int InvoiceCount { get; set; }
    public decimal TotalInvoiced { get; set; }
    public decimal TotalCollected { get; set; }
}

public sealed class WaitTimeRow
{
    public int BranchId { get; set; }
    public DateTime CheckedInAt { get; set; }
    public DateTime ConsultationStartedAt { get; set; }
}

public sealed class StatusCountRow
{
    public AppointmentStatus Status { get; set; }
    public int Count { get; set; }
}
