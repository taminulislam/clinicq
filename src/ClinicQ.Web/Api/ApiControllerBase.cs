using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicQ.Web.Api;

/// <summary>
/// Base for all versioned REST controllers: JWT bearer auth, JSON, ProblemDetails error mapping.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Produces("application/json")]
[ServiceFilter(typeof(ApiExceptionFilter))]
public abstract class ApiControllerBase : ControllerBase
{
    protected string CurrentUserName => User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "system";
}
