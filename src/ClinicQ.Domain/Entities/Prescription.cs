namespace ClinicQ.Domain.Entities;

public class Prescription
{
    public int Id { get; set; }
    public int AppointmentId { get; set; }
    public int PatientId { get; set; }
    public int DoctorId { get; set; }
    public DateTime IssuedAt { get; set; }
    public string? Diagnosis { get; set; }
    public string? Instructions { get; set; }
    public List<PrescriptionItem> Items { get; set; } = new();
}

public class PrescriptionItem
{
    public int Id { get; set; }
    public int PrescriptionId { get; set; }
    public string Medication { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public int DurationDays { get; set; }
    public string? Notes { get; set; }
}
