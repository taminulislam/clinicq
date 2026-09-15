using ClinicQ.Domain.Entities;
using ClinicQ.Web.Api.Contracts;
using ClinicQ.Web.Data.Repositories;
using ClinicQ.Web.Ui;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClinicQ.Web.Pages.Patients;

public sealed class EditModel : PageModel
{
    private readonly IPatientRepository _patients;

    public EditModel(IPatientRepository patients)
    {
        _patients = patients;
    }

    /// <summary>Validated by UpdatePatientRequestValidator (FluentValidation auto-validation).</summary>
    [BindProperty]
    public UpdatePatientRequest Input { get; set; } = new();

    public Patient Patient { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        var patient = await _patients.GetByIdAsync(id, cancellationToken);
        if (patient is null)
        {
            return NotFound();
        }

        Patient = patient;
        Input = new UpdatePatientRequest
        {
            FirstName = patient.FirstName,
            LastName = patient.LastName,
            DateOfBirth = patient.DateOfBirth,
            Gender = patient.Gender,
            Email = patient.Email,
            Phone = patient.Phone,
            AddressLine = patient.AddressLine,
            City = patient.City,
            State = patient.State,
            PostalCode = patient.PostalCode,
            Allergies = patient.Allergies
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, CancellationToken cancellationToken)
    {
        var patient = await _patients.GetByIdAsync(id, cancellationToken);
        if (patient is null)
        {
            return NotFound();
        }

        Patient = patient;
        if (!ModelState.IsValid)
        {
            return Page();
        }

        Input.ApplyTo(patient);
        await _patients.UpdateAsync(patient, cancellationToken);
        this.Flash("Patient record updated.");
        return RedirectToPage("Details", new { id });
    }
}
