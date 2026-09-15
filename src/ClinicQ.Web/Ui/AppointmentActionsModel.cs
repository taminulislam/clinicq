using ClinicQ.Domain.Appointments;

namespace ClinicQ.Web.Ui;

/// <summary>View model for the Shared/_AppointmentActions partial.</summary>
public sealed record AppointmentActionsModel(int Id, AppointmentStatus Status, string ReturnUrl, bool ShowCancel = false);
