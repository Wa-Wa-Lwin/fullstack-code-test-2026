using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using PaymentIntegration.Infrastructure.Payments;

namespace PaymentIntegration.Tests.Unit.Payments;

public class TwoC2PJwtTests
{
    private const string SecretKey = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";

    private const string PaymentTokenPayload =
        """{"merchantID":"JT01","invoiceNo":"INV0001","description":"Monthly Live Show","amount":9.99,"currencyCode":"SGD","paymentChannel":["CC"],"tokenize":true}""";

    private readonly TwoC2PJwt _jwt = new(SecretKey);

    [Fact]
    public async Task A_signed_token_verifies_and_returns_the_same_payload()
    {
        var token = _jwt.Sign(PaymentTokenPayload);

        var payload = JsonDocument.Parse(await _jwt.VerifyAsync(token)).RootElement;

        Assert.Equal("JT01", payload.GetProperty("merchantID").GetString());
        Assert.Equal("INV0001", payload.GetProperty("invoiceNo").GetString());
        Assert.Equal(9.99m, payload.GetProperty("amount").GetDecimal());
        Assert.Equal("CC", payload.GetProperty("paymentChannel")[0].GetString());
        Assert.True(payload.GetProperty("tokenize").GetBoolean());
    }

    [Fact]
    public void A_signed_token_is_HS256_and_carries_no_time_claims()
    {
        var parts = _jwt.Sign(PaymentTokenPayload).Split('.');

        var header = JsonDocument.Parse(Base64UrlEncoder.Decode(parts[0])).RootElement;
        var payload = JsonDocument.Parse(Base64UrlEncoder.Decode(parts[1])).RootElement;

        Assert.Equal("HS256", header.GetProperty("alg").GetString());
        Assert.False(payload.TryGetProperty("exp", out _));
        Assert.False(payload.TryGetProperty("iat", out _));
        Assert.False(payload.TryGetProperty("nbf", out _));
    }

    [Fact]
    public void The_signature_is_plain_HMAC_SHA256_over_the_secret_key_bytes()
    {
        // What any other HS256 implementation (such as 2C2P's) computes for the same input.
        var parts = _jwt.Sign(PaymentTokenPayload).Split('.');

        var expected = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(SecretKey),
            Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"));

        Assert.Equal(Base64UrlEncoder.Encode(expected), parts[2]);
    }

    [Fact]
    public async Task A_token_signed_outside_the_library_verifies()
    {
        var token = HandMadeToken("""{"invoiceNo":"INV0001","respCode":"0000"}""", SecretKey);

        var payload = JsonDocument.Parse(await _jwt.VerifyAsync(token)).RootElement;

        Assert.Equal("0000", payload.GetProperty("respCode").GetString());
    }

    [Fact]
    public async Task Lifetime_is_not_validated()
    {
        // 2C2P payloads have no exp claim; even a long-expired one must not fail verification.
        var token = HandMadeToken("""{"invoiceNo":"INV0001","exp":1}""", SecretKey);

        var payload = await _jwt.VerifyAsync(token);

        Assert.Contains("INV0001", payload);
    }

    [Fact]
    public async Task A_token_signed_with_another_key_is_rejected()
    {
        var other = new TwoC2PJwt("FEDCBA9876543210FEDCBA9876543210FEDCBA9876543210FEDCBA9876543210");
        var token = other.Sign(PaymentTokenPayload);

        await Assert.ThrowsAsync<SecurityTokenException>(() => _jwt.VerifyAsync(token));
    }

    [Fact]
    public async Task A_tampered_payload_is_rejected()
    {
        var parts = _jwt.Sign(PaymentTokenPayload).Split('.');
        var tampered = Base64UrlEncoder.Encode(PaymentTokenPayload.Replace("9.99", "0.01"));

        await Assert.ThrowsAsync<SecurityTokenException>(
            () => _jwt.VerifyAsync($"{parts[0]}.{tampered}.{parts[2]}"));
    }

    [Fact]
    public async Task An_unsigned_token_is_rejected()
    {
        var header = Base64UrlEncoder.Encode("""{"alg":"none","typ":"JWT"}""");
        var payload = Base64UrlEncoder.Encode(PaymentTokenPayload);

        await Assert.ThrowsAsync<SecurityTokenException>(() => _jwt.VerifyAsync($"{header}.{payload}."));
    }

    private static string HandMadeToken(string payloadJson, string secretKey)
    {
        var header = Base64UrlEncoder.Encode("""{"alg":"HS256","typ":"JWT"}""");
        var payload = Base64UrlEncoder.Encode(payloadJson);
        var signature = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secretKey),
            Encoding.ASCII.GetBytes($"{header}.{payload}"));

        return $"{header}.{payload}.{Base64UrlEncoder.Encode(signature)}";
    }
}
