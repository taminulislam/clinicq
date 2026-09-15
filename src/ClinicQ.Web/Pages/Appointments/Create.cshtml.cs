using System.Globalization;
using ClinicQ.Domain.Entities;
using ClinicQ.Domain.Exceptions;
using ClinicQ.Web.Api.Contracts;
using ClinicQ.Web.Data.Repositories;
using ClinicQ.Web.Infrastructure;
using ClinicQ.Web.Services;
using ClinicQ.Web.Ui;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClinicQ.Web.Pages.Appointments;

/// <summary>
/// Booking screen. Doctors and free slots are fetched with jQuery from the Doctors/Slots page handlers;
/// the posted <see cref="CreateAppointmentRequest"/> is validated by FluentValidation and the slot rules.
/// </summary>
public sealed class CreateModel : PageModel
{
    private readonly AppointmentService _service;
    private readonly IBranchRepository _branches;
    private readonly IDoctorRepository _doctors;
    private readonly IPatientRepository _patients;
    private readonly ISlotRepository _slots;
    private readonly IClock _clock;

    public CreateModel(AppointmentService service, IBranchRepository branches, IDoctorRepository doctors, IPatientRepository patients, ISlotRepository slots, IClock clock)
    {
        _service = service;
        _branches = branches;
        _doctors = doctors;
        _patients = patients;
        _slots = slots;
        _clock = clock;
    }

    [BindProperty]
    public CreateAppointmentRequest Input { get; set; } = new();

    public IReadOnlyList<Branch> Branches { get; private set; } = Array.Empty<Branch>();
    public IReadOnlyList<Patient> Patients { get; private set; } = Array.Empty<Patient>();
    public DateOnly Today { get; private set; }

    public async Task OnGetAsync(int? branchId, int? patientId, CancellationToken cancellationToken)
    {
        Input.BranchId = branchId ?? 0;
        Input.PatientId = patientId ?? 0;
        await LoadListsAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadListsAsync(cancellationToken);
            return Page();
        }

        try
        {
            var appointment = await _service.RequestAsync(
                new AppointmentRequest(Input.PatientId, Input.DoctorId, Input.BranchId, Input.Start, Input.Reason, Input.Notes),
                cancellationToken);
            this.Flash($"Appointment #{appointment.Id} requested for {appointment.ScheduledStart:ddd d MMM HH:mm}.");
            return RedirectToPage("Details", new { id = appointment.Id });
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await LoadListsAsync(cancellationToken);
            return Page();
        }
    }

    public async Task<JsonResult> OnGetDoctorsAsync(int branchId, CancellationToken cancellationToken)
    {
        var doctors = await _doctors.GetAllAsync(branchId, cancellationToken);
        return new JsonResult(doctors.Where(d => d.IsActive).Select(d => new { d.Id, d.FullName, d.Specialty }));
    }

    public async Task<JsonResult> OnGetSlotsAsync(int branchId, int doctorId, string date, CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) || doctorId <= 0)
        {
            return new JsonResult(Array.Empty<object>());
        }

        var slots = await _slots.GetAvailableSlotsAsync(branchId, doctorId, day, _clock.Now, cancellationToken);
        return new JsonResult(slots.Select(s => new
        {
            start = s.Start.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
            label = s.Start.ToString("HH:mm", CultureInfo.InvariantCulture),
            s.Remaining,
            s.IsAvailable
        }));
    }

    private async Task LoadListsAsync(CancellationToken cancellationToken)
    {
        Today = _clock.Today;
        Branches = await _branches.GetAllAsync(cancellationToken);
        Patients = await _patients.SearchAsync(null, 0, 1000, cancellationToken);
        if (Input.BranchId == 0 && Branches.Count > 0)
        {
            Input.BranchId = Branches[0].Id;
        }
    }
}
