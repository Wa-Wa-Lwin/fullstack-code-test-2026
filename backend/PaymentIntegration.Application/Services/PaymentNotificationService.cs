using Microsoft.Extensions.Logging;
using PaymentIntegration.Application.Exceptions;
using PaymentIntegration.Application.Interfaces;

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
    /// notification is harmless: a final transaction never changes. A verified notification
    /// for an unknown invoice is logged and ignored.
    /// </summary>
    public async Task HandleAsync(string? payload, CancellationToken ct)
    {
        // Signature first. Nothing below runs for a forged or unreadable payload.
        var result = await gateway.ParseNotificationAsync(payload ?? string.Empty, ct);

        var transaction = await transactions.GetByInvoiceNoAsync(result.InvoiceNo!, ct);
        if (transaction is null)
        {
            // Signed with the merchant key, so it is genuine, but no transaction has this
            // invoice (for example, the database was reset while a payment page was open).
            // Sending it again cannot make the transaction appear, so it is acknowledged rather
            // than rejected: an error response would only make the provider retry.
            logger.LogWarning("Ignoring a payment notification for unknown invoice {InvoiceNo}.", result.InvoiceNo);
            return;
        }

        await results.ApplyAsync(transaction, result, ct);
    }
}
