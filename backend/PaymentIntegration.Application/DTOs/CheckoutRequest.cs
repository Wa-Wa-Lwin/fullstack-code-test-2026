namespace PaymentIntegration.Application.DTOs;

public class CheckoutRequest
{
    public int CustomerId { get; set; }

    public int ProductId { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;
}