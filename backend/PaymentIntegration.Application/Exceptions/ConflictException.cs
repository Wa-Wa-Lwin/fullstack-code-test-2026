namespace PaymentIntegration.Application.Exceptions;

/// <summary>
/// A request clashes with the current state, for example a checkout with the same
/// Idempotency-Key is still being processed. The API maps it to 409.
/// </summary>
public sealed class ConflictException : Exception
{
    public ConflictException(string message)
        : base(message)
    {
    }
}
