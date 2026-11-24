using System.Text.Json;
using Microsoft.Extensions.Logging;
using PaymentIntegration.Application.Interfaces;
using StackExchange.Redis;

namespace PaymentIntegration.Infrastructure.Redis;

/// <summary>
/// Stores values in Redis as JSON. The cache only makes reads faster: MySQL has all the data.
/// So a Redis failure is logged and treated as a miss (or a skipped write), and the caller
/// reads from the database instead of returning an error.
/// </summary>
public class RedisCacheService(
    IConnectionMultiplexer redis,
    ILogger<RedisCacheService> logger) : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<T?> GetAsync<T>(
        string key,
        CancellationToken ct)
    {
        // StackExchange.Redis commands take no CancellationToken, so check before sending.
        ct.ThrowIfCancellationRequested();

        try
        {
            var value = await redis.GetDatabase().StringGetAsync(key);

            return value.IsNullOrEmpty
                ? default
                : JsonSerializer.Deserialize<T>(value.ToString(), JsonOptions);
        }
        catch (Exception ex) when (IsRedisFailure(ex))
        {
            logger.LogWarning(ex, "Cache read failed for {CacheKey}; reading from the database instead.", key);
            return default;
        }
        catch (JsonException ex)
        {
            // An entry written by an older version of a DTO. Treat it as a miss; the caller
            // overwrites it with fresh data.
            logger.LogWarning(ex, "Cached value for {CacheKey} could not be read; ignoring it.", key);
            return default;
        }
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan expiration,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            var json = JsonSerializer.Serialize(value, JsonOptions);
            await redis.GetDatabase().StringSetAsync(key, json, expiration);
        }
        catch (Exception ex) when (IsRedisFailure(ex))
        {
            logger.LogWarning(ex, "Cache write failed for {CacheKey}; the next read goes to the database.", key);
        }
    }

    public async Task RemoveAsync(
        string key,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            await redis.GetDatabase().KeyDeleteAsync(key);
        }
        catch (Exception ex) when (IsRedisFailure(ex))
        {
            // Throwing would not help the caller: its database write already happened.
            // The stale entry disappears when its TTL runs out.
            logger.LogError(ex, "Cache invalidation failed for {CacheKey}; it stays stale until its TTL expires.", key);
        }
    }

    // RedisTimeoutException derives from TimeoutException, not RedisException.
    private static bool IsRedisFailure(Exception ex)
        => ex is RedisException or RedisTimeoutException;
}
