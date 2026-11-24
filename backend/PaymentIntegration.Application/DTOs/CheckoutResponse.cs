namespace PaymentIntegration.Application.DTOs;

public class CheckoutResponse
{
    /// <example>42</example>
    public long TransactionId { get; set; }

    /// <example>INV20261124120000AB12CD</example>
    public string InvoiceNo { get; set; } = string.Empty;

    /// <example>9.99</example>
    public decimal Amount { get; set; }

    /// <example>SGD</example>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Pending, Succeeded or Failed.</summary>
    /// <example>Pending</example>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Where to send the customer: the provider's hosted payment page, or the 3-D Secure page for a saved
    /// card. Null when there is nothing to do there (a saved-card payment that has finished,
    /// or a failed checkout).
    /// </summary>
    /// <example>https://sandbox-pgw-ui.2c2p.com/payment/4.1/#/token/kSAops9Zwhos8hSTSeLTUcC</example>
    public string? RedirectUrl { get; set; }
}
