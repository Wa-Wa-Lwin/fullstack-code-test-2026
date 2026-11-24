using Microsoft.EntityFrameworkCore;
using PaymentIntegration.Application.Interfaces;
using PaymentIntegration.Domain.Entities;
using PaymentIntegration.Domain.Enums;
using PaymentIntegration.Infrastructure.Persistence;

namespace PaymentIntegration.Infrastructure.Repositories;

public class TransactionRepository(PaymentDbContext db) : ITransactionRepository
{
    // Reads are untracked. A caller changes a transaction through UpdateAsync or
    // UpdateIfPendingAsync, never by relying on change tracking.
    public Task<Transaction?> GetByIdAsync(long id, CancellationToken ct)
        => db.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<Transaction?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct)
        => db.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey, ct);

    public Task<Transaction?> GetByInvoiceNoAsync(string invoiceNo, CancellationToken ct)
        => db.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.InvoiceNo == invoiceNo, ct);

    public Task AddAsync(Transaction transaction, CancellationToken ct)
    {
        db.Transactions.Add(transaction);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Transaction transaction, CancellationToken ct)
    {
        // Reads are untracked, so attach the entity and mark it Modified for SaveChangesAsync.
        db.Transactions.Update(transaction);
        return Task.CompletedTask;
    }

    public async Task<bool> UpdateIfPendingAsync(Transaction transaction, CancellationToken ct)
    {
        if (transaction.Status == TransactionStatus.Pending)
        {
            throw new InvalidOperationException(
                "Call MarkSucceeded or MarkFailed before saving the payment result.");
        }

        // One UPDATE ... WHERE id = @id AND status = 'Pending'. MySQL locks the row for the
        // first handler; a concurrent second handler re-checks the WHERE after the lock is
        // released, sees the final status and changes 0 rows. MarkSucceeded/MarkFailed only
        // checked this handler's in-memory copy, so the WHERE is what decides the winner.
        var rows = await db.Transactions
            .Where(t => t.Id == transaction.Id && t.Status == TransactionStatus.Pending)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(t => t.Status, transaction.Status)
                    .SetProperty(t => t.RespCode, transaction.RespCode)
                    .SetProperty(t => t.RespDesc, transaction.RespDesc)
                    .SetProperty(t => t.ProviderRef, transaction.ProviderRef)
                    .SetProperty(t => t.PaymentChannel, transaction.PaymentChannel)
                    .SetProperty(t => t.MaskedCardNo, transaction.MaskedCardNo)
                    .SetProperty(t => t.PaidAt, transaction.PaidAt),
                ct);

        // If this context tracks the entity (it was added in this scope), a later
        // SaveChangesAsync would write its status again without the Pending check and could
        // overwrite the winner's result. Stop tracking it, as if it had been read untracked.
        var entry = db.Entry(transaction);
        if (entry.State != EntityState.Detached)
        {
            entry.State = EntityState.Detached;
        }

        return rows == 1;
    }

    public Task SaveChangesAsync(CancellationToken ct)
        => db.SaveChangesAsync(ct);
}
