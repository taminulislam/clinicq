using ClinicQ.Domain.Entities;

namespace ClinicQ.Web.Api.Contracts;

public sealed class PrescriptionItemDto
{
    public string Medication { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public int DurationDays { get; set; }
    public string? Notes { get; set; }
}

public sealed class CreatePrescriptionRequest
{
    public int AppointmentId { get; set; }
    public string? Diagnosis { get; set; }
    public string? Instructions { get; set; }
    public List<PrescriptionItemDto> Items { get; set; } = new();
}

public sealed record PrescriptionResponse(
    int Id,
    int AppointmentId,
    int PatientId,
    int DoctorId,
    DateTime IssuedAt,
    string? Diagnosis,
    string? Instructions,
    IReadOnlyList<PrescriptionItemDto> Items)
{
    public static PrescriptionResponse From(Prescription p) => new(
        p.Id, p.AppointmentId, p.PatientId, p.DoctorId, p.IssuedAt, p.Diagnosis, p.Instructions,
        p.Items.Select(i => new PrescriptionItemDto
        {
            Medication = i.Medication,
            Dosage = i.Dosage,
            Frequency = i.Frequency,
            DurationDays = i.DurationDays,
            Notes = i.Notes
        }).ToList());
}
