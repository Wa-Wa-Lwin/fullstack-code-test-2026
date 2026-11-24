namespace PaymentIntegration.Application.DTOs;

public class CustomerDto
{
    /// <example>1</example>
    public int Id { get; set; }

    /// <example>Lisa</example>
    public string FirstName { get; set; } = string.Empty;

    /// <example>La</example>
    public string LastName { get; set; } = string.Empty;

    /// <example>lisa.la@example.com</example>
    public string Email { get; set; } = string.Empty;
}
