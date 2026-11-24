using PaymentIntegration.Application.DTOs.Payments;

namespace PaymentIntegration.Application.Interfaces;

public interface IPaymentGateway
{
    Task<HostedPaymentResult> CreateHostedPaymentAsync(
        HostedPaymentRequest request,
        CancellationToken ct);

    Task<SavedCardResult> ChargeSavedCardAsync(
        SavedCardRequest request,
        CancellationToken ct);

    Task<PaymentResult> InquireAsync(
        string invoiceNo,
        CancellationToken ct);

    PaymentResult ParseNotification(string payload);
}
