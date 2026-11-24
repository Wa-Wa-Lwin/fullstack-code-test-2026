namespace PaymentIntegration.Api.Models;

/// <summary>The "Frontend" configuration section.</summary>
public class FrontendOptions
{
    public const string SectionName = "Frontend";

    /// <summary>The Next.js origin allowed by CORS.</summary>
    public string Origin { get; set; } = "http://localhost:3000";

    /// <summary>The result page the browser return redirects to, with ?invoiceNo=.</summary>
    public string PaymentResultUrl { get; set; } = "http://localhost:3000/payment/result";
}
