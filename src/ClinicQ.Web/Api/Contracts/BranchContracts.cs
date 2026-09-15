using ClinicQ.Domain.Billing;
using ClinicQ.Domain.Entities;
using ClinicQ.Domain.Scheduling;

namespace ClinicQ.Web.Api.Contracts;

public sealed record BranchResponse(int Id, string Code, string Name, string AddressLine, string City, string State, string PostalCode, string Phone, decimal TaxRatePercent, bool IsActive)
{
    public static BranchResponse From(Branch b) => new(b.Id, b.Code, b.Name, b.AddressLine, b.City, b.State, b.PostalCode, b.Phone, b.TaxRatePercent, b.IsActive);
}

public sealed record DoctorResponse(int Id, int BranchId, string FullName, string Specialty, string Email, string LicenseNumber, bool IsActive)
{
    public static DoctorResponse From(Doctor d) => new(d.Id, d.BranchId, d.FullName, d.Specialty, d.Email, d.LicenseNumber, d.IsActive);
}

public sealed record SlotResponse(DateTime Start, DateTime End, int Capacity, int Booked, int Remaining, bool IsAvailable)
{
    public static SlotResponse From(AppointmentSlot s) => new(s.Start, s.End, s.Capacity, s.Booked, s.Remaining, s.IsAvailable);
}

public sealed record FeeScheduleItemResponse(int Id, string ServiceCode, string Description, decimal Amount, bool IsActive, DateTime EffectiveFrom)
{
    public static FeeScheduleItemResponse From(FeeScheduleItem f) => new(f.Id, f.ServiceCode, f.Description, f.Amount, f.IsActive, f.EffectiveFrom);
}

public sealed class UpdateSlotRuleRequest
{
    /// <summary>HH:mm</summary>
    public string OpenTime { get; set; } = "08:00";
    /// <summary>HH:mm</summary>
    public string CloseTime { get; set; } = "17:00";
    public int SlotDurationMinutes { get; set; } = 30;
    public int WorkingDaysMask { get; set; } = WorkingDays.MondayToFriday;
    public int MaxBookingsPerSlot { get; set; } = 1;
    public int MinLeadTimeHours { get; set; } = 1;
    public int MaxAdvanceDays { get; set; } = 60;
}

public sealed record SlotRuleResponse(int BranchId, string OpenTime, string CloseTime, int SlotDurationMinutes, int WorkingDaysMask, string WorkingDays, int MaxBookingsPerSlot, int MinLeadTimeHours, int MaxAdvanceDays)
{
    public static SlotRuleResponse From(BranchSlotRule r) => new(
        r.BranchId, r.OpenTime.ToString("HH:mm"), r.CloseTime.ToString("HH:mm"), r.SlotDurationMinutes, r.WorkingDaysMask,
        Domain.Scheduling.WorkingDays.Describe(r.WorkingDaysMask), r.MaxBookingsPerSlot, r.MinLeadTimeHours, r.MaxAdvanceDays);
}
