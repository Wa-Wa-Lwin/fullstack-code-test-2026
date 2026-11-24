namespace PaymentIntegration.Application.DTOs.Payments;

public class HostedPaymentResult
{
    /// <summary>The hosted payment page to redirect the customer to.</summary>
    public string PaymentUrl { get; set; } = string.Empty;

    /// <summary>The provider's token for this payment, stored in transactions.payment_token.</summary>
    public string PaymentToken { get; set; } = string.Empty;
}
