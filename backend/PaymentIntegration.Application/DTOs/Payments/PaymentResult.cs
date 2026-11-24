namespace PaymentIntegration.Application.DTOs.Payments;

public class PaymentResult
{
    public PaymentOutcome Outcome { get; set; }

    public string? RawCode { get; set; }

    public string? Description { get; set; }

    public string? PaymentChannel { get; set; }

    public string? MaskedAccountNumber { get; set; }

    public string? ProviderReference { get; set; }

    public string? CustomerToken { get; set; }

    public decimal? Amount { get; set; }

    public string? Currency { get; set; }
}
