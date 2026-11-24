namespace PaymentIntegration.Application.DTOs.Payments;

public class HostedPaymentRequest
{
    public string InvoiceNo { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Pays by card only and asks the provider to store the card, so later payments can be
    /// charged with the returned customer token. Set it for recurring products.
    /// </summary>
    public bool SaveCard { get; set; }
}
