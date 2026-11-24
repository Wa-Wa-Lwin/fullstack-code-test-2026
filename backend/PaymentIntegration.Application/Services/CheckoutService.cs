using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using PaymentIntegration.Application.DTOs;
using PaymentIntegration.Application.DTOs.Payments;
using PaymentIntegration.Application.Exceptions;
using PaymentIntegration.Application.Interfaces;
using PaymentIntegration.Domain.Entities;
using PaymentIntegration.Domain.Enums;

namespace PaymentIntegration.Application.Services;

/// <summary>
/// Starts a payment. A repeated request with the same Idempotency-Key never creates a
/// second transaction or a second payment gateway call. Three guards back each other up:
/// the stored transaction (replays), a Redis lock (concurrent duplicates), and the unique
/// index on idempotency_key (the last defence if the lock expired or Redis is down).
/// </summary>
public class CheckoutService(
    ICustomerRepository customers,
    IProductRepository products,
    ICustomerCardRepository cards,
    ITransactionRepository transactions,
    IPaymentGateway gateway,
    IDistributedLockService locks,
    PaymentResultService results,
    ILogger<CheckoutService> logger)
{
    public const int MaxIdempotencyKeyLength = 100;

    // Longer than every gateway call made under the lock: a saved card can take up to three
    // provider calls in a row, each with a 15s HttpClient timeout.
    public static readonly TimeSpan LockTtl = TimeSpan.FromSeconds(60);

    public static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(2);

    private const string InvoiceChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public async Task<CheckoutResponse> CheckoutAsync(
        CheckoutRequest request,
        string? idempotencyKey,
        CancellationToken ct)
    {
        var key = ValidateKey(idempotencyKey);

        // 1. Validate the customer, the product and the card.
        var customer = await customers.GetByIdAsync(request.CustomerId, ct)
            ?? throw new NotFoundException(nameof(Customer), request.CustomerId);

        var product = await products.GetByIdAsync(request.ProductId, ct)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        CustomerCard? card = null;
        if (request.CustomerCardId is { } cardId)
        {
            card = await cards.GetByIdAsync(cardId, ct)
                ?? throw new NotFoundException(nameof(CustomerCard), cardId);

            if (card.CustomerId != customer.Id)
            {
                throw new BusinessRuleException($"Card {cardId} does not belong to customer {customer.Id}.");
            }
        }

        // 2. A replay returns the stored result without calling the gateway.
        if (await transactions.GetByIdempotencyKeyAsync(key, ct) is { } replay)
        {
            return Replay(replay, request);
        }

        // 3. Only one request per key does the work at a time.
        var lockKey = $"lock:checkout:{key}";
        var lockValue = Guid.NewGuid().ToString("N");
        var locked = false;
        try
        {
            locked = await locks.AcquireLockAsync(lockKey, lockValue, LockTtl, ct);
            if (!locked)
            {
                throw new ConflictException("A checkout with this Idempotency-Key is in progress.", RetryAfter);
            }
        }
        catch (Exception ex) when (ex is not ConflictException and not OperationCanceledException)
        {
            // Redis is down. Carry on: the unique index on idempotency_key still lets only
            // one request insert, and only the inserting request calls the gateway.
            logger.LogError(ex, "Checkout lock unavailable for key {IdempotencyKey}; continuing without it.", key);
        }

        try
        {
            // 4. The first request may have finished between step 2 and the lock.
            if (await transactions.GetByIdempotencyKeyAsync(key, ct) is { } finished)
            {
                return Replay(finished, request);
            }

            // 5. Insert the Pending transaction before calling the gateway, so a request that loses
            // the insert never calls the gateway.
            var transaction = new Transaction
            {
                CustomerId = customer.Id,
                ProductId = product.Id,
                CustomerCardId = card?.Id,
                InvoiceNo = NewInvoiceNo(),
                IdempotencyKey = key,
                Amount = product.Price,
                Currency = product.Currency,
            };

            if (!await transactions.TryAddAsync(transaction, ct))
            {
                var winner = await transactions.GetByIdempotencyKeyAsync(key, ct)
                    ?? throw new ConflictException("The checkout could not be created; please retry.", RetryAfter);
                return Replay(winner, request);
            }

            // 6. From here on the transaction exists and the gateway may be charging, so finish
            // even if the client disconnects: a cancelled request would leave it Pending.
            if (card is null)
            {
                await StartHostedPaymentAsync(transaction, product, CancellationToken.None);
            }
            else
            {
                await ChargeSavedCardAsync(transaction, product, card, CancellationToken.None);
            }

            var saved = await transactions.GetByIdAsync(transaction.Id, CancellationToken.None) ?? transaction;
            return ToResponse(saved);
        }
        finally
        {
            // 7. Release only our own lock, even if the request was cancelled.
            if (locked)
            {
                try
                {
                    await locks.ReleaseLockAsync(lockKey, lockValue, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not release checkout lock {LockKey}; it expires on its own.", lockKey);
                }
            }
        }
    }

    private async Task StartHostedPaymentAsync(Transaction transaction, Product product, CancellationToken ct)
    {
        HostedPaymentResult hosted;
        try
        {
            hosted = await gateway.CreateHostedPaymentAsync(
                new HostedPaymentRequest
                {
                    InvoiceNo = transaction.InvoiceNo,
                    Amount = transaction.Amount,
                    Currency = transaction.Currency,
                    Description = product.Name,
                    SaveCard = product.PaymentType == PaymentType.Recurring,
                },
                ct);
        }
        catch (PaymentGatewayException ex)
        {
            // Nothing was charged: the payment page was never created.
            await MarkGatewayFailureAsync(transaction, ex, ct);
            return;
        }

        transaction.PaymentToken = hosted.PaymentToken;
        transaction.RedirectUrl = hosted.PaymentUrl;
        await transactions.SaveChangesAsync(ct);
    }

    private async Task ChargeSavedCardAsync(
        Transaction transaction,
        Product product,
        CustomerCard card,
        CancellationToken ct)
    {
        SavedCardResult charged;
        try
        {
            charged = await gateway.ChargeSavedCardAsync(
                new SavedCardRequest
                {
                    InvoiceNo = transaction.InvoiceNo,
                    CustomerToken = card.CustomerToken,
                    Amount = transaction.Amount,
                    Currency = transaction.Currency,
                    Description = product.Name,
                },
                ct);
        }
        catch (PaymentGatewayException ex)
        {
            // Rejected before the charge.
            await MarkGatewayFailureAsync(transaction, ex, ct);
            return;
        }

        // Saved first, while the transaction is still tracked as Pending. RedirectUrl is the
        // 3-D Secure page when the issuer asks for it, otherwise null.
        transaction.PaymentToken = charged.PaymentToken;
        transaction.RedirectUrl = charged.RedirectUrl;
        await transactions.SaveChangesAsync(ct);

        // Succeeded or Failed from the Payment Inquiry (or a declined Do Payment) is applied
        // now. Pending (3-D Secure, or an unknown outcome) waits for the notification.
        await results.ApplyAsync(transaction, charged.Payment, ct);
    }

    private async Task MarkGatewayFailureAsync(Transaction transaction, PaymentGatewayException ex, CancellationToken ct)
    {
        logger.LogWarning(ex, "The payment gateway rejected checkout {InvoiceNo}; marking it Failed.", transaction.InvoiceNo);

        // The provider's own response code and description when it answered; otherwise why it could not be used.
        var description = ex.ProviderDescription ?? ex.Message;
        description = description.Length > 500 ? description[..500] : description;
        if (transaction.MarkFailed(ex.ProviderCode ?? "GATEWAY_ERROR", description))
        {
            await transactions.UpdateIfPendingAsync(transaction, ct);
        }
    }

    private static CheckoutResponse Replay(Transaction existing, CheckoutRequest request)
    {
        if (existing.CustomerId != request.CustomerId
            || existing.ProductId != request.ProductId
            || existing.CustomerCardId != request.CustomerCardId)
        {
            throw new BusinessRuleException("This Idempotency-Key was already used for a different checkout.");
        }

        // Inserted but the gateway has not answered yet: the first request is still working.
        // An old row with no answer is returned as is, so a crashed request cannot block
        // its key forever.
        if (existing.Status == TransactionStatus.Pending
            && existing.PaymentToken is null
            && existing.CreatedAt > DateTime.UtcNow - LockTtl)
        {
            throw new ConflictException("A checkout with this Idempotency-Key is in progress.", RetryAfter);
        }

        return ToResponse(existing);
    }

    private static CheckoutResponse ToResponse(Transaction transaction) => new()
    {
        TransactionId = transaction.Id,
        InvoiceNo = transaction.InvoiceNo,
        Amount = transaction.Amount,
        Currency = transaction.Currency,
        Status = transaction.Status.ToString(),
        // A final saved-card payment has nothing left to do at the 3-D Secure page.
        RedirectUrl = transaction.Status == TransactionStatus.Pending ? transaction.RedirectUrl : null,
    };

    private static string ValidateKey(string? idempotencyKey)
    {
        var key = idempotencyKey?.Trim();

        if (string.IsNullOrEmpty(key))
        {
            throw new ValidationException("Idempotency-Key", "The Idempotency-Key header is required.");
        }

        if (key.Length > MaxIdempotencyKeyLength)
        {
            throw new ValidationException(
                "Idempotency-Key", $"The Idempotency-Key header must be at most {MaxIdempotencyKeyLength} characters.");
        }

        return key;
    }

    // Alphanumeric and at most 50 characters: the invoice_no column, and what the provider
    // accepts. The timestamp keeps them sortable; the random suffix makes a collision
    // practically impossible (and the unique index would reject one).
    private static string NewInvoiceNo()
        => $"INV{DateTime.UtcNow:yyyyMMddHHmmss}{RandomNumberGenerator.GetString(InvoiceChars, 6)}";
}
