using Microsoft.EntityFrameworkCore;
using PaymentIntegration.Application.Interfaces;
using PaymentIntegration.Domain.Entities;
using PaymentIntegration.Infrastructure.Persistence;

namespace PaymentIntegration.Infrastructure.Repositories;

public class ProductRepository(PaymentDbContext db) : IProductRepository
{
    public Task<Product?> GetByIdAsync(int id, CancellationToken ct)
        => db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct)
        => await db.Products
            .AsNoTracking()
            .OrderBy(p => p.Id)
            .ToListAsync(ct);
}
