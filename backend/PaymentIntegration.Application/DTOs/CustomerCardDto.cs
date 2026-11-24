namespace PaymentIntegration.Application.DTOs;

/// <summary>
/// A saved card as the client sees it. The 2C2P customer token is left out on purpose:
/// only the server uses it, to charge the card.
/// </summary>
public class CustomerCardDto
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public string MaskedCardNo { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
