namespace PaymentIntegration.Application.DTOs;

public class CustomerDto
{
    /// <example>1</example>
    public int Id { get; set; }

    /// <example>Alice</example>
    public string FirstName { get; set; } = string.Empty;

    /// <example>Tan</example>
    public string LastName { get; set; } = string.Empty;

    /// <example>alice.tan@example.com</example>
    public string Email { get; set; } = string.Empty;
}
