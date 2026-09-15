using ClinicQ.Domain.Entities;
using ClinicQ.Web.Data.Models;
using ClinicQ.Web.Data.Repositories;
using ClinicQ.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClinicQ.Web.Pages.Patients;

/// <summary>Patient record: demographics, visit history, prescriptions, lab reports and invoices.</summary>
public sealed class DetailsModel : PageModel
{
    private readonly IPatientRepository _patients;
    private readonly IAppointmentRepository _appointments;
    private readonly IPrescriptionRepository _prescriptions;
    private readonly ILabReportRepository _labReports;
    private readonly IInvoiceRepository _invoices;
    private readonly IClock _clock;

    public DetailsModel(
        IPatientRepository patients,
        IAppointmentRepository appointments,
        IPrescriptionRepository prescriptions,
        ILabReportRepository labReports,
        IInvoiceRepository invoices,
        IClock clock)
    {
        _patients = patients;
        _appointments = appointments;
        _prescriptions = prescriptions;
        _labReports = labReports;
        _invoices = invoices;
        _clock = clock;
    }

    public Patient Patient { get; private set; } = new();
    public int Age { get; private set; }
    public IReadOnlyList<AppointmentListItem> Appointments { get; private set; } = Array.Empty<AppointmentListItem>();
    public IReadOnlyList<Prescription> Prescriptions { get; private set; } = Array.Empty<Prescription>();
    public IReadOnlyList<LabReport> LabReports { get; private set; } = Array.Empty<LabReport>();
    public IReadOnlyList<InvoiceListItem> Invoices { get; private set; } = Array.Empty<InvoiceListItem>();
    public decimal OutstandingBalance => Invoices.Where(i => i.Status != Domain.Billing.InvoiceStatus.Void).Sum(i => i.Balance);

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        var patient = await _patients.GetByIdAsync(id, cancellationToken);
        if (patient is null)
        {
            return NotFound();
        }

        Patient = patient;
        Age = patient.AgeOn(_clock.Now);
        Appointments = await _appointments.ListAsync(new AppointmentFilter { PatientId = id, Limit = 500 }, cancellationToken);
        Prescriptions = await _prescriptions.ListByPatientAsync(id, cancellationToken);
        LabReports = await _labReports.ListByPatientAsync(id, cancellationToken);
        Invoices = await _invoices.ListAsync(patientId: id, limit: 500, cancellationToken: cancellationToken);
        return Page();
    }
}
