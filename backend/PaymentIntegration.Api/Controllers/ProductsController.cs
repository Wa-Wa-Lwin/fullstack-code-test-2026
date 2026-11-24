using Microsoft.AspNetCore.Mvc;
using PaymentIntegration.Application.DTOs;
using PaymentIntegration.Application.Services;

namespace PaymentIntegration.Api.Controllers;

[ApiController]
[Route("api/products")]
[Produces("application/json")]
public class ProductsController(ProductService products) : ControllerBase
{
    /// <summary>Lists the products (cached in Redis).</summary>
    /// <response code="200">All products.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ProductDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> GetAll(CancellationToken ct)
        => Ok(await products.GetAllAsync(ct));
}
