namespace PaymentIntegration.Application.DTOs.Payments;

public class PaymentResult
{
    public PaymentOutcome Outcome { get; set; }

    /// <summary>The invoice the result belongs to. A notification is matched to its transaction by it.</summary>
    public string? InvoiceNo { get; set; }

    /// <summary>The provider's response code, stored as is. Only the gateway interprets it.</summary>
    public string? RawCode { get; set; }

    public string? Description { get; set; }

    public string? PaymentChannel { get; set; }

    public string? MaskedAccountNumber { get; set; }

    public string? ProviderReference { get; set; }

    public string? CustomerToken { get; set; }

    public decimal? Amount { get; set; }

    public string? Currency { get; set; }
}
