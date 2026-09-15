using ClinicQ.Domain.Entities;
using ClinicQ.Domain.Scheduling;
using ClinicQ.Web.Data.Repositories;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClinicQ.Web.Pages.Branches;

public sealed class IndexModel : PageModel
{
    private readonly IBranchRepository _branches;
    private readonly IDoctorRepository _doctors;

    public IndexModel(IBranchRepository branches, IDoctorRepository doctors)
    {
        _branches = branches;
        _doctors = doctors;
    }

    public IReadOnlyList<BranchRow> Rows { get; private set; } = Array.Empty<BranchRow>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var rows = new List<BranchRow>();
        foreach (var branch in await _branches.GetAllAsync(cancellationToken))
        {
            var rule = await _branches.GetSlotRuleAsync(branch.Id, cancellationToken);
            var doctors = await _doctors.GetAllAsync(branch.Id, cancellationToken);
            rows.Add(new BranchRow(branch, rule, doctors.Count));
        }

        Rows = rows;
    }

    public sealed record BranchRow(Branch Branch, BranchSlotRule? Rule, int DoctorCount);
}
