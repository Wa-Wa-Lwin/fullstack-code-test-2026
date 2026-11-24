using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using PaymentIntegration.Application.Interfaces;
using PaymentIntegration.Domain.Entities;
using PaymentIntegration.Infrastructure.Persistence;

namespace PaymentIntegration.Infrastructure.Repositories;

public class CustomerCardRepository(PaymentDbContext db) : ICustomerCardRepository
{
    public Task<CustomerCard?> GetByIdAsync(int id, CancellationToken ct)
        => db.CustomerCards
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<CustomerCard>> GetByCustomerIdAsync(int customerId, CancellationToken ct)
        => await db.CustomerCards
            .AsNoTracking()
            .Where(c => c.CustomerId == customerId)
            .OrderBy(c => c.Id)
            .ToListAsync(ct);

    public Task AddAsync(CustomerCard card, CancellationToken ct)
    {
        db.CustomerCards.Add(card);
        return Task.CompletedTask;
    }

    public async Task<bool> AddIfNotExistsAsync(CustomerCard card, CancellationToken ct)
    {
        // Every recurring payment reports the same card again, so check first to keep the
        // common case free of exceptions. This check alone is not safe under concurrency.
        var exists = await db.CustomerCards.AnyAsync(
            c => c.CustomerId == card.CustomerId && c.CustomerToken == card.CustomerToken,
            ct);

        if (exists)
        {
            return false;
        }

        db.CustomerCards.Add(card);

        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException
        {
            Number: (int)MySqlErrorCode.DuplicateKeyEntry
        })
        {
            // A concurrent request inserted the same card between the check and the insert.
            // The unique index on (customer_id, customer_token) rejected this row.
            db.Entry(card).State = EntityState.Detached;
            return false;
        }
    }

    public Task SaveChangesAsync(CancellationToken ct)
        => db.SaveChangesAsync(ct);
}
