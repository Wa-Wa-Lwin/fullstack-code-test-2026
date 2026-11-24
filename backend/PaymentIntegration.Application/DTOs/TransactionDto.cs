namespace PaymentIntegration.Application.DTOs;

/// <summary>A transaction as the result page shows it. Always read from the database.</summary>
public class TransactionDto
{
    /// <example>42</example>
    public long Id { get; set; }

    /// <summary>Lets the failure page's Go back button preselect the customer.</summary>
    /// <example>1</example>
    public int CustomerId { get; set; }

    /// <summary>Lets the failure page's Go back button preselect the product.</summary>
    /// <example>3</example>
    public int ProductId { get; set; }

    /// <example>Cloud Storage Plan</example>
    public string ProductName { get; set; } = string.Empty;

    /// <example>INV20261124120000AB12CD</example>
    public string InvoiceNo { get; set; } = string.Empty;

    /// <example>9.99</example>
    public decimal Amount { get; set; }

    /// <example>SGD</example>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Pending, Succeeded or Failed.</summary>
    /// <example>Succeeded</example>
    public string Status { get; set; } = string.Empty;

    /// <example>CC</example>
    public string? PaymentChannel { get; set; }

    /// <example>411111XXXXXX1111</example>
    public string? MaskedCardNo { get; set; }

    /// <example>Success</example>
    public string? RespDesc { get; set; }

    /// <example>2026-11-24T12:00:00Z</example>
    public DateTime CreatedAt { get; set; }

    /// <summary>Set when the payment succeeded (UTC).</summary>
    /// <example>2026-11-24T12:01:30Z</example>
    public DateTime? PaidAt { get; set; }
}
