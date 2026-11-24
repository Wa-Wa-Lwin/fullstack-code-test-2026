using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PaymentIntegration.Application.DTOs.Payments;
using PaymentIntegration.Application.Exceptions;
using PaymentIntegration.Application.Interfaces;

namespace PaymentIntegration.Infrastructure.Payments;

/// <summary>
/// 2C2P PGW v4.3 over plain HTTP (the spec allows no 2C2P SDK). Registered as a typed
/// HttpClient with no retry handler: retrying Do Payment could charge the customer twice.
/// </summary>
public sealed class TwoC2PPaymentGateway : IPaymentGateway
{
    private const string PaymentTokenPath = "paymentToken";
    private const string DoPaymentPath = "payment";
    private const string PaymentInquiryPath = "paymentInquiry";

    private const string CardChannel = "CC";
    private const string No3DS = "N";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private readonly HttpClient _http;
    private readonly TwoC2POptions _options;
    private readonly TwoC2PJwt _jwt;
    private readonly ILogger<TwoC2PPaymentGateway> _logger;

    public TwoC2PPaymentGateway(
        HttpClient http,
        IOptions<TwoC2POptions> options,
        ILogger<TwoC2PPaymentGateway> logger)
    {
        _http = http;
        _options = options.Value;
        _jwt = new TwoC2PJwt(_options.SecretKey);
        _logger = logger;
    }

    public async Task<HostedPaymentResult> CreateHostedPaymentAsync(
        HostedPaymentRequest request,
        CancellationToken ct)
    {
        var tokenRequest = NewPaymentTokenRequest(
            request.InvoiceNo, request.Description, request.Amount, request.Currency);

        if (request.SaveCard)
        {
            // Card only, and store the card: 2C2P returns a customerToken with the result.
            tokenRequest = tokenRequest with { PaymentChannel = [CardChannel], Tokenize = true };
        }

        var token = await RequestPaymentTokenAsync(tokenRequest, ct);

        if (string.IsNullOrEmpty(token.WebPaymentUrl))
        {
            throw new PaymentGatewayException(
                $"2C2P returned no payment page URL for invoice {request.InvoiceNo}.", token.RespCode);
        }

        return new HostedPaymentResult
        {
            PaymentUrl = token.WebPaymentUrl,
            PaymentToken = token.PaymentToken!,
        };
    }

    public async Task<SavedCardResult> ChargeSavedCardAsync(
        SavedCardRequest request,
        CancellationToken ct)
    {
        var tokenRequest = NewPaymentTokenRequest(
                request.InvoiceNo, request.Description, request.Amount, request.Currency)
            with
            {
                PaymentChannel = [CardChannel],
                CustomerToken = [request.CustomerToken],
                CustomerTokenOnly = true,
                Request3DS = No3DS,
            };

        // Throws PaymentGatewayException when rejected. Nothing has been charged yet.
        var token = await RequestPaymentTokenAsync(tokenRequest, ct);
        var paymentToken = token.PaymentToken!;

        var doPayment = new DoPaymentRequest
        {
            PaymentToken = paymentToken,
            Payment = new DoPaymentMethod
            {
                Code = new DoPaymentCode { ChannelCode = CardChannel },
                Data = new DoPaymentData { CustomerToken = request.CustomerToken },
            },
        };

        DoPaymentResponse response;
        try
        {
            // Sent exactly once. A failure here leaves the charge's fate unknown, so it is
            // reported as Pending, never retried and never assumed failed.
            (response, _) = await PostAsync<DoPaymentResponse>(DoPaymentPath, doPayment, "Do Payment", ct);
        }
        catch (PaymentGatewayException ex)
        {
            _logger.LogError(ex, "2C2P Do Payment for invoice {InvoiceNo} gave no usable answer; leaving it Pending.", request.InvoiceNo);
            return SavedCardResultOf(paymentToken, UnknownOutcome(request.InvoiceNo, ex.ProviderCode));
        }

        _logger.LogInformation(
            "2C2P Do Payment for invoice {InvoiceNo} returned {RespCode} {RespDesc}.",
            request.InvoiceNo, response.RespCode, response.RespDesc);

        switch (response.RespCode)
        {
            case null or "":
                return SavedCardResultOf(paymentToken, UnknownOutcome(request.InvoiceNo, null));

            case TwoC2PResponseCodes.RedirectToAuthentication:
                // The issuer wants 3-D Secure even though request3DS was "N". The customer
                // completes it at this URL and 2C2P then sends the backend notification.
                var result = SavedCardResultOf(paymentToken, new PaymentResult
                {
                    Outcome = PaymentOutcome.Pending,
                    InvoiceNo = request.InvoiceNo,
                    RawCode = response.RespCode,
                    Description = response.RespDesc,
                    PaymentChannel = NullIfEmpty(response.ChannelCode),
                });
                result.RedirectUrl = NullIfEmpty(response.Data);
                return result;

            case TwoC2PResponseCodes.CompletedInquiryRequired:
                // The charge finished. Only the Payment Inquiry says whether it succeeded.
                try
                {
                    return SavedCardResultOf(paymentToken, await InquireAsync(request.InvoiceNo, ct));
                }
                catch (PaymentGatewayException ex)
                {
                    _logger.LogError(ex, "2C2P Payment Inquiry for invoice {InvoiceNo} failed after Do Payment completed; leaving it Pending.", request.InvoiceNo);
                    return SavedCardResultOf(paymentToken, UnknownOutcome(request.InvoiceNo, response.RespCode));
                }

            default:
                return SavedCardResultOf(paymentToken, new PaymentResult
                {
                    Outcome = PaymentOutcome.Failed,
                    InvoiceNo = request.InvoiceNo,
                    RawCode = response.RespCode,
                    Description = response.RespDesc,
                    PaymentChannel = NullIfEmpty(response.ChannelCode),
                });
        }
    }

