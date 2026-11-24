using PaymentIntegration.Domain.Entities;

namespace PaymentIntegration.Application.Interfaces;

public interface ITransactionRepository
{
    Task<Transaction?> GetByIdAsync(long id);

    Task<Transaction?> GetByIdempotencyKeyAsync(string idempotencyKey);

    Task AddAsync(Transaction transaction);

    Task UpdateAsync(Transaction transaction);

    Task SaveChangesAsync();
}
