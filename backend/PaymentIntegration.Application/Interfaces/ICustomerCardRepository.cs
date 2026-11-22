using PaymentIntegration.Domain.Entities;

namespace PaymentIntegration.Application.Interfaces;

public interface ICustomerCardRepository
{
    Task<CustomerCard?> GetByIdAsync(int id);

    Task<IReadOnlyList<CustomerCard>> GetByCustomerIdAsync(int customerId);

    Task AddAsync(CustomerCard card);

    Task SaveChangesAsync();
}
