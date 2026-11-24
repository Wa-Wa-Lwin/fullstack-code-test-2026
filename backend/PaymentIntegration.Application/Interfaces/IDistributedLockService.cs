namespace PaymentIntegration.Application.Interfaces;

public interface IDistributedLockService
{
    /// <summary>
    /// Takes the lock if no one holds it. Returns false if another owner holds it.
    /// <paramref name="expiration"/> must be longer than the locked work can take. For
    /// checkout that means longer than all the payment gateway calls made while the lock is
    /// held, plus the database work, or the lock can expire mid-call and let a retry in.
    /// </summary>
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
