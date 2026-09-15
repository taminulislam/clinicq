using ClinicQ.Domain.Entities;
using ClinicQ.Web.Data.Repositories;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClinicQ.Web.Pages.Patients;

public sealed class IndexModel : PageModel
{
    private readonly IPatientRepository _patients;

    public IndexModel(IPatientRepository patients)
    {
        _patients = patients;
    }

    public IReadOnlyList<Patient> Patients { get; private set; } = Array.Empty<Patient>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
        => Patients = await _patients.SearchAsync(null, 0, 5000, cancellationToken);
}
