using ClinicQ.Domain.Entities;

namespace ClinicQ.Web.Api.Contracts;

public class PatientRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? AddressLine { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Allergies { get; set; }

    public void ApplyTo(Patient patient)
    {
        patient.FirstName = FirstName.Trim();
        patient.LastName = LastName.Trim();
        patient.DateOfBirth = DateOfBirth.Date;
        patient.Gender = Gender.Trim();
        patient.Email = Email.Trim();
        patient.Phone = Phone.Trim();
        patient.AddressLine = AddressLine?.Trim();
        patient.City = City?.Trim();
        patient.State = State?.Trim();
        patient.PostalCode = PostalCode?.Trim();
        patient.Allergies = Allergies?.Trim();
    }
}

public sealed class CreatePatientRequest : PatientRequest
{
}

public sealed class UpdatePatientRequest : PatientRequest
{
}

public sealed record PatientResponse(
    int Id,
    string Mrn,
    string FirstName,
    string LastName,
    string FullName,
    DateTime DateOfBirth,
    string Gender,
    string Email,
    string Phone,
    string? AddressLine,
    string? City,
    string? State,
    string? PostalCode,
    string? Allergies,
    DateTime CreatedAt)
{
    public static PatientResponse From(Patient p) => new(
        p.Id, p.Mrn, p.FirstName, p.LastName, p.FullName, p.DateOfBirth, p.Gender, p.Email, p.Phone,
        p.AddressLine, p.City, p.State, p.PostalCode, p.Allergies, p.CreatedAt);
}
