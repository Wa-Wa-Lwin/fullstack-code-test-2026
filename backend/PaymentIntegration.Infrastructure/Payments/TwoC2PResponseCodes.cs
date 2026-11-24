using PaymentIntegration.Application.DTOs.Payments;

namespace PaymentIntegration.Infrastructure.Payments;

/// <summary>
/// The 2C2P response codes this integration acts on. Only this class reads them; the
/// Application layer sees a <see cref="PaymentOutcome"/> and stores the raw code as is.
/// </summary>
internal static class TwoC2PResponseCodes
{
    public const string Success = "0000";

    // Do Payment flow codes.
    public const string RedirectToAuthentication = "1001";
    public const string CompletedInquiryRequired = "2000";

    private const string Pending = "0001";
    private const string InProgress = "2001";
    private const string TransactionNotFound = "2002";
    private const string InquiryRecordNotFound = "4071";

    private const string SystemError = "0999";
    private const string Timeout = "5002";
    private const string InternalError = "5998";
    private const string PaymentExpired = "9020";
    private const string PaymentFailed = "9035";

    /// <summary>
    /// Maps a payment result code (Payment Inquiry or backend notification) to an outcome.
    /// Only 0000 is a success. Codes that mean "not finished yet" stay Pending, so a payment
    /// the customer is still completing is never marked Failed.
    /// </summary>
    public static PaymentOutcome ToOutcome(string? code) => code switch
    {
        Success => PaymentOutcome.Succeeded,
        Pending or InProgress or TransactionNotFound or InquiryRecordNotFound => PaymentOutcome.Pending,
        _ => PaymentOutcome.Failed,
    };

    /// <summary>
    /// True when the code says 2C2P could not process the request itself (a system error,
    /// a timeout, or a rejected or invalid request: the 6xxx codes and most 9xxx codes),
    /// rather than reporting a payment result. Such an inquiry answer says nothing about
    /// the payment, so it must not mark the payment Failed.
    /// </summary>
    public static bool IsRequestOrSystemError(string? code) => code switch
    {
        null or "" => true,
        SystemError or Timeout or InternalError => true,
        PaymentExpired or PaymentFailed => false,
        _ => code[0] is '6' or '9',
    };
}
