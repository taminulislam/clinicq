using ClinicQ.Web.Api.Contracts;
using ClinicQ.Web.Data.Repositories;
using ClinicQ.Web.Infrastructure.Storage;
using ClinicQ.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClinicQ.Web.Api.V1;

[Route("api/v{version:apiVersion}/lab-reports")]
public sealed class LabReportsController : ApiControllerBase
{
    private readonly LabReportService _service;
    private readonly ILabReportRepository _reports;
    private readonly IFileStorage _storage;

    public LabReportsController(LabReportService service, ILabReportRepository reports, IFileStorage storage)
    {
        _service = service;
        _reports = reports;
        _storage = storage;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LabReportResponse>>> Recent([FromQuery] int limit = 50, CancellationToken cancellationToken = default)
        => Ok((await _reports.ListRecentAsync(Math.Clamp(limit, 1, 200), cancellationToken)).Select(LabReportResponse.From).ToList());

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LabReportResponse>> Get(int id, CancellationToken cancellationToken)
        => Ok(LabReportResponse.From(await _service.GetRequiredAsync(id, cancellationToken)));

    /// <summary>Upload a lab report (multipart/form-data: patientId, appointmentId?, testName, notes?, file).</summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    [ProducesResponseType(typeof(LabReportResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LabReportResponse>> Upload([FromForm] LabReportUploadForm form, CancellationToken cancellationToken)
    {
        if (form.File is null || form.File.Length == 0)
        {
            return BadRequest(new ProblemDetails { Status = 400, Title = "A file is required." });
        }

        if (string.IsNullOrWhiteSpace(form.TestName))
        {
            return BadRequest(new ProblemDetails { Status = 400, Title = "testName is required." });
        }

        await using var stream = form.File.OpenReadStream();
        var report = await _service.UploadAsync(new LabReportUpload(
            form.PatientId, form.AppointmentId, form.TestName, form.Notes,
            form.File.FileName, form.File.ContentType, form.File.Length, stream, CurrentUserName), cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = report.Id, version = "1" }, LabReportResponse.From(report));
    }

    /// <summary>Time-limited download link: a SAS URL on Azure Blob Storage, or the API download route on local disk.</summary>
    [HttpGet("{id:int}/download-link")]
    public async Task<ActionResult<DownloadLinkResponse>> DownloadLink(int id, CancellationToken cancellationToken)
    {
        var fallback = Url.ActionLink(nameof(Download), values: new { id, version = "1" }) ?? $"/api/v1/lab-reports/{id}/download";
        var link = await _service.GetDownloadLinkAsync(id, fallback, cancellationToken);
        return Ok(new DownloadLinkResponse(id, link.Url, link.ExpiresAt, _storage.ProviderName));
    }

    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> Download(int id, CancellationToken cancellationToken)
    {
        var (report, content) = await _service.OpenAsync(id, cancellationToken);
        return File(content, report.ContentType, report.FileName);
    }
}
