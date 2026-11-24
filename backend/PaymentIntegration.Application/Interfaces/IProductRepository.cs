using PaymentIntegration.Domain.Entities;

namespace PaymentIntegration.Application.Interfaces;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(
        int id,
        CancellationToken ct);

    Task<IReadOnlyList<Product>> GetAllAsync(
        CancellationToken ct);
}
