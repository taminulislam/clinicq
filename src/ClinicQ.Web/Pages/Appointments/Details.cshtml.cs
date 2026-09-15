using ClinicQ.Domain.Appointments;
using ClinicQ.Domain.Billing;
using ClinicQ.Domain.Entities;
using ClinicQ.Domain.Exceptions;
using ClinicQ.Web.Data.Repositories;
using ClinicQ.Web.Services;
using ClinicQ.Web.Ui;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClinicQ.Web.Pages.Appointments;

public sealed class DetailsModel : PageModel
{
    private readonly AppointmentService _service;
    private readonly IPatientRepository _patients;
    private readonly IDoctorRepository _doctors;
    private readonly IBranchRepository _branches;
    private readonly IInvoiceRepository _invoices;
    private readonly IPrescriptionRepository _prescriptions;
    private readonly ILabReportRepository _labReports;

    public DetailsModel(
        AppointmentService service,
        IPatientRepository patients,
        IDoctorRepository doctors,
        IBranchRepository branches,
        IInvoiceRepository invoices,
        IPrescriptionRepository prescriptions,
        ILabReportRepository labReports)
    {
        _service = service;
        _patients = patients;
        _doctors = doctors;
        _branches = branches;
        _invoices = invoices;
        _prescriptions = prescriptions;
        _labReports = labReports;
    }

    public Appointment Appointment { get; private set; } = new();
    public Patient Patient { get; private set; } = new();
    public Doctor Doctor { get; private set; } = new();
    public Branch Branch { get; private set; } = new();
    public Invoice? Invoice { get; private set; }
    public IReadOnlyList<Prescription> Prescriptions { get; private set; } = Array.Empty<Prescription>();
    public IReadOnlyList<LabReport> LabReports { get; private set; } = Array.Empty<LabReport>();

    public bool CanPrescribe => Appointment.Status is AppointmentStatus.InConsultation or AppointmentStatus.Completed;
    public bool CanInvoice => Appointment.Status == AppointmentStatus.Completed && Invoice is null;

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        try
        {
            Appointment = await _service.GetRequiredAsync(id, cancellationToken);
        }
        catch (EntityNotFoundException)
        {
            return NotFound();
        }

        Patient = await _patients.GetByIdAsync(Appointment.PatientId, cancellationToken) ?? new Patient();
        Doctor = await _doctors.GetByIdAsync(Appointment.DoctorId, cancellationToken) ?? new Doctor();
        Branch = await _branches.GetByIdAsync(Appointment.BranchId, cancellationToken) ?? new Branch();
        Invoice = await _invoices.GetByAppointmentIdAsync(id, cancellationToken);
        Prescriptions = await _prescriptions.ListByAppointmentAsync(id, cancellationToken);
        LabReports = (await _labReports.ListByPatientAsync(Appointment.PatientId, cancellationToken))
            .Where(r => r.AppointmentId == id)
            .ToList();
        return Page();
    }

    /// <summary>Status move posted from the shared _AppointmentActions partial (also used by the Today queue).</summary>
    public async Task<IActionResult> OnPostTransitionAsync(int id, AppointmentStatus target, string? text, string? returnUrl, CancellationToken cancellationToken)
    {
        await this.TryDomainAsync(
            () => _service.MoveToAsync(id, target, text, cancellationToken),
            $"Appointment #{id} moved to {Badges.Label(target)}.");

        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToPage(new { id });
    }
}
