namespace PaymentIntegration.Application.DTOs;

public class CheckoutResponse
{
    public long TransactionId { get; set; }

    public string InvoiceNo { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? RedirectUrl { get; set; }
}
