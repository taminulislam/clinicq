using ClinicQ.Domain.Appointments;
using ClinicQ.Domain.Exceptions;
using ClinicQ.Web.Services;
using ClinicQ.Web.Ui;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClinicQ.Web.Pages.Appointments;

public sealed class EditModel : PageModel
{
    private readonly AppointmentService _service;

    public EditModel(AppointmentService service)
    {
        _service = service;
    }

    [BindProperty]
    public EditAppointmentInput Input { get; set; } = new();

    public Appointment Appointment { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        var appointment = await LoadAsync(id, cancellationToken);
        if (appointment is null)
        {
            return NotFound();
        }

        if (AppointmentStateMachine.IsTerminal(appointment.Status))
        {
            this.FlashError($"A {Badges.Label(appointment.Status)} appointment can no longer be edited.");
            return RedirectToPage("Details", new { id });
        }

        Input = new EditAppointmentInput { Reason = appointment.Reason, Notes = appointment.Notes };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, CancellationToken cancellationToken)
    {
        if (await LoadAsync(id, cancellationToken) is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            await _service.UpdateDetailsAsync(id, Input.Reason, Input.Notes, cancellationToken);
            this.Flash("Appointment updated.");
            return RedirectToPage("Details", new { id });
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return Page();
        }
    }

    private async Task<Appointment?> LoadAsync(int id, CancellationToken cancellationToken)
    {
        try
        {
            Appointment = await _service.GetRequiredAsync(id, cancellationToken);
            return Appointment;
        }
        catch (EntityNotFoundException)
        {
            return null;
        }
    }
}

public sealed class EditAppointmentInput
{
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public sealed class EditAppointmentInputValidator : AbstractValidator<EditAppointmentInput>
{
    public EditAppointmentInputValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}
