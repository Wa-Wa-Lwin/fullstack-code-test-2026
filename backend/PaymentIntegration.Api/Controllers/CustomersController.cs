using Microsoft.AspNetCore.Mvc;
using PaymentIntegration.Application.DTOs;
using PaymentIntegration.Application.Services;

namespace PaymentIntegration.Api.Controllers;

[ApiController]
[Route("api/customers")]
[Produces("application/json")]
public class CustomersController(CustomerService customers) : ControllerBase
{
    /// <summary>Lists the customers (cached in Redis).</summary>
    /// <response code="200">All customers.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CustomerDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CustomerDto>>> GetAll(CancellationToken ct)
        => Ok(await customers.GetAllAsync(ct));

    /// <summary>Lists a customer's saved cards (cached in Redis, cleared when a card is saved).</summary>
    /// <param name="id" example="1">The customer ID.</param>
    /// <param name="ct">Cancelled when the client disconnects.</param>
    /// <response code="200">The customer's saved cards, masked.</response>
    /// <response code="404">The customer does not exist.</response>
    [HttpGet("{id:int}/cards")]
    [ProducesResponseType<IReadOnlyList<CustomerCardDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<CustomerCardDto>>> GetCards(int id, CancellationToken ct)
        => Ok(await customers.GetCardsAsync(id, ct));
}
