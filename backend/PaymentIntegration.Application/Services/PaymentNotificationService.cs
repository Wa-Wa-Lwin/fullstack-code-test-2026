using Microsoft.Extensions.Logging;
using PaymentIntegration.Application.Exceptions;
using PaymentIntegration.Application.Interfaces;
using PaymentIntegration.Domain.Entities;

namespace PaymentIntegration.Application.Services;

/// <summary>Handles the provider's server-to-server payment notification.</summary>
public class PaymentNotificationService(
    IPaymentGateway gateway,
    ITransactionRepository transactions,
    PaymentResultService results,
    ILogger<PaymentNotificationService> logger)
{
    /// <summary>
    /// Verifies the payload, then applies its result. A notification that fails verification
    /// throws <see cref="ValidationException"/> before the database is touched. A repeated
    /// notification is harmless: a final transaction never changes.
    /// </summary>
    public async Task HandleAsync(string? payload, CancellationToken ct)
    {
        // Signature first. Nothing below runs for a forged or unreadable payload.
        var result = await gateway.ParseNotificationAsync(payload ?? string.Empty, ct);

        var transaction = await transactions.GetByInvoiceNoAsync(result.InvoiceNo!, ct);
        if (transaction is null)
        {
            logger.LogWarning("Payment notification for unknown invoice {InvoiceNo}.", result.InvoiceNo);
            throw new NotFoundException(nameof(Transaction), result.InvoiceNo!);
        }

        await results.ApplyAsync(transaction, result, ct);
    }
}
