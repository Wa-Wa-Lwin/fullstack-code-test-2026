using PaymentIntegration.Domain.Entities;

namespace PaymentIntegration.Application.Interfaces;

public interface ITransactionRepository
{
    Task<Transaction?> GetByIdAsync(
        long id,
        CancellationToken ct);

    Task<Transaction?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken ct);

    Task<Transaction?> GetByInvoiceNoAsync(
        string invoiceNo,
        CancellationToken ct);

    Task AddAsync(
        Transaction transaction,
        CancellationToken ct);

    /// <summary>
    /// Inserts the transaction and writes it at once. Returns false, inserting nothing, if
    /// a row with the same idempotency key or invoice number already exists: the unique
    /// indexes decide, so of two concurrent inserts exactly one wins. On success the
    /// transaction stays tracked, so a later <see cref="SaveChangesAsync"/> writes changes
    /// made to it (such as the payment token).
    /// </summary>
    Task<bool> TryAddAsync(
        Transaction transaction,
        CancellationToken ct);

    Task UpdateAsync(
        Transaction transaction,
        CancellationToken ct);

    /// <summary>
    /// Saves the transaction's final status and payment result in one atomic database update
    /// that only matches a row whose stored status is still Pending. Returns true if the row
    /// changed, or false if it was no longer Pending or no row matched. It writes to the
    /// database at once, without <see cref="SaveChangesAsync"/>.
    /// </summary>
    Task<bool> UpdateIfPendingAsync(
        Transaction transaction,
        CancellationToken ct);

    Task SaveChangesAsync(
        CancellationToken ct);
}
