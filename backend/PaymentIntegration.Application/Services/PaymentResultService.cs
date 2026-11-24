using Microsoft.Extensions.Logging;
using PaymentIntegration.Application.DTOs.Payments;
using PaymentIntegration.Application.Interfaces;
using PaymentIntegration.Domain.Entities;
using PaymentIntegration.Domain.Enums;

namespace PaymentIntegration.Application.Services;

/// <summary>
/// Applies a payment result to its transaction. The backend notification, the Payment
/// Inquiry after a saved-card charge and the browser return all go through here, so the
/// rules are written once.
/// </summary>
public class PaymentResultService(
    ITransactionRepository transactions,
    CustomerService customers,
    ILogger<PaymentResultService> logger)
{
    /// <summary>
    /// Returns true if this call moved the transaction out of Pending. A Pending result,
    /// a mismatched amount or an already final transaction changes nothing.
    /// </summary>
    public async Task<bool> ApplyAsync(
        Transaction transaction,
        PaymentResult result,
        CancellationToken ct)
    {
        if (result.Outcome == PaymentOutcome.Pending)
        {
            logger.LogInformation(
                "Payment result for {InvoiceNo} is still pending ({RawCode}).",
                transaction.InvoiceNo, result.RawCode);
            return false;
        }

        if (!AmountMatches(transaction, result))
        {
            // Never mark a payment Succeeded for an amount other than the one charged for.
            logger.LogWarning(
                "Ignoring payment result for {InvoiceNo}: it reports {ResultAmount} {ResultCurrency}, the transaction is {Amount} {Currency}.",
                transaction.InvoiceNo, result.Amount, result.Currency, transaction.Amount, transaction.Currency);
            return false;
        }

        var code = result.RawCode ?? "UNKNOWN";
        var marked = result.Outcome == PaymentOutcome.Succeeded
            ? transaction.MarkSucceeded(code, result.Description, result.ProviderReference, result.PaymentChannel, result.MaskedAccountNumber)
            : transaction.MarkFailed(code, result.Description, result.ProviderReference, result.PaymentChannel, result.MaskedAccountNumber);

        // MarkSucceeded/MarkFailed only checked this in-memory copy. The conditional
        // update re-checks Pending in the database, so a concurrent handler cannot also win.
        var updated = marked && await transactions.UpdateIfPendingAsync(transaction, ct);

        logger.LogInformation(
            "Payment result {Outcome} ({RawCode}) for {InvoiceNo}: {Effect}.",
            result.Outcome, result.RawCode, transaction.InvoiceNo,
            updated ? "transaction updated" : "already final, nothing changed");

        // Saved even when this update changed nothing, so a repeated notification still saves
        // a card whose first save failed. But only for a transaction that really is Succeeded:
        // if a failure result finalised it first, this success result saves no card.
        if (result.Outcome == PaymentOutcome.Succeeded
            && result.CustomerToken is not null
            && (updated || await IsStoredAsSucceededAsync(transaction.Id, ct)))
        {
            await customers.SaveCardAsync(
                new CustomerCard
                {
                    CustomerId = transaction.CustomerId,
                    CustomerToken = result.CustomerToken,
                    MaskedCardNo = result.MaskedAccountNumber ?? string.Empty,
                },
                ct);
        }

        return updated;
    }

    private async Task<bool> IsStoredAsSucceededAsync(long transactionId, CancellationToken ct)
        => (await transactions.GetByIdAsync(transactionId, ct))?.Status == TransactionStatus.Succeeded;

    private static bool AmountMatches(Transaction transaction, PaymentResult result)
    {
        // A success must state the amount it was for. A failure without one (a declined
        // Do Payment) is accepted: marking a payment Failed cannot over-credit anyone.
        if (result.Amount is null || result.Currency is null)
        {
            return result.Outcome != PaymentOutcome.Succeeded;
        }

        return result.Amount.Value == transaction.Amount
            && string.Equals(result.Currency, transaction.Currency, StringComparison.OrdinalIgnoreCase);
    }
}
