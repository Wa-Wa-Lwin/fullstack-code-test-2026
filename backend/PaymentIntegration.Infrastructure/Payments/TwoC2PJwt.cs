using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace PaymentIntegration.Infrastructure.Payments;

/// <summary>
/// Signs request payloads and verifies response payloads the way 2C2P PGW expects:
/// a compact JWT signed with HMAC SHA-256 using the merchant secret key.
/// </summary>
internal sealed class TwoC2PJwt
{
    private readonly JsonWebTokenHandler _handler = new()
    {
        // 2C2P payloads are plain payment fields with no exp, iat or nbf. CreateToken(string)
        // signs the JSON as given; this setting also keeps the SecurityTokenDescriptor
        // overloads from adding those claims if one is ever used. A unit test checks the output.
        SetDefaultTimesOnTokenCreation = false,
    };

    private readonly SigningCredentials _signingCredentials;
    private readonly TokenValidationParameters _validationParameters;

    public TwoC2PJwt(string secretKey)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

        _signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        _validationParameters = new TokenValidationParameters
        {
            IssuerSigningKey = key,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            RequireSignedTokens = true,

            // 2C2P payloads carry no exp, iss or aud claims, so only the signature is checked.
            ValidateLifetime = false,
            RequireExpirationTime = false,
            ValidateIssuer = false,
            ValidateAudience = false,
        };
    }

    /// <summary>Signs a JSON payload and returns the compact JWT.</summary>
    public string Sign(string payloadJson)
        => _handler.CreateToken(payloadJson, _signingCredentials);

    /// <summary>Verifies the signature and returns the payload JSON.</summary>
    /// <exception cref="SecurityTokenException">The token is malformed or its signature is invalid.</exception>
    public async Task<string> VerifyAsync(string token)
    {
        var result = await _handler.ValidateTokenAsync(token, _validationParameters);

        if (!result.IsValid || result.SecurityToken is not JsonWebToken jwt)
        {
            throw new SecurityTokenException("The 2C2P JWT could not be verified.", result.Exception);
        }

        return Base64UrlEncoder.Decode(jwt.EncodedPayload);
    }
}
