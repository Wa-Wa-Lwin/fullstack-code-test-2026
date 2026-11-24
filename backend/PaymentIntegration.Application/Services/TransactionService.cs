using Microsoft.Extensions.Logging;
using PaymentIntegration.Application.DTOs;
using PaymentIntegration.Application.Exceptions;
using PaymentIntegration.Application.Interfaces;
using PaymentIntegration.Domain.Entities;
using PaymentIntegration.Domain.Enums;

namespace PaymentIntegration.Application.Services;

/// <summary>
/// Reads transactions for the result page. Always from the database, never from the cache:
/// a status changes whenever the payment notification arrives.
/// </summary>
public class TransactionService(
    ITransactionRepository transactions,
    IProductRepository products,
    IPaymentGateway gateway,
    PaymentResultService results,
    ILogger<TransactionService> logger)
{
    public async Task<TransactionDto> GetByInvoiceNoAsync(string invoiceNo, CancellationToken ct)
    {
        var transaction = await transactions.GetByInvoiceNoAsync(invoiceNo, ct)
            ?? throw new NotFoundException(nameof(Transaction), invoiceNo);

        var product = await products.GetByIdAsync(transaction.ProductId, ct);

        return new TransactionDto
        {
            Id = transaction.Id,
            CustomerId = transaction.CustomerId,
            ProductId = transaction.ProductId,
            ProductName = product?.Name ?? string.Empty,
            InvoiceNo = transaction.InvoiceNo,
            Amount = transaction.Amount,
            Currency = transaction.Currency,
            Status = transaction.Status.ToString(),
            PaymentChannel = transaction.PaymentChannel,
            MaskedCardNo = transaction.MaskedCardNo,
            RespDesc = transaction.RespDesc,
            CreatedAt = transaction.CreatedAt,
            PaidAt = transaction.PaidAt,
        };
    }

    /// <summary>
    /// Runs a Payment Inquiry for a transaction that is still Pending and applies the answer.
    /// The browser return calls it: after 3-D Secure on a saved card, and as a fallback when
    /// the backend notification has not arrived yet. An unreachable provider leaves the
    /// transaction Pending for the notification to settle.
    /// </summary>
    public async Task RefreshIfPendingAsync(string invoiceNo, CancellationToken ct)
    {
        var transaction = await transactions.GetByInvoiceNoAsync(invoiceNo, ct)
            ?? throw new NotFoundException(nameof(Transaction), invoiceNo);

        if (transaction.Status != TransactionStatus.Pending)
        {
            return;
        }

        try
        {
            var result = await gateway.InquireAsync(invoiceNo, ct);
            await results.ApplyAsync(transaction, result, ct);
        }
        catch (PaymentGatewayException ex)
        {
            logger.LogWarning(ex, "Payment Inquiry for {InvoiceNo} failed; it stays Pending.", invoiceNo);
        }
    }
}
