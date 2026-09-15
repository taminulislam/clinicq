using System.Security.Claims;
using ClinicQ.Domain.Billing;
using ClinicQ.Domain.Entities;
using ClinicQ.Domain.Exceptions;
using ClinicQ.Web.Api.Contracts;
using ClinicQ.Web.Data.Repositories;
using ClinicQ.Web.Infrastructure.Pdf;
using ClinicQ.Web.Services;
using ClinicQ.Web.Ui;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClinicQ.Web.Pages.Invoices;

public sealed class DetailsModel : PageModel
{
    private readonly BillingService _billing;
    private readonly IInvoiceRepository _invoices;
    private readonly IPatientRepository _patients;
    private readonly IBranchRepository _branches;
    private readonly IPdfService _pdf;

    public DetailsModel(BillingService billing, IInvoiceRepository invoices, IPatientRepository patients, IBranchRepository branches, IPdfService pdf)
    {
        _billing = billing;
        _invoices = invoices;
        _patients = patients;
        _branches = branches;
        _pdf = pdf;
    }

    public Invoice Invoice { get; private set; } = new();
    public Patient Patient { get; private set; } = new();
    public Branch Branch { get; private set; } = new();
    public IReadOnlyList<Payment> Payments { get; private set; } = Array.Empty<Payment>();

    /// <summary>Same DTO and FluentValidation rules as POST /api/v1/invoices/{id}/payments.</summary>
    [BindProperty]
    public RecordPaymentRequest Payment { get; set; } = new();

    public bool CanTakePayment => Invoice.Status is InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid;
    public bool CanVoid => Invoice.AmountPaid == 0 && Invoice.Status != InvoiceStatus.Void;

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(id, cancellationToken))
        {
            return NotFound();
        }

        Payment = new RecordPaymentRequest { Amount = Invoice.Balance, Method = nameof(PaymentMethod.Card) };
        return Page();
    }

    public async Task<IActionResult> OnPostPaymentAsync(int id, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(id, cancellationToken))
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var method = Enum.Parse<PaymentMethod>(Payment.Method, ignoreCase: true);
        var receivedBy = User.FindFirstValue(ClaimTypes.Name) ?? "portal";
        await this.TryDomainAsync(
            () => _billing.RecordPaymentAsync(id, Payment.Amount, method, Payment.Reference, receivedBy, cancellationToken),
            $"Payment of {Payment.Amount:C2} recorded.");
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostVoidAsync(int id, CancellationToken cancellationToken)
    {
        await this.TryDomainAsync(() => _billing.VoidAsync(id, cancellationToken), "Invoice voided.");
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnGetPdfAsync(int id, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(id, cancellationToken))
        {
            return NotFound();
        }

        var bytes = _pdf.RenderInvoice(new InvoicePdfModel(Invoice, Patient, Branch, Payments));
        return File(bytes, "application/pdf", $"{Invoice.InvoiceNumber}.pdf");
    }

    private async Task<bool> LoadAsync(int id, CancellationToken cancellationToken)
    {
        try
        {
            Invoice = await _billing.GetRequiredAsync(id, cancellationToken);
        }
        catch (EntityNotFoundException)
        {
            return false;
        }

        Patient = await _patients.GetByIdAsync(Invoice.PatientId, cancellationToken) ?? new Patient();
        Branch = await _branches.GetByIdAsync(Invoice.BranchId, cancellationToken) ?? new Branch();
        Payments = await _invoices.GetPaymentsAsync(id, cancellationToken);
        return true;
    }
}
