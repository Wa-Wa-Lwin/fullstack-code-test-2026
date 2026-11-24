namespace PaymentIntegration.Application.DTOs.Payments;

public class HostedPaymentResult
{
    public string PaymentUrl { get; set; } = string.Empty;

    public string? ProviderReference { get; set; }
}
