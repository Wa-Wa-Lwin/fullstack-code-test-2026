namespace PaymentIntegration.Application.Interfaces;

public interface IDistributedLockService
{
    Task<bool> AcquireLockAsync(
        string key,
        string lockValue,
        TimeSpan expiration,
        CancellationToken ct);

    /// <summary>
    /// Releases the lock only if it still holds <paramref name="lockValue"/>.
    /// Call it from a finally block with <see cref="CancellationToken.None"/>,
    /// so a cancelled request still releases its lock.
    /// </summary>
    Task ReleaseLockAsync(
        string key,
        string lockValue,
        CancellationToken ct);
}
