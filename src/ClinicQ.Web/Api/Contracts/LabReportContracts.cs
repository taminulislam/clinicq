using ClinicQ.Domain.Entities;

namespace ClinicQ.Web.Api.Contracts;

/// <summary>Multipart form for POST /api/v1/lab-reports.</summary>
public sealed class LabReportUploadForm
{
    public int PatientId { get; set; }
    public int? AppointmentId { get; set; }
    public string TestName { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public IFormFile? File { get; set; }
}

public sealed record LabReportResponse(
    int Id,
    int PatientId,
    int? AppointmentId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string TestName,
    string? Notes,
    DateTime UploadedAt,
    string UploadedBy)
{
    public static LabReportResponse From(LabReport r) => new(
        r.Id, r.PatientId, r.AppointmentId, r.FileName, r.ContentType, r.SizeBytes, r.TestName, r.Notes, r.UploadedAt, r.UploadedBy);
}

public sealed record DownloadLinkResponse(int Id, string Url, DateTimeOffset ExpiresAt, string Provider);
