namespace PaymentIntegration.Application.DTOs;

public class CheckoutRequest
{
    public int CustomerId { get; set; }

    public int ProductId { get; set; }

    public int? CustomerCardId { get; set; }
}