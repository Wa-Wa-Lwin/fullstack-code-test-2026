namespace PaymentIntegration.Application.DTOs.Payments;

public class SavedCardResult
{
    public PaymentResult Payment { get; set; } = new();

    public string? CustomerToken { get; set; }
}
