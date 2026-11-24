namespace PaymentIntegration.Application.DTOs;

public class ProductDto
{
    /// <example>3</example>
    public int Id { get; set; }

    /// <example>Monthly Live Show</example>
    public string Name { get; set; } = string.Empty;

    /// <example>100 GB of cloud storage, billed monthly.</example>
    public string Description { get; set; } = string.Empty;

    /// <example>9.99</example>
    public decimal Price { get; set; }

    /// <example>SGD</example>
    public string Currency { get; set; } = string.Empty;

    /// <summary>OneTime, or Recurring (the card is saved on the hosted page).</summary>
    /// <example>Recurring</example>
    public string PaymentType { get; set; } = string.Empty;
}
