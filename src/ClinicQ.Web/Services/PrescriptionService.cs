using ClinicQ.Domain.Appointments;
using ClinicQ.Domain.Entities;
using ClinicQ.Domain.Exceptions;
using ClinicQ.Web.Data.Repositories;
using ClinicQ.Web.Infrastructure;
using ClinicQ.Web.Infrastructure.Pdf;

namespace ClinicQ.Web.Services;

public sealed record PrescriptionItemRequest(string Medication, string Dosage, string Frequency, int DurationDays, string? Notes);

public sealed record PrescriptionRequest(int AppointmentId, string? Diagnosis, string? Instructions, IReadOnlyList<PrescriptionItemRequest> Items);

public sealed class PrescriptionService
{
    private readonly IPrescriptionRepository _prescriptions;
    private readonly IAppointmentRepository _appointments;
    private readonly IPatientRepository _patients;
    private readonly IDoctorRepository _doctors;
    private readonly IBranchRepository _branches;
    private readonly IPdfService _pdf;
    private readonly IClock _clock;

    public PrescriptionService(
        IPrescriptionRepository prescriptions,
        IAppointmentRepository appointments,
        IPatientRepository patients,
        IDoctorRepository doctors,
        IBranchRepository branches,
        IPdfService pdf,
        IClock clock)
    {
        _prescriptions = prescriptions;
        _appointments = appointments;
        _patients = patients;
        _doctors = doctors;
        _branches = branches;
        _pdf = pdf;
        _clock = clock;
    }

    public async Task<Prescription> CreateAsync(PrescriptionRequest request, CancellationToken cancellationToken = default)
    {
        var appointment = await _appointments.GetByIdAsync(request.AppointmentId, cancellationToken)
                          ?? throw new EntityNotFoundException("Appointment", request.AppointmentId);

        if (appointment.Status is not (AppointmentStatus.InConsultation or AppointmentStatus.Completed))
        {
            throw new DomainException("Prescriptions can only be issued during or after a consultation.");
        }

        if (request.Items.Count == 0)
        {
            throw new DomainException("A prescription needs at least one medication.");
        }

        var prescription = new Prescription
        {
            AppointmentId = appointment.Id,
            PatientId = appointment.PatientId,
            DoctorId = appointment.DoctorId,
            IssuedAt = _clock.Now,
            Diagnosis = request.Diagnosis?.Trim(),
            Instructions = request.Instructions?.Trim(),
            Items = request.Items.Select(i => new PrescriptionItem
            {
                Medication = i.Medication.Trim(),
                Dosage = i.Dosage.Trim(),
                Frequency = i.Frequency.Trim(),
                DurationDays = i.DurationDays,
                Notes = i.Notes?.Trim()
            }).ToList()
        };

        await _prescriptions.CreateAsync(prescription, cancellationToken);
        return prescription;
    }

    public async Task<Prescription> GetRequiredAsync(int id, CancellationToken cancellationToken = default)
        => await _prescriptions.GetByIdAsync(id, cancellationToken) ?? throw new EntityNotFoundException("Prescription", id);

    public async Task<byte[]> RenderPdfAsync(int id, CancellationToken cancellationToken = default)
    {
        var prescription = await GetRequiredAsync(id, cancellationToken);
        var patient = await _patients.GetByIdAsync(prescription.PatientId, cancellationToken) ?? throw new EntityNotFoundException("Patient", prescription.PatientId);
        var doctor = await _doctors.GetByIdAsync(prescription.DoctorId, cancellationToken) ?? throw new EntityNotFoundException("Doctor", prescription.DoctorId);
        var branch = await _branches.GetByIdAsync(doctor.BranchId, cancellationToken) ?? throw new EntityNotFoundException("Branch", doctor.BranchId);

        return _pdf.RenderPrescription(new PrescriptionPdfModel(prescription, patient, doctor, branch));
    }
}
