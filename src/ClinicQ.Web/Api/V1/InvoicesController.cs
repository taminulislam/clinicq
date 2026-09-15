using ClinicQ.Domain.Billing;
using ClinicQ.Domain.Entities;
using ClinicQ.Domain.Exceptions;
using ClinicQ.Web.Api.Contracts;
using ClinicQ.Web.Data.Models;
using ClinicQ.Web.Data.Repositories;
using ClinicQ.Web.Infrastructure.Pdf;
using ClinicQ.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicQ.Web.Api.V1;

public sealed class InvoicesController : ApiControllerBase
{
    private readonly BillingService _billing;
    private readonly IInvoiceRepository _invoices;
    private readonly IPatientRepository _patients;
    private readonly IBranchRepository _branches;
    private readonly IPdfService _pdf;

    public InvoicesController(BillingService billing, IInvoiceRepository invoices, IPatientRepository patients, IBranchRepository branches, IPdfService pdf)
    {
        _billing = billing;
        _invoices = invoices;
        _patients = patients;
        _branches = branches;
        _pdf = pdf;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InvoiceListItem>>> List([FromQuery] int? branchId, [FromQuery] InvoiceStatus? status, [FromQuery] int? patientId, [FromQuery] int limit = 100, CancellationToken cancellationToken = default)
        => Ok(await _invoices.ListAsync(branchId, status, patientId, Math.Clamp(limit, 1, 500), cancellationToken));

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvoiceResponse>> Get(int id, CancellationToken cancellationToken)
        => Ok(InvoiceResponse.From(await _billing.GetRequiredAsync(id, cancellationToken)));

    /// <summary>Generate an invoice for a completed appointment from the branch fee schedule.</summary>
    [HttpPost]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Billing},{Roles.Receptionist}")]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InvoiceResponse>> Generate([FromBody] GenerateInvoiceRequest request, CancellationToken cancellationToken)
    {
        var services = request.Services.Select(s => new ServiceLineRequest(s.ServiceCode, s.Quantity)).ToList();
        var invoice = await _billing.GenerateInvoiceAsync(request.AppointmentId, services, request.DiscountPercent, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = invoice.Id, version = "1" }, InvoiceResponse.From(invoice));
    }

    [HttpPost("{id:int}/payments")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Billing},{Roles.Receptionist}")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InvoiceResponse>> RecordPayment(int id, [FromBody] RecordPaymentRequest request, CancellationToken cancellationToken)
    {
        var method = Enum.Parse<PaymentMethod>(request.Method, true);
        var invoice = await _billing.RecordPaymentAsync(id, request.Amount, method, request.Reference, CurrentUserName, cancellationToken);
        return Ok(InvoiceResponse.From(invoice));
    }

    [HttpGet("{id:int}/payments")]
    public async Task<ActionResult<IReadOnlyList<PaymentResponse>>> Payments(int id, CancellationToken cancellationToken)
    {
        _ = await _billing.GetRequiredAsync(id, cancellationToken);
        return Ok((await _invoices.GetPaymentsAsync(id, cancellationToken)).Select(PaymentResponse.From).ToList());
    }

    [HttpPost("{id:int}/void")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Billing}")]
    public async Task<ActionResult<InvoiceResponse>> Void(int id, CancellationToken cancellationToken)
        => Ok(InvoiceResponse.From(await _billing.VoidAsync(id, cancellationToken)));

    /// <summary>Invoice as a PDF document (QuestPDF).</summary>
    [HttpGet("{id:int}/pdf")]
    [Produces("application/pdf")]
    public async Task<IActionResult> Pdf(int id, CancellationToken cancellationToken)
    {
        var invoice = await _billing.GetRequiredAsync(id, cancellationToken);
        var patient = await _patients.GetByIdAsync(invoice.PatientId, cancellationToken) ?? throw new EntityNotFoundException("Patient", invoice.PatientId);
        var branch = await _branches.GetByIdAsync(invoice.BranchId, cancellationToken) ?? throw new EntityNotFoundException("Branch", invoice.BranchId);
        var payments = await _invoices.GetPaymentsAsync(id, cancellationToken);

        var bytes = _pdf.RenderInvoice(new InvoicePdfModel(invoice, patient, branch, payments));
        return File(bytes, "application/pdf", $"{invoice.InvoiceNumber}.pdf");
    }
}
