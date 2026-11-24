namespace PaymentIntegration.Application.Exceptions;

/// <summary>
/// The payment provider rejected a request, could not be reached, or sent a response that
/// could not be trusted. <see cref="ProviderCode"/> holds the provider's own response code
/// when it sent one. The API maps it to 502.
/// </summary>
public sealed class PaymentGatewayException : Exception
{
    public PaymentGatewayException(string message, string? providerCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        ProviderCode = providerCode;
    }

    public string? ProviderCode { get; }
}
