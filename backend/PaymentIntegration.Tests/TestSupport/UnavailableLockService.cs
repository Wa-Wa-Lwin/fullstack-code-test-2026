using PaymentIntegration.Application.Interfaces;

namespace PaymentIntegration.Tests.TestSupport;

/// <summary>Behaves like the Redis lock while Redis is down: every call throws.</summary>
public sealed class UnavailableLockService : IDistributedLockService
{
    public Task<bool> AcquireLockAsync(string key, string lockValue, TimeSpan expiration, CancellationToken ct)
        => throw new InvalidOperationException("Redis is down.");

    public Task ReleaseLockAsync(string key, string lockValue, CancellationToken ct)
        => throw new InvalidOperationException("Redis is down.");
}
