namespace ClinicQ.Domain.Entities;

public class Patient
{
    public int Id { get; set; }
    /// <summary>Medical record number, unique across all branches.</summary>
    public string Mrn { get; set; } = string.Empty;
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
    public DateTime CreatedAt { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();

    public int AgeOn(DateTime date)
    {
        var age = date.Year - DateOfBirth.Year;
        if (DateOfBirth.Date > date.AddYears(-age))
        {
            age--;
        }

        return age;
    }
}
