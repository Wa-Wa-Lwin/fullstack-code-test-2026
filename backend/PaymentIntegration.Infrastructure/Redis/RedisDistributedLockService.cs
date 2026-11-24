using PaymentIntegration.Application.Interfaces;
using StackExchange.Redis;

namespace PaymentIntegration.Infrastructure.Redis;

/// <summary>
/// A lock held as a Redis key whose value is the owner's lockValue.
/// Redis errors are not caught here, so the caller decides what to do when Redis is down.
/// Checkout continues without the lock: it inserts the Pending transaction before calling
/// the payment gateway, so the unique index on idempotency_key still lets only one
/// request through.
/// </summary>
public class RedisDistributedLockService(IConnectionMultiplexer redis) : IDistributedLockService
{
    // Deletes the key only if it still holds this owner's value. Redis runs a script from
    // start to finish with no other command in between, so no other request can take the
    // lock between the GET and the DEL. Returns 1 if the key was deleted, 0 if not.
    private const string ReleaseScript = """
        if redis.call('get', KEYS[1]) == ARGV[1] then
            return redis.call('del', KEYS[1])
        else
            return 0
        end
        """;

    public Task<bool> AcquireLockAsync(
        string key,
        string lockValue,
        TimeSpan expiration,
        CancellationToken ct)
    {
        // StackExchange.Redis commands take no CancellationToken, so check before sending.
        ct.ThrowIfCancellationRequested();

        // One atomic SET key value PX <ms> NX. Only one request can create the key, and it
        // always gets an expiry, so a crashed request's lock frees itself after the TTL.
        return redis.GetDatabase().StringSetAsync(
            key,
            lockValue,
            expiration,
            When.NotExists);
    }

    public async Task ReleaseLockAsync(
        string key,
        string lockValue,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        await redis.GetDatabase().ScriptEvaluateAsync(
            ReleaseScript,
            new RedisKey[] { key },
            new RedisValue[] { lockValue });
    }
}
