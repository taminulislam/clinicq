namespace ClinicQ.Domain.Entities;

/// <summary>
/// A physical clinic location. Slot rules and the fee schedule are configured per branch.
/// </summary>
public class Branch
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AddressLine { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = "Central Standard Time";
    /// <summary>Sales/service tax applied to invoices at this branch, in percent.</summary>
    public decimal TaxRatePercent { get; set; }
    public bool IsActive { get; set; } = true;
}
