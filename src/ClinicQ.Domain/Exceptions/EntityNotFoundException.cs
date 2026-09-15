namespace ClinicQ.Domain.Exceptions;

/// <summary>
/// Raised when a referenced aggregate does not exist. Mapped to HTTP 404 by the API layer.
/// </summary>
public sealed class EntityNotFoundException : DomainException
{
    public string EntityName { get; }
    public object Key { get; }

    public EntityNotFoundException(string entityName, object key)
        : base($"{entityName} with key '{key}' was not found.")
    {
        EntityName = entityName;
        Key = key;
    }
}
