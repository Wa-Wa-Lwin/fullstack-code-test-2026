using System.Globalization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PaymentIntegration.Application.Exceptions;

namespace PaymentIntegration.Api;

/// <summary>
/// Maps the Application exceptions to problem-details responses in one place. Anything else
/// falls through to the default 500 problem response.
/// </summary>
internal sealed class ApiExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "The request is invalid."),
            NotFoundException => (StatusCodes.Status404NotFound, "The resource was not found."),
            ConflictException => (StatusCodes.Status409Conflict, "The request conflicts with one in progress."),
            BusinessRuleException => (StatusCodes.Status422UnprocessableEntity, "The request cannot be processed."),
            PaymentGatewayException => (StatusCodes.Status502BadGateway, "The payment provider could not complete the request."),
            _ => (0, null),
        };

        if (title is null)
        {
            return false;
        }

        if (exception is ConflictException { RetryAfter: { } retryAfter })
        {
            httpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        var problem = exception is ValidationException { Errors.Count: > 0 } validation
            ? new ValidationProblemDetails(validation.Errors.ToDictionary(e => e.Key, e => e.Value))
            : new ProblemDetails();

        problem.Status = status;
        problem.Title = title;
        problem.Detail = exception.Message;

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
