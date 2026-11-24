using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PaymentIntegration.Api;

/// <summary>
/// Gives the error responses in Swagger an example, so every documented status code shows
/// what the problem-details body looks like.
/// </summary>
internal sealed class ProblemDetailsExamplesFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema is not OpenApiSchema concrete)
        {
            return;
        }

        // The document is OpenAPI 3.0, which has only the single "example" keyword. The newer
        // Examples list is an OpenAPI 3.1 feature and is left out of 3.0 output.
#pragma warning disable CS0618
        if (context.Type == typeof(ValidationProblemDetails))
        {
            concrete.Example = new JsonObject
            {
                ["type"] = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                ["title"] = "The request is invalid.",
                ["status"] = 400,
                ["detail"] = "The Idempotency-Key header is required.",
                ["errors"] = new JsonObject
                {
                    ["Idempotency-Key"] = new JsonArray("The Idempotency-Key header is required."),
                },
            };
        }
        else if (context.Type == typeof(ProblemDetails))
        {
            concrete.Example = new JsonObject
            {
                ["type"] = "https://tools.ietf.org/html/rfc9110#section-15.5.5",
                ["title"] = "The resource was not found.",
                ["status"] = 404,
                ["detail"] = "Customer '99' was not found.",
            };
        }
#pragma warning restore CS0618
    }
}
