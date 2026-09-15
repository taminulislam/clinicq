namespace ClinicQ.Domain.Exceptions;

/// <summary>
/// Base class for business-rule violations. The API layer maps these to HTTP 400/409 responses.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }

    public DomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
