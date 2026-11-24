using Microsoft.Extensions.Options;

namespace PaymentIntegration.Infrastructure.Payments;

/// <summary>The "TwoC2P" configuration section.</summary>
public sealed class TwoC2POptions
{
    public const string SectionName = "TwoC2P";

    public string MerchantId { get; set; } = string.Empty;

    /// <summary>The merchant secret key. It signs and verifies every JWT (HS256).</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// The PGW API root including its version, e.g. https://sandbox-pgw.2c2p.com/payment/4.3/.
    /// The endpoint names (paymentToken, payment, paymentInquiry) are appended to it.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Where 2C2P sends the customer's browser after the payment page.</summary>
    public string FrontendReturnUrl { get; set; } = string.Empty;

    /// <summary>Where 2C2P posts the server-to-server payment notification.</summary>
    public string BackendReturnUrl { get; set; } = string.Empty;
}

/// <summary>
/// Runs when the gateway is first created (the first checkout), not at startup, so the API
/// still serves its other endpoints while the 2C2P settings are missing.
/// </summary>
internal sealed class TwoC2POptionsValidator : IValidateOptions<TwoC2POptions>
{
    // HS256 needs a key of at least 256 bits. 2C2P secret keys are 64 characters.
    private const int MinimumSecretKeyLength = 32;

    public ValidateOptionsResult Validate(string? name, TwoC2POptions options)
    {
        var errors = new List<string>();
        var section = TwoC2POptions.SectionName;

        if (string.IsNullOrWhiteSpace(options.MerchantId))
        {
            errors.Add($"{section}:MerchantId is not configured.");
        }

        if (options.SecretKey.Length < MinimumSecretKeyLength)
        {
            errors.Add($"{section}:SecretKey must be the 2C2P merchant secret key (at least {MinimumSecretKeyLength} characters).");
        }

        AddIfNotAbsoluteUrl(errors, $"{section}:BaseUrl", options.BaseUrl);
        AddIfNotAbsoluteUrl(errors, $"{section}:FrontendReturnUrl", options.FrontendReturnUrl);
        AddIfNotAbsoluteUrl(errors, $"{section}:BackendReturnUrl", options.BackendReturnUrl);

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    private static void AddIfNotAbsoluteUrl(List<string> errors, string key, string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            errors.Add($"{key} must be an absolute http(s) URL.");
        }
    }
}
