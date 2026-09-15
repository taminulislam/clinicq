using ClinicQ.Domain.Entities;
using ClinicQ.Domain.Exceptions;
using ClinicQ.Web.Data.Repositories;
using ClinicQ.Web.Infrastructure;
using ClinicQ.Web.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace ClinicQ.Web.Services;

public sealed record LabReportUpload(int PatientId, int? AppointmentId, string TestName, string? Notes, string FileName, string ContentType, long Length, Stream Content, string UploadedBy);

/// <summary>
/// Stores lab report files through <see cref="IFileStorage"/> and tracks their metadata.
/// </summary>
public sealed class LabReportService
{
    private const string Container = "lab-reports";

    private readonly ILabReportRepository _reports;
    private readonly IPatientRepository _patients;
    private readonly IAppointmentRepository _appointments;
    private readonly IFileStorage _storage;
    private readonly StorageOptions _options;
    private readonly IClock _clock;

    public LabReportService(
        ILabReportRepository reports,
        IPatientRepository patients,
        IAppointmentRepository appointments,
        IFileStorage storage,
        IOptions<StorageOptions> options,
        IClock clock)
    {
        _reports = reports;
        _patients = patients;
        _appointments = appointments;
        _storage = storage;
        _options = options.Value;
        _clock = clock;
    }

    public async Task<LabReport> UploadAsync(LabReportUpload upload, CancellationToken cancellationToken = default)
    {
        _ = await _patients.GetByIdAsync(upload.PatientId, cancellationToken)
            ?? throw new EntityNotFoundException("Patient", upload.PatientId);

        if (upload.AppointmentId.HasValue)
        {
            var appointment = await _appointments.GetByIdAsync(upload.AppointmentId.Value, cancellationToken)
                              ?? throw new EntityNotFoundException("Appointment", upload.AppointmentId.Value);
            if (appointment.PatientId != upload.PatientId)
            {
                throw new DomainException("The appointment does not belong to the specified patient.");
            }
        }

        if (upload.Length <= 0)
        {
            throw new DomainException("The uploaded file is empty.");
        }

        if (upload.Length > _options.MaxUploadBytes)
        {
            throw new DomainException($"The file exceeds the maximum size of {_options.MaxUploadBytes / (1024 * 1024)} MB.");
        }

        var allowed = _options.EffectiveContentTypes;
        if (!allowed.Contains(upload.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new DomainException($"Content type '{upload.ContentType}' is not allowed. Allowed: {string.Join(", ", allowed)}.");
        }

        var stored = await _storage.SaveAsync(Container, upload.FileName, upload.Content, upload.ContentType, cancellationToken);
        var report = new LabReport
        {
            PatientId = upload.PatientId,
            AppointmentId = upload.AppointmentId,
            FileName = Path.GetFileName(upload.FileName),
            ContentType = upload.ContentType,
            SizeBytes = stored.SizeBytes,
            StoragePath = stored.Path,
            TestName = upload.TestName.Trim(),
            Notes = upload.Notes?.Trim(),
            UploadedAt = _clock.Now,
            UploadedBy = upload.UploadedBy
        };

        await _reports.CreateAsync(report, cancellationToken);
        return report;
    }

    public async Task<LabReport> GetRequiredAsync(int id, CancellationToken cancellationToken = default)
        => await _reports.GetByIdAsync(id, cancellationToken) ?? throw new EntityNotFoundException("LabReport", id);

    public async Task<DownloadLink> GetDownloadLinkAsync(int id, string fallbackUrl, CancellationToken cancellationToken = default)
    {
        var report = await GetRequiredAsync(id, cancellationToken);
        return await _storage.GetDownloadLinkAsync(report.StoragePath, TimeSpan.FromMinutes(_options.DownloadLinkMinutes), fallbackUrl, cancellationToken);
    }

    public async Task<(LabReport Report, Stream Content)> OpenAsync(int id, CancellationToken cancellationToken = default)
    {
        var report = await GetRequiredAsync(id, cancellationToken);
        var stream = await _storage.OpenReadAsync(report.StoragePath, cancellationToken)
                     ?? throw new EntityNotFoundException("LabReportFile", report.StoragePath);
        return (report, stream);
    }
}
