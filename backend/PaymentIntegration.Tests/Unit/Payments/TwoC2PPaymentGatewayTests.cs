using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PaymentIntegration.Application.DTOs.Payments;
using PaymentIntegration.Application.Exceptions;
using PaymentIntegration.Infrastructure.Payments;
using PaymentIntegration.Tests.TestSupport;

namespace PaymentIntegration.Tests.Unit.Payments;

public class TwoC2PPaymentGatewayTests
{
    private const string SecretKey = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";

    private static readonly TwoC2POptions GatewayOptions = new()
    {
        MerchantId = "JT01",
        SecretKey = SecretKey,
        BaseUrl = "https://pgw.test/payment/4.3/",
        FrontendReturnUrl = "https://shop.test/payment/result",
        BackendReturnUrl = "https://api.test/api/payments/2c2p/notification",
    };

    private readonly StubHttpMessageHandler _http = new();
    private readonly TwoC2PJwt _jwt = new(SecretKey);
    private readonly TwoC2PPaymentGateway _gateway;

    public TwoC2PPaymentGatewayTests()
    {
        var client = new HttpClient(_http) { BaseAddress = new Uri(GatewayOptions.BaseUrl) };
        _gateway = new TwoC2PPaymentGateway(
            client, Options.Create(GatewayOptions), NullLogger<TwoC2PPaymentGateway>.Instance);
    }

    // ---- Payment Token (hosted payment page) ----

