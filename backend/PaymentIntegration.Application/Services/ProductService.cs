using PaymentIntegration.Application.Caching;
using PaymentIntegration.Application.DTOs;
using PaymentIntegration.Application.Interfaces;
using PaymentIntegration.Domain.Entities;

namespace PaymentIntegration.Application.Services;

public class ProductService(
    IProductRepository products,
    ICacheService cache)
{
    private static readonly TimeSpan ProductsTtl = TimeSpan.FromMinutes(10);

    /// <summary>Cache-aside: read Redis first; on a miss, read MySQL and fill the cache.</summary>
    public async Task<IReadOnlyList<ProductDto>> GetAllAsync(CancellationToken ct)
    {
        var cached = await cache.GetAsync<List<ProductDto>>(CacheKeys.Products, ct);
        if (cached is not null)
        {
            return cached;
        }

        var result = (await products.GetAllAsync(ct))
            .Select(ToDto)
            .ToList();

        await cache.SetAsync(CacheKeys.Products, result, ProductsTtl, ct);
        return result;
    }

    private static ProductDto ToDto(Product product)
        => new()
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            Currency = product.Currency,
            PaymentType = product.PaymentType.ToString()
        };
}
