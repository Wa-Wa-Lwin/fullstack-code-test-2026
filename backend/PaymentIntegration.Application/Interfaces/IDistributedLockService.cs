namespace PaymentIntegration.Application.Interfaces;

public interface IDistributedLockService
{
    Task<bool> AcquireLockAsync(
        string key,
        string lockValue,
        TimeSpan expiration);

    Task ReleaseLockAsync(string key, string lockValue);
}
