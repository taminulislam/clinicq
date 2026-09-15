namespace ClinicQ.Domain.Entities;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Receptionist = "Receptionist";
    public const string Doctor = "Doctor";
    public const string Billing = "Billing";

    public static readonly IReadOnlyList<string> All = new[] { Admin, Receptionist, Doctor, Billing };
}
