using PaymentIntegration.Domain.Entities;

namespace PaymentIntegration.Application.Interfaces;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(int id);
}
