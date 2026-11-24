namespace PaymentIntegration.Application.DTOs.Payments;

public class HostedPaymentRequest
{
    public string InvoiceNo { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string ReturnUrl { get; set; } = string.Empty;
}
