namespace ClinicQ.Domain.Entities;

/// <summary>
/// Result of one nightly billing reconciliation pass for a branch.
/// </summary>
public class ReconciliationRun
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public DateTime RunAt { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public int InvoiceCount { get; set; }
    public decimal TotalInvoiced { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal Outstanding { get; set; }
    public int UnbilledCompletedAppointments { get; set; }
    public string Notes { get; set; } = string.Empty;
}
