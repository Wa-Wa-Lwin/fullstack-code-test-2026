using PaymentIntegration.Domain.Entities;

namespace PaymentIntegration.Application.Interfaces;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(int id);
}
