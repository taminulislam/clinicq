using ClinicQ.Domain.Appointments;
using ClinicQ.Web.Api.Contracts;
using ClinicQ.Web.Data.Models;
using ClinicQ.Web.Data.Repositories;
using ClinicQ.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClinicQ.Web.Api.V1;

/// <summary>Appointment booking and lifecycle: Requested -> Confirmed -> CheckedIn -> InConsultation -> Completed.</summary>
public sealed class AppointmentsController : ApiControllerBase
{
    private readonly AppointmentService _service;
    private readonly IAppointmentRepository _appointments;

    public AppointmentsController(AppointmentService service, IAppointmentRepository appointments)
    {
        _service = service;
        _appointments = appointments;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AppointmentListItem>>> List(
        [FromQuery] int? branchId, [FromQuery] int? doctorId, [FromQuery] int? patientId, [FromQuery] AppointmentStatus? status,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int offset = 0, [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var filter = new AppointmentFilter
        {
            BranchId = branchId,
            DoctorId = doctorId,
            PatientId = patientId,
            Status = status,
            From = from,
            To = to,
            Offset = Math.Max(0, offset),
            Limit = Math.Clamp(limit, 1, 500)
        };
        return Ok(await _appointments.ListAsync(filter, cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentResponse>> Get(int id, CancellationToken cancellationToken)
        => Ok(AppointmentResponse.From(await _service.GetRequiredAsync(id, cancellationToken)));

    /// <summary>Request a new appointment. Enforces branch slot rules (working days, boundaries, lead time, capacity).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AppointmentResponse>> Create([FromBody] CreateAppointmentRequest request, CancellationToken cancellationToken)
    {
        var appointment = await _service.RequestAsync(
            new AppointmentRequest(request.PatientId, request.DoctorId, request.BranchId, request.Start, request.Reason, request.Notes),
            cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = appointment.Id, version = "1" }, AppointmentResponse.From(appointment));
    }

    [HttpPost("{id:int}/confirm")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AppointmentResponse>> Confirm(int id, CancellationToken cancellationToken)
        => Ok(AppointmentResponse.From(await _service.ConfirmAsync(id, cancellationToken)));

    [HttpPost("{id:int}/check-in")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AppointmentResponse>> CheckIn(int id, CancellationToken cancellationToken)
        => Ok(AppointmentResponse.From(await _service.CheckInAsync(id, cancellationToken)));

    [HttpPost("{id:int}/start")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AppointmentResponse>> Start(int id, CancellationToken cancellationToken)
        => Ok(AppointmentResponse.From(await _service.StartConsultationAsync(id, cancellationToken)));

    [HttpPost("{id:int}/complete")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AppointmentResponse>> Complete(int id, [FromBody] CompleteAppointmentRequest? request, CancellationToken cancellationToken)
        => Ok(AppointmentResponse.From(await _service.CompleteAsync(id, request?.Notes, cancellationToken)));

    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AppointmentResponse>> Cancel(int id, [FromBody] CancelAppointmentRequest request, CancellationToken cancellationToken)
        => Ok(AppointmentResponse.From(await _service.CancelAsync(id, request.Reason, cancellationToken)));

    [HttpPost("{id:int}/no-show")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AppointmentResponse>> NoShow(int id, CancellationToken cancellationToken)
        => Ok(AppointmentResponse.From(await _service.MarkNoShowAsync(id, cancellationToken)));
}
