using ClinicQ.Domain.Entities;
using ClinicQ.Domain.Exceptions;
using ClinicQ.Web.Api.Contracts;
using ClinicQ.Web.Data.Repositories;
using ClinicQ.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace ClinicQ.Web.Api.V1;

public sealed class PatientsController : ApiControllerBase
{
    private readonly IPatientRepository _patients;
    private readonly ILabReportRepository _labReports;
    private readonly IPrescriptionRepository _prescriptions;
    private readonly IClock _clock;

    public PatientsController(IPatientRepository patients, ILabReportRepository labReports, IPrescriptionRepository prescriptions, IClock clock)
    {
        _patients = patients;
        _labReports = labReports;
        _prescriptions = prescriptions;
        _clock = clock;
    }

    /// <summary>Search patients by name, MRN or email.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PatientResponse>>> Search([FromQuery] string? search, [FromQuery] int offset = 0, [FromQuery] int limit = 50, CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 200);
        var rows = await _patients.SearchAsync(search, Math.Max(0, offset), limit, cancellationToken);
        Response.Headers["X-Total-Count"] = (await _patients.CountAsync(search, cancellationToken)).ToString();
        return Ok(rows.Select(PatientResponse.From).ToList());
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PatientResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var patient = await _patients.GetByIdAsync(id, cancellationToken) ?? throw new EntityNotFoundException("Patient", id);
        return Ok(PatientResponse.From(patient));
    }

    [HttpPost]
    [ProducesResponseType(typeof(PatientResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<PatientResponse>> Create([FromBody] CreatePatientRequest request, CancellationToken cancellationToken)
    {
        var patient = new Patient { Mrn = await _patients.NextMrnAsync(cancellationToken), CreatedAt = _clock.Now };
        request.ApplyTo(patient);
        await _patients.CreateAsync(patient, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = patient.Id, version = "1" }, PatientResponse.From(patient));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<PatientResponse>> Update(int id, [FromBody] UpdatePatientRequest request, CancellationToken cancellationToken)
    {
        var patient = await _patients.GetByIdAsync(id, cancellationToken) ?? throw new EntityNotFoundException("Patient", id);
        request.ApplyTo(patient);
        await _patients.UpdateAsync(patient, cancellationToken);
        return Ok(PatientResponse.From(patient));
    }

    [HttpGet("{id:int}/lab-reports")]
    public async Task<ActionResult<IReadOnlyList<LabReportResponse>>> LabReports(int id, CancellationToken cancellationToken)
    {
        _ = await _patients.GetByIdAsync(id, cancellationToken) ?? throw new EntityNotFoundException("Patient", id);
        return Ok((await _labReports.ListByPatientAsync(id, cancellationToken)).Select(LabReportResponse.From).ToList());
    }

    [HttpGet("{id:int}/prescriptions")]
    public async Task<ActionResult<IReadOnlyList<PrescriptionResponse>>> Prescriptions(int id, CancellationToken cancellationToken)
    {
        _ = await _patients.GetByIdAsync(id, cancellationToken) ?? throw new EntityNotFoundException("Patient", id);
        return Ok((await _prescriptions.ListByPatientAsync(id, cancellationToken)).Select(PrescriptionResponse.From).ToList());
    }
}
