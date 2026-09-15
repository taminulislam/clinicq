namespace ClinicQ.Domain.Entities;

public class Doctor
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Specialty { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string LicenseNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
