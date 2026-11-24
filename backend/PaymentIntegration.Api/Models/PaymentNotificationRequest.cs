namespace PaymentIntegration.Api.Models;

/// <summary>The body 2C2P posts to backendReturnUrl.</summary>
public class PaymentNotificationRequest
{
    /// <summary>A JWT (HS256, merchant secret key) carrying the payment result.</summary>
    /// <example>eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpbnZvaWNlTm8iOiJJTlYwMDAxIiwicmVzcENvZGUiOiIwMDAwIn0.c2lnbmF0dXJl</example>
    [System.ComponentModel.DataAnnotations.Required]
    public string? Payload { get; set; }
}
