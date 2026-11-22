namespace PaymentIntegration.Application.DTOs;

public class TransactionDto
{
    public long Id { get; set; }

    public int CustomerId { get; set; }

    public int ProductId { get; set; }

    public string InvoiceNo { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? PaymentChannel { get; set; }

    public string? MaskedCardNo { get; set; }

    public string? ProviderRef { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? PaidAt { get; set; }
}
