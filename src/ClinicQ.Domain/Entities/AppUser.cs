namespace ClinicQ.Domain.Entities;

/// <summary>
/// Portal/API user. Passwords are stored as PBKDF2 hashes (see PasswordHasher in the web project).
/// </summary>
public class AppUser
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = Roles.Receptionist;
    public int? BranchId { get; set; }
    public bool IsActive { get; set; } = true;
}
