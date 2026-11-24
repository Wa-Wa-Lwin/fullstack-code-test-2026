using System.ComponentModel.DataAnnotations;

namespace PaymentIntegration.Application.DTOs;

public class CheckoutRequest
{
    /// <example>1</example>
    [Range(1, int.MaxValue)]
    public int CustomerId { get; set; }

    /// <example>3</example>
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    /// <summary>
    /// A saved card of this customer to charge directly. Leave it out to pay on the provider's hosted
    /// payment page instead.
    /// </summary>
    /// <example>null</example>
    [Range(1, int.MaxValue)]
    public int? CustomerCardId { get; set; }
}
