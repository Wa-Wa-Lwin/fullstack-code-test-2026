using PaymentIntegration.Domain.Enums;

namespace PaymentIntegration.Domain.Entities;

public class Product
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public string Currency { get; set; } = "SGD";

    public PaymentType PaymentType { get; set; }
}
