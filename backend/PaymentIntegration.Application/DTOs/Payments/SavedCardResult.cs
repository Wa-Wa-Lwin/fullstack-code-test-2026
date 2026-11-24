namespace PaymentIntegration.Application.DTOs.Payments;

public class SavedCardResult
{
    public PaymentResult Payment { get; set; } = new();

    /// <summary>The provider's token for this payment, stored in transactions.payment_token.</summary>
    public string PaymentToken { get; set; } = string.Empty;

    /// <summary>
    /// Set when the card issuer requires the customer to authenticate (3-D Secure) before the
    /// charge completes. The payment stays Pending until the provider's notification arrives.
    /// </summary>
    public string? RedirectUrl { get; set; }
}
