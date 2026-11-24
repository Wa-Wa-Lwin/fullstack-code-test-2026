namespace PaymentIntegration.Application.Exceptions;

/// <summary>
/// A request clashes with the current state, for example a checkout with the same
/// Idempotency-Key is still being processed. The API maps it to 409 and sends
/// <see cref="RetryAfter"/> as a Retry-After header when it is set.
/// </summary>
public sealed class ConflictException : Exception
{
    public ConflictException(string message, TimeSpan? retryAfter = null)
        : base(message)
    {
        RetryAfter = retryAfter;
    }

    public TimeSpan? RetryAfter { get; }
}
