using ClinicQ.Domain.Appointments;
using ClinicQ.Domain.Entities;
using ClinicQ.Web.Data.Models;
using ClinicQ.Web.Data.Repositories;
using ClinicQ.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClinicQ.Web.Pages.Appointments;

public sealed class IndexModel : PageModel
{
    private readonly IAppointmentRepository _appointments;
    private readonly IBranchRepository _branches;
    private readonly IDoctorRepository _doctors;
    private readonly IClock _clock;

    public IndexModel(IAppointmentRepository appointments, IBranchRepository branches, IDoctorRepository doctors, IClock clock)
    {
        _appointments = appointments;
        _branches = branches;
        _doctors = doctors;
        _clock = clock;
    }

    [BindProperty(SupportsGet = true)] public int? BranchId { get; set; }
    [BindProperty(SupportsGet = true)] public int? DoctorId { get; set; }
    [BindProperty(SupportsGet = true)] public AppointmentStatus? Status { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }

    public IReadOnlyList<Branch> Branches { get; private set; } = Array.Empty<Branch>();
    public IReadOnlyList<Doctor> Doctors { get; private set; } = Array.Empty<Doctor>();
    public IReadOnlyList<AppointmentListItem> Items { get; private set; } = Array.Empty<AppointmentListItem>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        From ??= _clock.Today.AddDays(-7);
        To ??= _clock.Today.AddDays(14);

        Branches = await _branches.GetAllAsync(cancellationToken);
        Doctors = await _doctors.GetAllAsync(BranchId, cancellationToken);
        Items = await _appointments.ListAsync(new AppointmentFilter
        {
            BranchId = BranchId,
            DoctorId = DoctorId,
            Status = Status,
            From = From.Value.ToDateTime(TimeOnly.MinValue),
            To = To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue),
            Limit = 2000
        }, cancellationToken);
    }
}
