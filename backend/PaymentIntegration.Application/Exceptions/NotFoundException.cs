namespace PaymentIntegration.Application.Exceptions;

/// <summary>A requested record does not exist. The API maps it to 404.</summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string message)
        : base(message)
    {
    }

    public NotFoundException(string entityName, object key)
        : base($"{entityName} '{key}' was not found.")
    {
    }
}
