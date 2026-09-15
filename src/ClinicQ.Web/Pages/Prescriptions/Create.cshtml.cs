using ClinicQ.Domain.Appointments;
using ClinicQ.Domain.Entities;
using ClinicQ.Domain.Exceptions;
using ClinicQ.Web.Api.Contracts;
using ClinicQ.Web.Data.Repositories;
using ClinicQ.Web.Services;
using ClinicQ.Web.Ui;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClinicQ.Web.Pages.Prescriptions;

[Authorize(Roles = $"{Roles.Admin},{Roles.Doctor}")]
public sealed class CreateModel : PageModel
{
    public const int MaxItems = 5;

    private readonly PrescriptionService _service;
    private readonly IAppointmentRepository _appointments;
    private readonly IPatientRepository _patients;

    public CreateModel(PrescriptionService service, IAppointmentRepository appointments, IPatientRepository patients)
    {
        _service = service;
        _appointments = appointments;
        _patients = patients;
    }

    /// <summary>Validated by CreatePrescriptionRequestValidator; blank rows are dropped before validation matters.</summary>
    [BindProperty]
    public CreatePrescriptionRequest Input { get; set; } = new();

    public Appointment Appointment { get; private set; } = new();
    public Patient Patient { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int appointmentId, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(appointmentId, cancellationToken))
        {
            return NotFound();
        }

        Input = new CreatePrescriptionRequest { AppointmentId = appointmentId, Diagnosis = Appointment.Reason };
        PadItems();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int appointmentId, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(appointmentId, cancellationToken))
        {
            return NotFound();
        }

        // Rows left completely blank in the form are not medications; drop them and re-validate.
        Input.AppointmentId = appointmentId;
        Input.Items = Input.Items.Where(i => !string.IsNullOrWhiteSpace(i.Medication)).ToList();
        ModelState.Clear();
        if (!TryValidateModel(Input, nameof(Input)))
        {
            PadItems();
            return Page();
        }

        try
        {
            var items = Input.Items.Select(i => new PrescriptionItemRequest(i.Medication, i.Dosage, i.Frequency, i.DurationDays, i.Notes)).ToList();
            var rx = await _service.CreateAsync(new PrescriptionRequest(appointmentId, Input.Diagnosis, Input.Instructions, items), cancellationToken);
            this.Flash($"Prescription #{rx.Id} issued.");
            return RedirectToPage("Details", new { id = rx.Id });
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            PadItems();
            return Page();
        }
    }

    private void PadItems()
    {
        while (Input.Items.Count < MaxItems)
        {
            Input.Items.Add(new PrescriptionItemDto());
        }
    }

    private async Task<bool> LoadAsync(int appointmentId, CancellationToken cancellationToken)
    {
        var appointment = await _appointments.GetByIdAsync(appointmentId, cancellationToken);
        if (appointment is null)
        {
            return false;
        }

        Appointment = appointment;
        Patient = await _patients.GetByIdAsync(appointment.PatientId, cancellationToken) ?? new Patient();
        return true;
    }

    public bool CanPrescribe => Appointment.Status is AppointmentStatus.InConsultation or AppointmentStatus.Completed;
}
