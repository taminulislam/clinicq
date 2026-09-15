using ClinicQ.Domain.Appointments;
using ClinicQ.Domain.Billing;

namespace ClinicQ.Web.Ui;

/// <summary>Bootstrap badge colours for statuses, shared across Razor Pages.</summary>
public static class Badges
{
    public static string For(AppointmentStatus status) => status switch
    {
        AppointmentStatus.Requested => "text-bg-secondary",
        AppointmentStatus.Confirmed => "text-bg-primary",
        AppointmentStatus.CheckedIn => "text-bg-info",
        AppointmentStatus.InConsultation => "text-bg-warning",
        AppointmentStatus.Completed => "text-bg-success",
        AppointmentStatus.Cancelled => "text-bg-dark",
        AppointmentStatus.NoShow => "text-bg-danger",
        _ => "text-bg-light"
    };

    public static string For(InvoiceStatus status) => status switch
    {
        InvoiceStatus.Paid => "text-bg-success",
        InvoiceStatus.PartiallyPaid => "text-bg-warning",
        InvoiceStatus.Issued => "text-bg-primary",
        InvoiceStatus.Void => "text-bg-dark",
        _ => "text-bg-secondary"
    };

    /// <summary>Splits PascalCase status names for display, e.g. InConsultation -> "In consultation".</summary>
    public static string Label(Enum value)
    {
        var text = value.ToString();
        var chars = new List<char>(text.Length + 4);
        for (var i = 0; i < text.Length; i++)
        {
            if (i > 0 && char.IsUpper(text[i]))
            {
                chars.Add(' ');
                chars.Add(char.ToLowerInvariant(text[i]));
            }
            else
            {
                chars.Add(text[i]);
            }
        }

        return new string(chars.ToArray());
    }
}
