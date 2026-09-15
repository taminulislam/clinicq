using ClinicQ.Domain.Entities;
using ClinicQ.Web.Api.Contracts;
using ClinicQ.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicQ.Web.Api.V1;

public sealed class PrescriptionsController : ApiControllerBase
{
    private readonly PrescriptionService _service;

    public PrescriptionsController(PrescriptionService service)
    {
        _service = service;
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PrescriptionResponse>> Get(int id, CancellationToken cancellationToken)
        => Ok(PrescriptionResponse.From(await _service.GetRequiredAsync(id, cancellationToken)));

    [HttpPost]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Doctor}")]
    [ProducesResponseType(typeof(PrescriptionResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<PrescriptionResponse>> Create([FromBody] CreatePrescriptionRequest request, CancellationToken cancellationToken)
    {
        var items = request.Items.Select(i => new PrescriptionItemRequest(i.Medication, i.Dosage, i.Frequency, i.DurationDays, i.Notes)).ToList();
        var prescription = await _service.CreateAsync(new PrescriptionRequest(request.AppointmentId, request.Diagnosis, request.Instructions, items), cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = prescription.Id, version = "1" }, PrescriptionResponse.From(prescription));
    }

    /// <summary>Prescription as a PDF document (QuestPDF).</summary>
    [HttpGet("{id:int}/pdf")]
    [Produces("application/pdf")]
    public async Task<IActionResult> Pdf(int id, CancellationToken cancellationToken)
    {
        var bytes = await _service.RenderPdfAsync(id, cancellationToken);
        return File(bytes, "application/pdf", $"prescription-{id}.pdf");
    }
}
