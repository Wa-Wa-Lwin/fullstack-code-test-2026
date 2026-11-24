namespace PaymentIntegration.Application.Exceptions;

/// <summary>
/// A well-formed request that breaks a business rule, for example a saved card that belongs
/// to another customer, or an Idempotency-Key reused for a different purchase. The API maps
/// it to 422.
/// </summary>
public sealed class BusinessRuleException : Exception
{
    public BusinessRuleException(string message)
        : base(message)
    {
    }
}