    public async Task<PaymentResult> InquireAsync(string invoiceNo, CancellationToken ct)
    {
        var inquiry = new PaymentInquiryRequest { MerchantId = _options.MerchantId, InvoiceNo = invoiceNo };

        var (response, signed) = await PostAsync<PaymentResultPayload>(
            PaymentInquiryPath, SignedEnvelope(inquiry), "Payment Inquiry", ct);

        _logger.LogInformation(
            "2C2P Payment Inquiry for invoice {InvoiceNo} returned {RespCode} {RespDesc}.",
            invoiceNo, response.RespCode, response.RespDesc);

        if (TwoC2PResponseCodes.IsRequestOrSystemError(response.RespCode))
        {
            throw new PaymentGatewayException(
                $"2C2P could not answer the Payment Inquiry for invoice {invoiceNo}: {response.RespCode} {response.RespDesc}",
                response.RespCode);
        }

        RequireSignedSuccess(signed, response.RespCode, "Payment Inquiry");

        return ToPaymentResult(response);
    }

    public async Task<PaymentResult> ParseNotificationAsync(string payload, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new ValidationException("payload", "The notification payload is empty.");
        }

        string json;
        try
        {
            json = await _jwt.VerifyAsync(payload);
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "Rejected a 2C2P notification whose signature could not be verified.");
            throw new ValidationException("payload", "The notification signature is invalid.");
        }

        PaymentResultPayload? notification;
        try
        {
            notification = JsonSerializer.Deserialize<PaymentResultPayload>(json, JsonOptions);
        }
        catch (JsonException)
        {
            notification = null;
        }

        if (notification is null
            || string.IsNullOrEmpty(notification.InvoiceNo)
            || string.IsNullOrEmpty(notification.RespCode))
        {
            throw new ValidationException("payload", "The notification has no invoiceNo or respCode.");
        }

        return ToPaymentResult(notification);
    }

    private PaymentTokenRequest NewPaymentTokenRequest(
        string invoiceNo,
        string description,
        decimal amount,
        string currency)
        => new()
        {
            MerchantId = _options.MerchantId,
            InvoiceNo = invoiceNo,
            // 2C2P requires special characters in the description to be HTML-encoded.
            Description = WebUtility.HtmlEncode(description),
            Amount = amount,
            CurrencyCode = currency,
            FrontendReturnUrl = _options.FrontendReturnUrl,
            BackendReturnUrl = _options.BackendReturnUrl,
        };

    private async Task<PaymentTokenResponse> RequestPaymentTokenAsync(
        PaymentTokenRequest request,
        CancellationToken ct)
    {
        var (response, signed) = await PostAsync<PaymentTokenResponse>(
            PaymentTokenPath, SignedEnvelope(request), "Payment Token", ct);

        _logger.LogInformation(
            "2C2P Payment Token for invoice {InvoiceNo} returned {RespCode} {RespDesc}.",
            request.InvoiceNo, response.RespCode, response.RespDesc);

        if (response.RespCode != TwoC2PResponseCodes.Success)
        {
            throw new PaymentGatewayException(
                $"2C2P rejected the Payment Token request for invoice {request.InvoiceNo}: {response.RespCode} {response.RespDesc}",
                response.RespCode);
        }

        RequireSignedSuccess(signed, response.RespCode, "Payment Token");

        if (string.IsNullOrEmpty(response.PaymentToken))
        {
            throw new PaymentGatewayException(
                $"2C2P returned no payment token for invoice {request.InvoiceNo}.", response.RespCode);
        }

        return response;
    }

    private object SignedEnvelope<T>(T payload)
        => new { payload = _jwt.Sign(JsonSerializer.Serialize(payload, JsonOptions)) };

    /// <summary>
    /// Posts JSON and reads the response in either shape 2C2P uses: a signed
    /// { "payload": "&lt;JWT&gt;" } (verified, then decoded) or plain JSON, which is how
    /// Do Payment answers and how some errors come back. Returns whether it was signed.
    /// </summary>
    private async Task<(T Response, bool Signed)> PostAsync<T>(
        string path,
        object body,
        string operation,
        CancellationToken ct)
        where T : class, ITwoC2PResponse
    {
        using var content = new StringContent(
            JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");

        HttpResponseMessage httpResponse;
        try
        {
            httpResponse = await _http.PostAsync(path, content, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new PaymentGatewayException($"2C2P {operation} request failed.", innerException: ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            // HttpClient.Timeout expired. A cancellation from the caller is rethrown as is.
            throw new PaymentGatewayException($"2C2P {operation} request timed out.", innerException: ex);
        }

        using (httpResponse)
        {
            var text = await httpResponse.Content.ReadAsStringAsync(ct);

            string json;
            bool signed;
            try
            {
                (json, signed) = await UnwrapAsync(text);
            }
            catch (Exception ex) when (ex is JsonException or SecurityTokenException)
            {
                var reason = ex is SecurityTokenException ? "failed signature verification" : "is not JSON";
                throw new PaymentGatewayException(
                    $"2C2P {operation} response (HTTP {(int)httpResponse.StatusCode}) {reason}.",
                    innerException: ex);
            }

            T? response;
            try
            {
                response = JsonSerializer.Deserialize<T>(json, JsonOptions);
            }
            catch (JsonException ex)
            {
                throw new PaymentGatewayException(
                    $"2C2P {operation} response has an unexpected shape.", innerException: ex);
            }

            if (response is null
                || (string.IsNullOrEmpty(response.RespCode) && !httpResponse.IsSuccessStatusCode))
            {
                // Not a 2C2P answer at all, e.g. a 404 from a wrong API version in BaseUrl.
                throw new PaymentGatewayException(
                    $"2C2P {operation} returned HTTP {(int)httpResponse.StatusCode} without a response code.");
            }

            return (response, signed);
        }
    }

    private async Task<(string Json, bool Signed)> UnwrapAsync(string text)
    {
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;

        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("payload", out var payload)
            && payload.ValueKind == JsonValueKind.String
            && payload.GetString() is { Length: > 0 } token)
        {
            return (await _jwt.VerifyAsync(token), true);
        }

        return (text, false);
    }

    /// <summary>
    /// Payment Token and Payment Inquiry sign their answers. An error may come back as plain
    /// JSON, but a success that is not signed cannot be trusted.
    /// </summary>
    private static void RequireSignedSuccess(bool signed, string? respCode, string operation)
    {
        if (!signed && respCode == TwoC2PResponseCodes.Success)
        {
            throw new PaymentGatewayException(
                $"2C2P {operation} returned a success without a signed payload.", respCode);
        }
    }

    private static PaymentResult ToPaymentResult(PaymentResultPayload payload) => new()
    {
        Outcome = TwoC2PResponseCodes.ToOutcome(payload.RespCode),
        InvoiceNo = NullIfEmpty(payload.InvoiceNo),
        RawCode = payload.RespCode,
        Description = payload.RespDesc,
        PaymentChannel = NullIfEmpty(payload.ChannelCode),
        MaskedAccountNumber = NullIfEmpty(payload.AccountNo),
        ProviderReference = NullIfEmpty(payload.TranRef) ?? NullIfEmpty(payload.ReferenceNo),
        CustomerToken = NullIfEmpty(payload.CustomerToken),
        Amount = payload.Amount,
        Currency = NullIfEmpty(payload.CurrencyCode),
    };

    /// <summary>The charge may or may not have happened; the backend notification will tell.</summary>
    private static PaymentResult UnknownOutcome(string invoiceNo, string? rawCode) => new()
    {
        Outcome = PaymentOutcome.Pending,
        InvoiceNo = invoiceNo,
        RawCode = rawCode,
        Description = "The payment result is not known yet; waiting for the 2C2P notification.",
    };

    private static SavedCardResult SavedCardResultOf(string paymentToken, PaymentResult payment)
        => new() { PaymentToken = paymentToken, Payment = payment };

    // 2C2P sends "" rather than leaving a field out.
    private static string? NullIfEmpty(string? value)
        => string.IsNullOrEmpty(value) ? null : value;
}
