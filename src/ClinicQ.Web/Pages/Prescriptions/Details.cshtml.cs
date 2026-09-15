using ClinicQ.Domain.Entities;
using ClinicQ.Domain.Exceptions;
using ClinicQ.Web.Data.Repositories;
using ClinicQ.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClinicQ.Web.Pages.Prescriptions;

public sealed class DetailsModel : PageModel
{
    private readonly PrescriptionService _service;
    private readonly IPatientRepository _patients;
    private readonly IDoctorRepository _doctors;

    public DetailsModel(PrescriptionService service, IPatientRepository patients, IDoctorRepository doctors)
    {
        _service = service;
        _patients = patients;
        _doctors = doctors;
    }

    public Prescription Prescription { get; private set; } = new();
    public Patient Patient { get; private set; } = new();
    public Doctor Doctor { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        try
        {
            Prescription = await _service.GetRequiredAsync(id, cancellationToken);
        }
        catch (EntityNotFoundException)
        {
            return NotFound();
        }

        Patient = await _patients.GetByIdAsync(Prescription.PatientId, cancellationToken) ?? new Patient();
        Doctor = await _doctors.GetByIdAsync(Prescription.DoctorId, cancellationToken) ?? new Doctor();
        return Page();
    }

    /// <summary>QuestPDF prescription.</summary>
    public async Task<IActionResult> OnGetPdfAsync(int id, CancellationToken cancellationToken)
    {
        try
        {
            var bytes = await _service.RenderPdfAsync(id, cancellationToken);
            return File(bytes, "application/pdf", $"prescription-{id}.pdf");
        }
        catch (EntityNotFoundException)
        {
            return NotFound();
        }
    }
}
