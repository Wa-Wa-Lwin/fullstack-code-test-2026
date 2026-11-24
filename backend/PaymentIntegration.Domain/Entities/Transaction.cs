using PaymentIntegration.Domain.Enums;

namespace PaymentIntegration.Domain.Entities;

public class Transaction
{
    public long Id { get; set; }

    public int CustomerId { get; set; }

    public int ProductId { get; set; }

    public int? CustomerCardId { get; set; }

    public string InvoiceNo { get; set; } = string.Empty;

    public string IdempotencyKey { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "SGD";

    public TransactionStatus Status { get; private set; } = TransactionStatus.Pending;

    public string? PaymentToken { get; set; }

    public string? RedirectUrl { get; set; }

    public string? PaymentChannel { get; private set; }

    public string? MaskedCardNo { get; private set; }

    public string? ProviderRef { get; private set; }

    public string? RespCode { get; private set; }

    public string? RespDesc { get; private set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? PaidAt { get; private set; }

    public Customer? Customer { get; set; }

    public Product? Product { get; set; }

    public CustomerCard? CustomerCard { get; set; }

    /// <summary>
    /// Moves a pending transaction to <see cref="TransactionStatus.Succeeded"/>, records the
    /// gateway result and sets <see cref="PaidAt"/>. If the transaction is already final,
    /// nothing changes and the method returns false.
    /// </summary>
    public bool MarkSucceeded(
        string respCode,
        string? respDesc,
        string? providerRef,
        string? paymentChannel,
        string? maskedCardNo)
        => Complete(TransactionStatus.Succeeded, respCode, respDesc, providerRef, paymentChannel, maskedCardNo);

    /// <summary>
    /// Moves a pending transaction to <see cref="TransactionStatus.Failed"/> and records the
    /// gateway result. If the transaction is already final, nothing changes and the method
    /// returns false.
    /// </summary>
    public bool MarkFailed(
        string respCode,
        string? respDesc,
        string? providerRef = null,
        string? paymentChannel = null,
        string? maskedCardNo = null)
        => Complete(TransactionStatus.Failed, respCode, respDesc, providerRef, paymentChannel, maskedCardNo);

    private bool Complete(
        TransactionStatus finalStatus,
        string respCode,
        string? respDesc,
        string? providerRef,
        string? paymentChannel,
        string? maskedCardNo)
    {
        if (Status != TransactionStatus.Pending)
        {
            return false;
        }

        Status = finalStatus;
        RespCode = respCode;
        RespDesc = respDesc;
        ProviderRef = providerRef;
        PaymentChannel = paymentChannel;
        MaskedCardNo = maskedCardNo;

        if (finalStatus == TransactionStatus.Succeeded)
        {
            PaidAt = DateTime.UtcNow;
        }

        return true;
    }
}
