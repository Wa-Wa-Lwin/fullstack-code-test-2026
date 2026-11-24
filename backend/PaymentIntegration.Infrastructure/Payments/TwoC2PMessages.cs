using System.Text.Json.Serialization;

namespace PaymentIntegration.Infrastructure.Payments;

/// <summary>The fields every 2C2P answer carries.</summary>
internal interface ITwoC2PResponse
{
    string? RespCode { get; }

    string? RespDesc { get; }
}

// The 2C2P PGW v4.3 wire formats. Every field name is spelled out because several do not
// follow camelCase (merchantID, request3DS). Null fields are left out of requests.

/// <summary>Payment Token request, sent signed as { "payload": "&lt;JWT&gt;" }.</summary>
internal sealed record PaymentTokenRequest
{
    [JsonPropertyName("merchantID")]
    public required string MerchantId { get; init; }

    [JsonPropertyName("invoiceNo")]
    public required string InvoiceNo { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }

    [JsonPropertyName("amount")]
    public required decimal Amount { get; init; }

    [JsonPropertyName("currencyCode")]
    public required string CurrencyCode { get; init; }

    [JsonPropertyName("frontendReturnUrl")]
    public required string FrontendReturnUrl { get; init; }

    [JsonPropertyName("backendReturnUrl")]
    public required string BackendReturnUrl { get; init; }

    [JsonPropertyName("paymentChannel")]
    public string[]? PaymentChannel { get; init; }

    [JsonPropertyName("tokenize")]
    public bool? Tokenize { get; init; }

    [JsonPropertyName("customerToken")]
    public string[]? CustomerToken { get; init; }

    [JsonPropertyName("customerTokenOnly")]
    public bool? CustomerTokenOnly { get; init; }

    [JsonPropertyName("request3DS")]
    public string? Request3DS { get; init; }
}

/// <summary>Payment Token response (decoded from its payload, or plain JSON for some errors).</summary>
internal sealed record PaymentTokenResponse : ITwoC2PResponse
{
    [JsonPropertyName("webPaymentUrl")]
    public string? WebPaymentUrl { get; init; }

    [JsonPropertyName("paymentToken")]
    public string? PaymentToken { get; init; }

    [JsonPropertyName("respCode")]
    public string? RespCode { get; init; }

    [JsonPropertyName("respDesc")]
    public string? RespDesc { get; init; }
}

/// <summary>Do Payment request. Plain JSON, not a JWT.</summary>
internal sealed record DoPaymentRequest
{
    [JsonPropertyName("paymentToken")]
    public required string PaymentToken { get; init; }

    [JsonPropertyName("payment")]
    public required DoPaymentMethod Payment { get; init; }
}

internal sealed record DoPaymentMethod
{
    [JsonPropertyName("code")]
    public required DoPaymentCode Code { get; init; }

    [JsonPropertyName("data")]
    public required DoPaymentData Data { get; init; }
}

internal sealed record DoPaymentCode
{
    [JsonPropertyName("channelCode")]
    public required string ChannelCode { get; init; }
}

internal sealed record DoPaymentData
{
    [JsonPropertyName("customerToken")]
    public required string CustomerToken { get; init; }
}

/// <summary>Do Payment response. For 1001, <see cref="Data"/> holds the 3-D Secure URL.</summary>
internal sealed record DoPaymentResponse : ITwoC2PResponse
{
    [JsonPropertyName("channelCode")]
    public string? ChannelCode { get; init; }

    [JsonPropertyName("invoiceNo")]
    public string? InvoiceNo { get; init; }

    [JsonPropertyName("data")]
    public string? Data { get; init; }

    [JsonPropertyName("respCode")]
    public string? RespCode { get; init; }

    [JsonPropertyName("respDesc")]
    public string? RespDesc { get; init; }
}

/// <summary>Payment Inquiry request, sent signed as { "payload": "&lt;JWT&gt;" }.</summary>
internal sealed record PaymentInquiryRequest
{
    [JsonPropertyName("merchantID")]
    public required string MerchantId { get; init; }

    [JsonPropertyName("invoiceNo")]
    public required string InvoiceNo { get; init; }
}

/// <summary>
/// A payment result: the decoded Payment Inquiry response, and also the decoded backend
/// notification, which carries the same fields.
/// </summary>
internal sealed record PaymentResultPayload : ITwoC2PResponse
{
    [JsonPropertyName("merchantID")]
    public string? MerchantId { get; init; }

    [JsonPropertyName("invoiceNo")]
    public string? InvoiceNo { get; init; }

    [JsonPropertyName("amount")]
    public decimal? Amount { get; init; }

    [JsonPropertyName("currencyCode")]
    public string? CurrencyCode { get; init; }

    [JsonPropertyName("channelCode")]
    public string? ChannelCode { get; init; }

    /// <summary>The masked card number, e.g. 411111XXXXXX1111.</summary>
    [JsonPropertyName("accountNo")]
    public string? AccountNo { get; init; }

    /// <summary>The 2C2P transaction reference.</summary>
    [JsonPropertyName("tranRef")]
    public string? TranRef { get; init; }

    /// <summary>The card host's reference number.</summary>
    [JsonPropertyName("referenceNo")]
    public string? ReferenceNo { get; init; }

    /// <summary>Returned when the card was stored (tokenize: true).</summary>
    [JsonPropertyName("customerToken")]
    public string? CustomerToken { get; init; }

    [JsonPropertyName("respCode")]
    public string? RespCode { get; init; }

    [JsonPropertyName("respDesc")]
    public string? RespDesc { get; init; }
}
