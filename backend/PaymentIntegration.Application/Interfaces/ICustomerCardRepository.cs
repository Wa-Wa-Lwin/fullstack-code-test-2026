using PaymentIntegration.Domain.Entities;

namespace PaymentIntegration.Application.Interfaces;

public interface ICustomerCardRepository
{
    Task<CustomerCard?> GetByIdAsync(
        int id,
        CancellationToken ct);

    Task<IReadOnlyList<CustomerCard>> GetByCustomerIdAsync(
        int customerId,
        CancellationToken ct);

    Task AddAsync(
        CustomerCard card,
        CancellationToken ct);

    /// <summary>
    /// Inserts the card unless the same card is already saved. A database unique constraint
    /// decides what counts as the same card, so two requests that save it at once still create
    /// only one row. Returns true if a row was inserted, or false if the card already existed.
    /// It writes to the database at once, without <see cref="SaveChangesAsync"/>.
    /// </summary>
    Task<bool> AddIfNotExistsAsync(
        CustomerCard card,
        CancellationToken ct);

    Task SaveChangesAsync(
        CancellationToken ct);
}
