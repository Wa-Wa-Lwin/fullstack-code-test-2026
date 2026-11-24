using PaymentIntegration.Application.Caching;
using PaymentIntegration.Application.DTOs;
using PaymentIntegration.Application.Exceptions;
using PaymentIntegration.Application.Interfaces;
using PaymentIntegration.Domain.Entities;

namespace PaymentIntegration.Application.Services;

public class CustomerService(
    ICustomerRepository customers,
    ICustomerCardRepository cards,
    ICacheService cache)
{
    private static readonly TimeSpan CustomersTtl = TimeSpan.FromMinutes(10);

    // Shorter than the customer list: if an invalidation ever fails, a new card is
    // missing from the list for at most this long.
    private static readonly TimeSpan CardsTtl = TimeSpan.FromMinutes(5);

    /// <summary>Cache-aside: read Redis first; on a miss, read MySQL and fill the cache.</summary>
    public async Task<IReadOnlyList<CustomerDto>> GetAllAsync(CancellationToken ct)
    {
        var cached = await cache.GetAsync<List<CustomerDto>>(CacheKeys.Customers, ct);
        if (cached is not null)
        {
            return cached;
        }

        var result = (await customers.GetAllAsync(ct))
            .Select(ToDto)
            .ToList();

        await cache.SetAsync(CacheKeys.Customers, result, CustomersTtl, ct);
        return result;
    }

    /// <summary>Cache-aside, like <see cref="GetAllAsync"/>, one entry per customer.</summary>
    public async Task<IReadOnlyList<CustomerCardDto>> GetCardsAsync(
        int customerId,
        CancellationToken ct)
    {
        var key = CacheKeys.CustomerCards(customerId);

        var cached = await cache.GetAsync<List<CustomerCardDto>>(key, ct);
        if (cached is not null)
        {
            return cached;
        }

        if (await customers.GetByIdAsync(customerId, ct) is null)
        {
            throw new NotFoundException(nameof(Customer), customerId);
        }

        var result = (await cards.GetByCustomerIdAsync(customerId, ct))
            .Select(ToDto)
            .ToList();

        await cache.SetAsync(key, result, CardsTtl, ct);
        return result;
    }

    /// <summary>
    /// Saves a card unless the customer already has it, then clears the customer's cached
    /// card list so the next read loads the new card from the database.
    /// The payment notification handler saves cards through this method, so the
    /// invalidation cannot be forgotten. Returns false if the card was already saved.
    /// </summary>
    public async Task<bool> SaveCardAsync(
        CustomerCard card,
        CancellationToken ct)
    {
        // Database first, then the cache. Deleting first would let a read in between
        // refill the cache from the database before the new card is there.
        var added = await cards.AddIfNotExistsAsync(card, ct);

        // CancellationToken.None: the card is already saved, so the invalidation must run
        // even if the request is cancelled now.
        await cache.RemoveAsync(CacheKeys.CustomerCards(card.CustomerId), CancellationToken.None);

        return added;
    }

    private static CustomerDto ToDto(Customer customer)
        => new()
        {
            Id = customer.Id,
            FirstName = customer.FirstName,
            LastName = customer.LastName,
            Email = customer.Email
        };

    private static CustomerCardDto ToDto(CustomerCard card)
        => new()
        {
            Id = card.Id,
            CustomerId = card.CustomerId,
            MaskedCardNo = card.MaskedCardNo,
            CreatedAt = card.CreatedAt
        };
}