    [Fact]
    public async Task Hosted_payment_posts_a_signed_payment_token_request_and_returns_the_page()
    {
        _http.RespondJson("paymentToken", Signed(TokenSuccess));

        var result = await _gateway.CreateHostedPaymentAsync(HostedRequest(saveCard: false), default);

        Assert.Equal("https://pay.test/page", result.PaymentUrl);
        Assert.Equal("PT-1", result.PaymentToken);

        var request = Assert.Single(_http.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/payment/4.3/paymentToken", request.Path);
        Assert.Equal("application/json", request.MediaType);

        var payload = await SignedRequestPayload(request);
        Assert.Equal("JT01", payload.GetProperty("merchantID").GetString());
        Assert.Equal("INV0001", payload.GetProperty("invoiceNo").GetString());
        Assert.Equal("Square One Album", payload.GetProperty("description").GetString());
        Assert.Equal(29.90m, payload.GetProperty("amount").GetDecimal());
        Assert.Equal("SGD", payload.GetProperty("currencyCode").GetString());
        Assert.Equal(GatewayOptions.FrontendReturnUrl, payload.GetProperty("frontendReturnUrl").GetString());
        Assert.Equal(GatewayOptions.BackendReturnUrl, payload.GetProperty("backendReturnUrl").GetString());

        // A one-time payment offers every channel and stores nothing.
        Assert.False(payload.TryGetProperty("paymentChannel", out _));
        Assert.False(payload.TryGetProperty("tokenize", out _));
        Assert.False(payload.TryGetProperty("customerToken", out _));
    }

    [Fact]
    public async Task Hosted_payment_that_saves_the_card_asks_for_card_only_and_tokenize()
    {
        _http.RespondJson("paymentToken", Signed(TokenSuccess));

        await _gateway.CreateHostedPaymentAsync(HostedRequest(saveCard: true), default);

        var payload = await SignedRequestPayload(_http.Requests[0]);
        Assert.Equal(["CC"], StringArray(payload.GetProperty("paymentChannel")));
        Assert.True(payload.GetProperty("tokenize").GetBoolean());
        Assert.False(payload.TryGetProperty("customerTokenOnly", out _));
    }

    [Fact]
    public async Task A_plain_json_error_without_payload_is_read_and_thrown_with_its_code()
    {
        _http.RespondJson("paymentToken", """{"respCode":"9042","respDesc":"Hash value mismatch"}""");

        var ex = await Assert.ThrowsAsync<PaymentGatewayException>(
            () => _gateway.CreateHostedPaymentAsync(HostedRequest(saveCard: false), default));

        Assert.Equal("9042", ex.ProviderCode);
        Assert.Equal("Hash value mismatch", ex.ProviderDescription);
    }

    [Fact]
    public async Task A_signed_error_is_thrown_with_its_code()
    {
        _http.RespondJson("paymentToken", Signed(new { respCode = "9015", respDesc = "Existing Invoice Number" }));

        var ex = await Assert.ThrowsAsync<PaymentGatewayException>(
            () => _gateway.CreateHostedPaymentAsync(HostedRequest(saveCard: false), default));

        Assert.Equal("9015", ex.ProviderCode);
    }

    [Fact]
    public async Task A_response_signed_with_another_key_is_rejected()
    {
        var forged = new TwoC2PJwt("FEDCBA9876543210FEDCBA9876543210FEDCBA9876543210FEDCBA9876543210");
        _http.RespondJson("paymentToken", JsonSerializer.Serialize(new { payload = forged.Sign(JsonSerializer.Serialize(TokenSuccess)) }));

        await Assert.ThrowsAsync<PaymentGatewayException>(
            () => _gateway.CreateHostedPaymentAsync(HostedRequest(saveCard: false), default));
    }

    [Fact]
    public async Task An_unsigned_success_is_not_trusted()
    {
        _http.RespondJson("paymentToken", JsonSerializer.Serialize(TokenSuccess));

        await Assert.ThrowsAsync<PaymentGatewayException>(
            () => _gateway.CreateHostedPaymentAsync(HostedRequest(saveCard: false), default));
    }

    [Fact]
    public async Task A_non_2C2P_http_error_reports_the_status_code()
    {
        _http.RespondJson("paymentToken", "<html>Not Found</html>", HttpStatusCode.NotFound);

        var ex = await Assert.ThrowsAsync<PaymentGatewayException>(
            () => _gateway.CreateHostedPaymentAsync(HostedRequest(saveCard: false), default));

        Assert.Contains("404", ex.Message);
    }

    [Fact]
    public async Task The_invoice_number_is_put_into_the_frontend_return_url()
    {
        var options = Options.Create(new TwoC2POptions
        {
            MerchantId = "JT01",
            SecretKey = SecretKey,
            BaseUrl = GatewayOptions.BaseUrl,
            FrontendReturnUrl = "https://api.test/api/payments/2c2p/return/{invoiceNo}",
            BackendReturnUrl = GatewayOptions.BackendReturnUrl,
        });
        var gateway = new TwoC2PPaymentGateway(new HttpClient(_http), options, NullLogger<TwoC2PPaymentGateway>.Instance);
        _http.RespondJson("paymentToken", Signed(TokenSuccess));

        await gateway.CreateHostedPaymentAsync(HostedRequest(saveCard: false), default);

        var payload = await SignedRequestPayload(_http.Requests[0]);
        Assert.Equal(
            "https://api.test/api/payments/2c2p/return/INV0001",
            payload.GetProperty("frontendReturnUrl").GetString());
    }

    [Fact]
    public async Task Missing_settings_fail_the_call_with_a_gateway_exception_not_the_construction()
    {
        var services = new ServiceCollection();
        services.AddOptions<TwoC2POptions>();
        services.AddSingleton<IValidateOptions<TwoC2POptions>, TwoC2POptionsValidator>();
        var options = services
            .BuildServiceProvider()
            .GetRequiredService<IOptions<TwoC2POptions>>();

        // Creating the gateway must not throw: services that only read the database use it too.
        var gateway = new TwoC2PPaymentGateway(new HttpClient(_http), options, NullLogger<TwoC2PPaymentGateway>.Instance);

        var ex = await Assert.ThrowsAsync<PaymentGatewayException>(
            () => gateway.CreateHostedPaymentAsync(HostedRequest(saveCard: false), default));
        Assert.Contains("TwoC2P:SecretKey", ex.Message);
        Assert.Empty(_http.Requests);
    }

    // ---- Saved card: Payment Token, Do Payment, Payment Inquiry ----

    [Fact]
    public async Task Saved_card_sends_the_customer_token_and_returns_the_3ds_url_on_1001()
    {
        _http.RespondJson("paymentToken", Signed(TokenSuccess))
             .RespondJson("payment", """{"channelCode":"CC","respCode":"1001","respDesc":"Redirect to authenticate ACS bank page.","data":"https://acs.test/3ds"}""");

        var result = await _gateway.ChargeSavedCardAsync(SavedCardRequest(), default);

        Assert.Equal(PaymentOutcome.Pending, result.Payment.Outcome);
        Assert.Equal("1001", result.Payment.RawCode);
        Assert.Equal("https://acs.test/3ds", result.RedirectUrl);
        Assert.Equal("PT-1", result.PaymentToken);

        var token = await SignedRequestPayload(_http.Requests[0]);
        Assert.Equal(["CC"], StringArray(token.GetProperty("paymentChannel")));
        Assert.Equal(["CT-123"], StringArray(token.GetProperty("customerToken")));
        Assert.True(token.GetProperty("customerTokenOnly").GetBoolean());
        Assert.Equal("N", token.GetProperty("request3DS").GetString());
        Assert.False(token.TryGetProperty("tokenize", out _));

        // Do Payment is plain JSON, not a signed payload.
        var doPayment = _http.Requests[1];
        Assert.Equal("/payment/4.3/payment", doPayment.Path);
        var body = JsonDocument.Parse(doPayment.Body).RootElement;
        Assert.False(body.TryGetProperty("payload", out _));
        Assert.Equal("PT-1", body.GetProperty("paymentToken").GetString());
        Assert.Equal("CC", body.GetProperty("payment").GetProperty("code").GetProperty("channelCode").GetString());
        Assert.Equal("CT-123", body.GetProperty("payment").GetProperty("data").GetProperty("customerToken").GetString());

        Assert.Equal(0, _http.CallsTo("paymentInquiry"));
    }

    [Fact]
    public async Task Saved_card_runs_payment_inquiry_after_2000_and_maps_its_result()
    {
        _http.RespondJson("paymentToken", Signed(TokenSuccess))
             .RespondJson("payment", """{"channelCode":"CC","invoiceNo":"INV0002","respCode":"2000","respDesc":"Transaction is completed, please do payment inquiry request for full payment information."}""")
             .RespondJson("paymentInquiry", Signed(InquirySuccess));

        var result = await _gateway.ChargeSavedCardAsync(SavedCardRequest(), default);

        Assert.Equal(PaymentOutcome.Succeeded, result.Payment.Outcome);
        Assert.Equal("0000", result.Payment.RawCode);
        Assert.Equal("INV0002", result.Payment.InvoiceNo);
        Assert.Equal("411111XXXXXX1111", result.Payment.MaskedAccountNumber);
        Assert.Equal("TR-9", result.Payment.ProviderReference);
        Assert.Equal("CC", result.Payment.PaymentChannel);
        Assert.Equal(99.00m, result.Payment.Amount);
        Assert.Null(result.RedirectUrl);

        var inquiry = await SignedRequestPayload(_http.Requests[2]);
        Assert.Equal("/payment/4.3/paymentInquiry", _http.Requests[2].Path);
        Assert.Equal("JT01", inquiry.GetProperty("merchantID").GetString());
        Assert.Equal("INV0002", inquiry.GetProperty("invoiceNo").GetString());
    }

    [Fact]
    public async Task Saved_card_with_any_other_do_payment_code_fails_without_inquiry()
    {
        _http.RespondJson("paymentToken", Signed(TokenSuccess))
             .RespondJson("payment", """{"channelCode":"CC","respCode":"4005","respDesc":"Do not honor"}""");

        var result = await _gateway.ChargeSavedCardAsync(SavedCardRequest(), default);

        Assert.Equal(PaymentOutcome.Failed, result.Payment.Outcome);
        Assert.Equal("4005", result.Payment.RawCode);
        Assert.Equal("Do not honor", result.Payment.Description);
        Assert.Equal(0, _http.CallsTo("paymentInquiry"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_failed_do_payment_call_is_not_retried_and_stays_pending(bool timeout)
    {
        Exception failure = timeout
            ? new TaskCanceledException("HttpClient.Timeout elapsed", new TimeoutException())
            : new HttpRequestException("Connection reset");

        _http.RespondJson("paymentToken", Signed(TokenSuccess))
             .Throw("payment", failure);

        var result = await _gateway.ChargeSavedCardAsync(SavedCardRequest(), default);

        // The charge may have gone through, so it is neither retried nor marked Failed.
        Assert.Equal(PaymentOutcome.Pending, result.Payment.Outcome);
        Assert.Equal(1, _http.CallsTo("payment"));
        Assert.Equal(0, _http.CallsTo("paymentInquiry"));
    }

    [Fact]
    public async Task A_failed_inquiry_after_2000_stays_pending()
    {
        _http.RespondJson("paymentToken", Signed(TokenSuccess))
             .RespondJson("payment", """{"respCode":"2000","respDesc":"Transaction is completed"}""")
             .RespondJson("paymentInquiry", """{"respCode":"9999","respDesc":"Request to merchant backend has failed"}""");

        var result = await _gateway.ChargeSavedCardAsync(SavedCardRequest(), default);

        Assert.Equal(PaymentOutcome.Pending, result.Payment.Outcome);
        Assert.Equal(1, _http.CallsTo("payment"));
    }

    [Fact]
    public async Task A_rejected_saved_card_payment_token_throws_before_any_charge()
    {
        _http.RespondJson("paymentToken", """{"respCode":"9202","respDesc":"Invalid CustomerToken"}""");

        var ex = await Assert.ThrowsAsync<PaymentGatewayException>(
            () => _gateway.ChargeSavedCardAsync(SavedCardRequest(), default));

        Assert.Equal("9202", ex.ProviderCode);
        Assert.Equal(0, _http.CallsTo("payment"));
    }

    // ---- Payment Inquiry ----

    [Fact]
    public async Task An_inquiry_system_error_throws_instead_of_reporting_a_failed_payment()
    {
        _http.RespondJson("paymentInquiry", Signed(new { invoiceNo = "INV0002", respCode = "0999", respDesc = "System error" }));

        var ex = await Assert.ThrowsAsync<PaymentGatewayException>(
            () => _gateway.InquireAsync("INV0002", default));

        Assert.Equal("0999", ex.ProviderCode);
    }

    [Theory]
    [InlineData("0000", PaymentOutcome.Succeeded)]
    [InlineData("0001", PaymentOutcome.Pending)]
    [InlineData("2001", PaymentOutcome.Pending)]
    [InlineData("0003", PaymentOutcome.Failed)]
    [InlineData("4051", PaymentOutcome.Failed)]
    [InlineData("9020", PaymentOutcome.Failed)]
    public async Task Inquiry_codes_map_to_outcomes(string code, PaymentOutcome expected)
    {
        _http.RespondJson("paymentInquiry", Signed(new { invoiceNo = "INV0002", respCode = code, respDesc = "x" }));

        var result = await _gateway.InquireAsync("INV0002", default);

        Assert.Equal(expected, result.Outcome);
        Assert.Equal(code, result.RawCode);
    }

    // ---- Backend notification ----

    [Fact]
    public async Task A_notification_is_verified_and_mapped()
    {
        var payload = _jwt.Sign(JsonSerializer.Serialize(InquirySuccess));

        var result = await _gateway.ParseNotificationAsync(payload, default);

        Assert.Equal(PaymentOutcome.Succeeded, result.Outcome);
        Assert.Equal("INV0002", result.InvoiceNo);
        Assert.Equal("CT-123", result.CustomerToken);
        Assert.Equal("SGD", result.Currency);
    }

    [Fact]
    public async Task A_notification_with_a_bad_signature_is_rejected()
    {
        var forged = new TwoC2PJwt("FEDCBA9876543210FEDCBA9876543210FEDCBA9876543210FEDCBA9876543210");
        var payload = forged.Sign(JsonSerializer.Serialize(InquirySuccess));

        await Assert.ThrowsAsync<ValidationException>(() => _gateway.ParseNotificationAsync(payload, default));
    }

    [Fact]
    public async Task A_notification_that_is_not_a_jwt_is_rejected()
    {
        await Assert.ThrowsAsync<ValidationException>(() => _gateway.ParseNotificationAsync("not-a-jwt", default));
    }

    // ---- helpers ----

    private static readonly object TokenSuccess = new
    {
        webPaymentUrl = "https://pay.test/page",
        paymentToken = "PT-1",
        respCode = "0000",
        respDesc = "Success",
    };

    private static readonly object InquirySuccess = new
    {
        merchantID = "JT01",
        invoiceNo = "INV0002",
        amount = 99.00m,
        currencyCode = "SGD",
        channelCode = "CC",
        accountNo = "411111XXXXXX1111",
        tranRef = "TR-9",
        referenceNo = "RN-9",
        customerToken = "CT-123",
        cardToken = "",
        respCode = "0000",
        respDesc = "Success",
    };

    private static HostedPaymentRequest HostedRequest(bool saveCard) => new()
    {
        InvoiceNo = "INV0001",
        Amount = 29.90m,
        Currency = "SGD",
        Description = "Square One Album",
        SaveCard = saveCard,
    };

    private static SavedCardRequest SavedCardRequest() => new()
    {
        InvoiceNo = "INV0002",
        CustomerToken = "CT-123",
        Amount = 99.00m,
        Currency = "SGD",
        Description = "Yearly Photocard",
    };

    private string Signed(object payload)
        => JsonSerializer.Serialize(new { payload = _jwt.Sign(JsonSerializer.Serialize(payload)) });

    private async Task<JsonElement> SignedRequestPayload(RecordedRequest request)
    {
        var token = JsonDocument.Parse(request.Body).RootElement.GetProperty("payload").GetString()!;
        return JsonDocument.Parse(await _jwt.VerifyAsync(token)).RootElement.Clone();
    }

    private static string[] StringArray(JsonElement array)
        => array.EnumerateArray().Select(e => e.GetString()!).ToArray();
}
