using ClinicQ.Domain.Entities;
using ClinicQ.Web.Api.Contracts;
using ClinicQ.Web.Data.Repositories;
using ClinicQ.Web.Infrastructure;
using ClinicQ.Web.Ui;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClinicQ.Web.Pages.Patients;

public sealed class CreateModel : PageModel
{
    private readonly IPatientRepository _patients;
    private readonly IClock _clock;

    public CreateModel(IPatientRepository patients, IClock clock)
    {
        _patients = patients;
        _clock = clock;
    }

    /// <summary>Validated by CreatePatientRequestValidator (FluentValidation auto-validation).</summary>
    [BindProperty]
    public CreatePatientRequest Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var patient = new Patient { Mrn = await _patients.NextMrnAsync(cancellationToken), CreatedAt = _clock.Now };
        Input.ApplyTo(patient);
        await _patients.CreateAsync(patient, cancellationToken);

        this.Flash($"Registered {patient.FullName} ({patient.Mrn}).");
        return RedirectToPage("Details", new { id = patient.Id });
    }
}
