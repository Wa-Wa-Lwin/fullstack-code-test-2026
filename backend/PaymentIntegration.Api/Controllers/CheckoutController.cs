using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using PaymentIntegration.Application.DTOs;
using PaymentIntegration.Application.Services;

namespace PaymentIntegration.Api.Controllers;

[ApiController]
[Route("api/checkout")]
[Produces("application/json")]
public class CheckoutController(CheckoutService checkout) : ControllerBase
{
    /// <summary>Starts a payment for a product.</summary>
    /// <remarks>
    /// Without a card, the response carries the 2C2P payment page URL to redirect to. With a
    /// saved card, the card is charged directly: the response is final, or Pending with a
    /// 3-D Secure URL. Sending the same Idempotency-Key again returns the same transaction
    /// without calling 2C2P again.
    /// </remarks>
    /// <param name="request">The customer, the product and optionally a saved card.</param>
    /// <param name="idempotencyKey" example="3f2b8c1e-7d4a-4e9b-9c6f-1a2b3c4d5e6f">
    /// A UUID the checkout page generates when it loads. Send the same value on every retry
    /// of this purchase; at most 100 characters.
    /// </param>
    /// <param name="ct">Cancelled when the client disconnects.</param>
    /// <response code="200">The transaction for this key: new, or the stored one on a repeat.</response>
    /// <response code="400">The Idempotency-Key header is missing or too long, or the body is invalid.</response>
    /// <response code="404">The customer, product or card does not exist.</response>
    /// <response code="409">A request with the same key is still being processed. Retry after the Retry-After seconds.</response>
    /// <response code="422">The card belongs to another customer, or the key was used for a different checkout.</response>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<CheckoutResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CheckoutResponse>> Checkout(
        [FromBody] CheckoutRequest request,
        [FromHeader(Name = "Idempotency-Key")]
        [Required(ErrorMessage = "The Idempotency-Key header is required.")]
        [MaxLength(CheckoutService.MaxIdempotencyKeyLength, ErrorMessage = "The Idempotency-Key header must be at most 100 characters.")]
        string? idempotencyKey,
        CancellationToken ct)
        => Ok(await checkout.CheckoutAsync(request, idempotencyKey, ct));
}
