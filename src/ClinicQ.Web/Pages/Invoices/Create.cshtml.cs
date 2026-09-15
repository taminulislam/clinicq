using ClinicQ.Domain.Appointments;
using ClinicQ.Domain.Billing;
using ClinicQ.Domain.Entities;
using ClinicQ.Domain.Exceptions;
using ClinicQ.Web.Data.Repositories;
using ClinicQ.Web.Infrastructure;
using ClinicQ.Web.Services;
using ClinicQ.Web.Ui;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClinicQ.Web.Pages.Invoices;

/// <summary>Generates an invoice for a completed visit by picking services from the branch fee schedule.</summary>
public sealed class CreateModel : PageModel
{
    private readonly BillingService _billing;
    private readonly IAppointmentRepository _appointments;
    private readonly IBranchRepository _branches;
    private readonly IPatientRepository _patients;
    private readonly IClock _clock;

    public CreateModel(BillingService billing, IAppointmentRepository appointments, IBranchRepository branches, IPatientRepository patients, IClock clock)
    {
        _billing = billing;
        _appointments = appointments;
        _branches = branches;
        _patients = patients;
        _clock = clock;
    }

    public Appointment Appointment { get; private set; } = new();
    public Patient Patient { get; private set; } = new();
    public Branch Branch { get; private set; } = new();
    public IReadOnlyList<FeeScheduleItem> Fees { get; private set; } = Array.Empty<FeeScheduleItem>();
    public decimal PolicyDiscount { get; private set; }

    /// <summary>Quantity per service code; zero means not billed.</summary>
    [BindProperty]
    public Dictionary<string, int> Quantities { get; set; } = new();

    [BindProperty]
    public decimal? DiscountOverride { get; set; }

    public async Task<IActionResult> OnGetAsync(int appointmentId, CancellationToken cancellationToken)
    {
        var guard = await LoadAsync(appointmentId, cancellationToken);
        if (guard is not null)
        {
            return guard;
        }

        Quantities = Fees.ToDictionary(f => f.ServiceCode, f => f.ServiceCode == "FOLLOWUP" ? 1 : 0);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int appointmentId, CancellationToken cancellationToken)
    {
        var guard = await LoadAsync(appointmentId, cancellationToken);
        if (guard is not null)
        {
            return guard;
        }

        var services = Quantities.Where(q => q.Value > 0).Select(q => new ServiceLineRequest(q.Key, q.Value)).ToList();
        if (DiscountOverride is < 0 or > 100)
        {
            ModelState.AddModelError(nameof(DiscountOverride), "Discount must be between 0 and 100.");
            return Page();
        }

        try
        {
            var invoice = await _billing.GenerateInvoiceAsync(appointmentId, services, DiscountOverride, cancellationToken);
            this.Flash($"Invoice {invoice.InvoiceNumber} issued for {invoice.Total:C2}.");
            return RedirectToPage("Details", new { id = invoice.Id });
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return Page();
        }
    }

    private async Task<IActionResult?> LoadAsync(int appointmentId, CancellationToken cancellationToken)
    {
        var appointment = await _appointments.GetByIdAsync(appointmentId, cancellationToken);
        if (appointment is null)
        {
            return NotFound();
        }

        if (appointment.Status != AppointmentStatus.Completed)
        {
            this.FlashError("Only completed visits can be invoiced.");
            return RedirectToPage("/Appointments/Details", new { id = appointmentId });
        }

        Appointment = appointment;
        Patient = await _patients.GetByIdAsync(appointment.PatientId, cancellationToken) ?? new Patient();
        Branch = await _branches.GetByIdAsync(appointment.BranchId, cancellationToken) ?? new Branch();
        Fees = (await _branches.GetFeeScheduleAsync(appointment.BranchId, cancellationToken)).Where(f => f.IsActive).ToList();
        PolicyDiscount = DiscountPolicy.ForPatientAge(Patient.AgeOn(_clock.Now));
        return null;
    }
}
