using PaymentIntegration.Application.DTOs.Payments;
using PaymentIntegration.Application.Exceptions;

namespace PaymentIntegration.Application.Interfaces;

/// <summary>
/// A payment provider. Implementations hold every provider detail (endpoints, signing,
/// response codes); callers only see the provider-neutral types in <c>DTOs/Payments</c>.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>
    /// Creates a payment on the provider's hosted payment page and returns its URL.
    /// Nothing is charged until the customer pays on that page.
    /// </summary>
    /// <exception cref="PaymentGatewayException">
    /// The provider rejected the request or could not be reached. Nothing was charged.
    /// </exception>
    Task<HostedPaymentResult> CreateHostedPaymentAsync(
        HostedPaymentRequest request,
        CancellationToken ct);

    /// <summary>
    /// Charges a saved card. The result is Succeeded, Failed, or Pending. Pending means the
    /// customer must authenticate at <see cref="SavedCardResult.RedirectUrl"/>, or the
    /// outcome is unknown and the provider's notification will settle it.
    /// </summary>
    /// <exception cref="PaymentGatewayException">
    /// The provider rejected the request before the charge or could not be reached.
    /// Nothing was charged.
    /// </exception>
    Task<SavedCardResult> ChargeSavedCardAsync(
        SavedCardRequest request,
        CancellationToken ct);

    /// <summary>Asks the provider for the current result of a payment.</summary>
    /// <exception cref="PaymentGatewayException">
    /// The provider could not be reached or could not answer. The payment's state is
    /// unknown, so do not treat this as a failed payment.
    /// </exception>
    Task<PaymentResult> InquireAsync(
        string invoiceNo,
        CancellationToken ct);

    /// <summary>
    /// Verifies and reads a payment notification the provider sent to the backend.
    /// </summary>
    /// <exception cref="ValidationException">
    /// The payload is empty, unreadable, or its signature is invalid.
    /// </exception>
    Task<PaymentResult> ParseNotificationAsync(
        string payload,
        CancellationToken ct);
}
