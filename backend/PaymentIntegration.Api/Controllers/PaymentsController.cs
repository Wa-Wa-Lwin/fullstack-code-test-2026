using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PaymentIntegration.Api.Models;
using PaymentIntegration.Application.Services;

namespace PaymentIntegration.Api.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController(
    PaymentNotificationService notifications,
    TransactionService transactions,
    IOptions<FrontendOptions> frontend) : ControllerBase
{
    /// <summary>The 2C2P backend notification (backendReturnUrl). Called by 2C2P, not by the frontend.</summary>
    /// <remarks>
    /// The payload is a JWT signed with the merchant secret key. It is verified before
    /// anything else, and a forged one is rejected without touching the database. Sending
    /// the same notification again changes nothing.
    /// </remarks>
    /// <param name="request">The signed notification.</param>
    /// <param name="ct">Cancelled when the caller disconnects.</param>
    /// <response code="200">
    /// The notification was verified and applied. A repeat, or a notification for an unknown
    /// invoice (logged), is acknowledged and changes nothing.
    /// </response>
    /// <response code="400">The payload is missing or its signature is invalid.</response>
    [HttpPost("2c2p/notification")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Notification([FromBody] PaymentNotificationRequest request, CancellationToken ct)
    {
        await notifications.HandleAsync(request.Payload, ct);
        return Ok();
    }

    /// <summary>
    /// Where 2C2P sends the customer's browser back (frontendReturnUrl), after the payment
    /// page or after 3-D Secure for a saved card. Redirects to the frontend result page.
    /// </summary>
    /// <remarks>
    /// Accepts GET and POST, because a payment page may return the browser with a form POST,
    /// which a Next.js page cannot receive. Whatever the browser brings is not trusted: if
    /// the transaction is still Pending, a Payment Inquiry asks 2C2P for the real result.
    /// </remarks>
    /// <param name="invoiceNo" example="INV20261124120000AB12CD">The invoice number, put into the return URL at checkout.</param>
    /// <param name="ct">Cancelled when the browser disconnects.</param>
    /// <response code="303">Redirect to the frontend result page for this invoice.</response>
    /// <response code="404">No transaction has this invoice number.</response>
    [AcceptVerbs("GET", "POST")]
    [Route("2c2p/return/{invoiceNo}")]
    [ProducesResponseType(StatusCodes.Status303SeeOther)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Return(string invoiceNo, CancellationToken ct)
    {
        await transactions.RefreshIfPendingAsync(invoiceNo, ct);

        // 303 makes the browser follow with a GET even when 2C2P returned it with a POST.
        Response.Headers.Location = $"{frontend.Value.PaymentResultUrl}?invoiceNo={Uri.EscapeDataString(invoiceNo)}";
        return StatusCode(StatusCodes.Status303SeeOther);
    }
}
