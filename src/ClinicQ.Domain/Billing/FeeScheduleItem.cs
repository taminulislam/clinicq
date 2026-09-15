namespace ClinicQ.Domain.Billing;

/// <summary>
/// One priced service on a branch's fee schedule (e.g. CONSULT, FOLLOWUP, LAB-CBC).
/// </summary>
public class FeeScheduleItem
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public string ServiceCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime EffectiveFrom { get; set; }
}
