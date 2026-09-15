using System.Security.Claims;
using ClinicQ.Domain.Appointments;
using ClinicQ.Domain.Entities;
using ClinicQ.Web.Data.Models;
using ClinicQ.Web.Data.Repositories;
using ClinicQ.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClinicQ.Web.Pages;

/// <summary>Front-desk "today" queue: every appointment at a branch today, with one-click status moves.</summary>
public sealed class IndexModel : PageModel
{
    private readonly IAppointmentRepository _appointments;
    private readonly IBranchRepository _branches;
    private readonly IClock _clock;

    public IndexModel(IAppointmentRepository appointments, IBranchRepository branches, IClock clock)
    {
        _appointments = appointments;
        _branches = branches;
        _clock = clock;
    }

    public IReadOnlyList<Branch> Branches { get; private set; } = Array.Empty<Branch>();
    public IReadOnlyList<AppointmentListItem> Queue { get; private set; } = Array.Empty<AppointmentListItem>();
    public int? BranchId { get; private set; }
    public DateOnly Today { get; private set; }

    public int Count(AppointmentStatus status) => Queue.Count(a => a.Status == status);

    public async Task OnGetAsync(int? branchId, CancellationToken cancellationToken)
    {
        Branches = await _branches.GetAllAsync(cancellationToken);
        BranchId = branchId ?? (int.TryParse(User.FindFirstValue("branch_id"), out var own) ? own : Branches.FirstOrDefault()?.Id);
        Today = _clock.Today;

        var start = Today.ToDateTime(TimeOnly.MinValue);
        var rows = await _appointments.ListAsync(new AppointmentFilter
        {
            BranchId = BranchId,
            From = start,
            To = start.AddDays(1),
            Limit = 500
        }, cancellationToken);

        Queue = rows.OrderBy(a => a.ScheduledStart).ThenBy(a => a.DoctorName).ToList();
    }
}
