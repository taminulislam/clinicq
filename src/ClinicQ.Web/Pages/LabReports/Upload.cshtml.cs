using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using ClinicQ.Domain.Entities;
using ClinicQ.Domain.Exceptions;
using ClinicQ.Web.Data.Repositories;
using ClinicQ.Web.Services;
using ClinicQ.Web.Ui;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClinicQ.Web.Pages.LabReports;

public sealed class UploadModel : PageModel
{
    private readonly LabReportService _service;
    private readonly IPatientRepository _patients;

    public UploadModel(LabReportService service, IPatientRepository patients)
    {
        _service = service;
        _patients = patients;
    }

    [BindProperty]
    public UploadInput Input { get; set; } = new();

    public IReadOnlyList<Patient> Patients { get; private set; } = Array.Empty<Patient>();

    public async Task OnGetAsync(int? patientId, int? appointmentId, CancellationToken cancellationToken)
    {
        Input.PatientId = patientId ?? 0;
        Input.AppointmentId = appointmentId;
        Patients = await _patients.SearchAsync(null, 0, 5000, cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (Input.File is null || Input.File.Length == 0)
        {
            ModelState.AddModelError("Input.File", "Choose a file to upload.");
        }

        if (!ModelState.IsValid)
        {
            Patients = await _patients.SearchAsync(null, 0, 5000, cancellationToken);
            return Page();
        }

        try
        {
            await using var stream = Input.File!.OpenReadStream();
            var report = await _service.UploadAsync(new LabReportUpload(
                Input.PatientId, Input.AppointmentId, Input.TestName, Input.Notes,
                Input.File.FileName, Input.File.ContentType, Input.File.Length, stream,
                User.FindFirstValue(ClaimTypes.Name) ?? "portal"), cancellationToken);

            this.Flash($"Uploaded {report.TestName} ({report.FileName}).");
            return RedirectToPage("/Patients/Details", new { id = report.PatientId });
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            Patients = await _patients.SearchAsync(null, 0, 5000, cancellationToken);
            return Page();
        }
    }

    public sealed class UploadInput
    {
        [Range(1, int.MaxValue, ErrorMessage = "Select a patient.")]
        public int PatientId { get; set; }

        public int? AppointmentId { get; set; }

        [Required, StringLength(150)]
        [Display(Name = "Test name")]
        public string TestName { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Notes { get; set; }

        public IFormFile? File { get; set; }
    }
}
