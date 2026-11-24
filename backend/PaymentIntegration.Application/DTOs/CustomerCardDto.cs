namespace PaymentIntegration.Application.DTOs;

/// <summary>
/// A saved card as the client sees it. The payment provider's customer token is left out on purpose:
/// only the server uses it, to charge the card.
/// </summary>
public class CustomerCardDto
{
    /// <summary>Send this as customerCardId to pay with the card.</summary>
    /// <example>1</example>
    public int Id { get; set; }

    /// <example>1</example>
    public int CustomerId { get; set; }

    /// <example>411111XXXXXX1111</example>
    public string MaskedCardNo { get; set; } = string.Empty;

    /// <example>2026-11-24T12:00:00Z</example>
    public DateTime CreatedAt { get; set; }
}
