namespace PaymentIntegration.Application.Exceptions;

/// <summary>
/// The payment provider rejected a request, could not be reached, or sent a response that
/// could not be trusted. <see cref="ProviderCode"/> and <see cref="ProviderDescription"/>
/// hold the provider's own response code and description when it sent them. The API maps
/// it to 502.
/// </summary>
public sealed class PaymentGatewayException : Exception
{
    public PaymentGatewayException(
        string message,
        string? providerCode = null,
        Exception? innerException = null,
        string? providerDescription = null)
        : base(message, innerException)
    {
        ProviderCode = providerCode;
        ProviderDescription = providerDescription;
    }

    public string? ProviderCode { get; }

    public string? ProviderDescription { get; }
}
