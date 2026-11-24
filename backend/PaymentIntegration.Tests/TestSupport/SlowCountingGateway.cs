using PaymentIntegration.Application.DTOs.Payments;
using PaymentIntegration.Application.Interfaces;

namespace PaymentIntegration.Tests.TestSupport;

/// <summary>
/// Stands in for 2C2P in the checkout tests. Each call takes about 300ms, so concurrent
/// requests overlap, and every call is counted.
/// </summary>
public sealed class SlowCountingGateway : IPaymentGateway
{
    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    public async Task<HostedPaymentResult> CreateHostedPaymentAsync(HostedPaymentRequest request, CancellationToken ct)
    {
        Interlocked.Increment(ref _calls);
        await Task.Delay(TimeSpan.FromMilliseconds(300), ct);
        return new HostedPaymentResult
        {
            PaymentUrl = $"https://pay.test/{request.InvoiceNo}",
            PaymentToken = $"PT-{request.InvoiceNo}",
        };
    }

    public Task<SavedCardResult> ChargeSavedCardAsync(SavedCardRequest request, CancellationToken ct)
        => throw new NotSupportedException();

    public Task<PaymentResult> InquireAsync(string invoiceNo, CancellationToken ct)
        => throw new NotSupportedException();

    public Task<PaymentResult> ParseNotificationAsync(string payload, CancellationToken ct)
        => throw new NotSupportedException();
}
