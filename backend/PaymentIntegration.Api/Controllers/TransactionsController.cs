using Microsoft.AspNetCore.Mvc;
using PaymentIntegration.Application.DTOs;
using PaymentIntegration.Application.Services;

namespace PaymentIntegration.Api.Controllers;

[ApiController]
[Route("api/transactions")]
[Produces("application/json")]
public class TransactionsController(TransactionService transactions) : ControllerBase
{
    /// <summary>Gets a transaction's current status, read from the database (never cached).</summary>
    /// <param name="invoiceNo" example="INV20261124120000AB12CD">The invoice number returned by checkout.</param>
    /// <param name="ct">Cancelled when the client disconnects.</param>
    /// <response code="200">The transaction.</response>
    /// <response code="404">No transaction has this invoice number.</response>
    [HttpGet("{invoiceNo}")]
    [ProducesResponseType<TransactionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TransactionDto>> Get(string invoiceNo, CancellationToken ct)
        => Ok(await transactions.GetByInvoiceNoAsync(invoiceNo, ct));
}
