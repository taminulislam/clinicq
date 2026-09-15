using ClinicQ.Domain.Exceptions;
using ClinicQ.Web.Api.Contracts;
using ClinicQ.Web.Data.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace ClinicQ.Web.Api.V1;

public sealed class DoctorsController : ApiControllerBase
{
    private readonly IDoctorRepository _doctors;

    public DoctorsController(IDoctorRepository doctors)
    {
        _doctors = doctors;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DoctorResponse>>> List([FromQuery] int? branchId, CancellationToken cancellationToken)
        => Ok((await _doctors.GetAllAsync(branchId, cancellationToken)).Select(DoctorResponse.From).ToList());

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DoctorResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var doctor = await _doctors.GetByIdAsync(id, cancellationToken) ?? throw new EntityNotFoundException("Doctor", id);
        return Ok(DoctorResponse.From(doctor));
    }
}
