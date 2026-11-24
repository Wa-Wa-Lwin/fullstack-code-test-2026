using Microsoft.EntityFrameworkCore;
using PaymentIntegration.Application.Interfaces;
using PaymentIntegration.Domain.Entities;
using PaymentIntegration.Infrastructure.Persistence;

namespace PaymentIntegration.Infrastructure.Repositories;

public class CustomerRepository(PaymentDbContext db) : ICustomerRepository
{
    public Task<Customer?> GetByIdAsync(int id, CancellationToken ct)
        => db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken ct)
        => await db.Customers
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .ToListAsync(ct);
}
