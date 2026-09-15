using ClinicQ.Domain.Entities;
using ClinicQ.Domain.Exceptions;
using ClinicQ.Web.Data.Repositories;
using ClinicQ.Web.Infrastructure.Storage;
using ClinicQ.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClinicQ.Web.Pages.LabReports;

public sealed class IndexModel : PageModel
{
    private readonly ILabReportRepository _reports;
    private readonly IPatientRepository _patients;
    private readonly LabReportService _service;
    private readonly IFileStorage _storage;

    public IndexModel(ILabReportRepository reports, IPatientRepository patients, LabReportService service, IFileStorage storage)
    {
        _reports = reports;
        _patients = patients;
        _service = service;
        _storage = storage;
    }

    public IReadOnlyList<LabReport> Reports { get; private set; } = Array.Empty<LabReport>();
    public IReadOnlyDictionary<int, Patient> PatientsById { get; private set; } = new Dictionary<int, Patient>();
    public string StorageProvider => _storage.ProviderName;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Reports = await _reports.ListRecentAsync(500, cancellationToken);
        var patients = await _patients.SearchAsync(null, 0, 5000, cancellationToken);
        PatientsById = patients.ToDictionary(p => p.Id);
    }

    /// <summary>
    /// Portal download. With Azure Blob storage this redirects to a short-lived SAS URL; with local disk it streams the file.
    /// </summary>
    public async Task<IActionResult> OnGetDownloadAsync(int id, CancellationToken cancellationToken)
    {
        try
        {
            if (_storage is AzureBlobFileStorage)
            {
                var link = await _service.GetDownloadLinkAsync(id, string.Empty, cancellationToken);
                if (!string.IsNullOrEmpty(link.Url))
                {
                    return Redirect(link.Url);
                }
            }

            var (report, content) = await _service.OpenAsync(id, cancellationToken);
            return File(content, report.ContentType, report.FileName);
        }
        catch (EntityNotFoundException)
        {
            return NotFound();
        }
    }
}
