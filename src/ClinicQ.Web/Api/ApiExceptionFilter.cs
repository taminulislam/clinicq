using ClinicQ.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ClinicQ.Web.Api;

/// <summary>
/// Translates domain exceptions into RFC 7807 ProblemDetails responses:
/// not found -> 404, invalid state transition -> 409, other business rule violations -> 400.
/// </summary>
public sealed class ApiExceptionFilter : IExceptionFilter
{
    private readonly ILogger<ApiExceptionFilter> _logger;

    public ApiExceptionFilter(ILogger<ApiExceptionFilter> logger)
    {
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        var (status, title) = context.Exception switch
        {
            EntityNotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            InvalidAppointmentTransitionException => (StatusCodes.Status409Conflict, "Invalid appointment transition"),
            DomainException => (StatusCodes.Status400BadRequest, "Business rule violation"),
            _ => (0, string.Empty)
        };

        if (status == 0)
        {
            return;
        }

        _logger.LogWarning("API request {Path} rejected: {Message}", context.HttpContext.Request.Path, context.Exception.Message);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = context.Exception.Message,
            Instance = context.HttpContext.Request.Path,
            Type = $"https://httpstatuses.io/{status}"
        };
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

        context.Result = new ObjectResult(problem) { StatusCode = status };
        context.ExceptionHandled = true;
    }
}
