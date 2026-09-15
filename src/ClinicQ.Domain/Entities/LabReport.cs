namespace ClinicQ.Domain.Entities;

/// <summary>
/// Metadata for an uploaded lab result. The binary lives in blob/disk storage at <see cref="StoragePath"/>.
/// </summary>
public class LabReport
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public int? AppointmentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public string TestName { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime UploadedAt { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
}
