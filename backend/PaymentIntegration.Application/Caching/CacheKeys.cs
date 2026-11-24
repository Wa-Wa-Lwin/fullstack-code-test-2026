namespace PaymentIntegration.Application.Caching;

/// <summary>
/// Every cache key is built here, so the key a read uses and the key an invalidation
/// deletes can never drift apart.
/// There is deliberately no key for transactions: a status changes from Pending whenever
/// the payment notification arrives, so it is always read from the database.
/// </summary>
public static class CacheKeys
{
    public const string Customers = "customers";

    public const string Products = "products";

    public static string CustomerCards(int customerId)
        => $"customer:{customerId}:cards";
}
