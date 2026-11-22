using PaymentIntegration.Domain.Enums;

namespace PaymentIntegration.Domain.Entities;

public class Transaction
{
    public long Id { get; set; }

    public int CustomerId { get; set; }

    public int ProductId { get; set; }

    public string InvoiceNo { get; set; } = string.Empty;

    public string IdempotencyKey { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "SGD";

    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;

    public string? PaymentChannel { get; set; }

    public string? MaskedCardNo { get; set; }

    public string? ProviderRef { get; set; }

    public string? RespCode { get; set; }

    public string? RespDesc { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? PaidAt { get; set; }

    public Customer? Customer { get; set; }

    public Product? Product { get; set; }
}
