using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PaymentIntegration.Application.DTOs;
using PaymentIntegration.Application.DTOs.Payments;
using PaymentIntegration.Application.Exceptions;
using PaymentIntegration.Application.Interfaces;
using PaymentIntegration.Domain.Entities;
using PaymentIntegration.Domain.Enums;
using PaymentIntegration.Infrastructure.Persistence;
using PaymentIntegration.Tests.TestSupport;

namespace PaymentIntegration.Tests.Integration;

/// <summary>The input checks the spec asks for, and a rejected Payment Token request.</summary>
[Collection(ApiCollection.Name)]
public class CheckoutValidationTests(ApiFixture fixture)
{
    [Fact]
    public async Task A_missing_idempotency_key_is_a_400_problem()
    {
        var response = await fixture.Factory.CreateClient().PostAsJsonAsync(
            "/api/checkout", new { customerId = 1, productId = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("Idempotency-Key", problem!.Errors.Keys);
    }

    [Theory]
    [InlineData(999, 1)]
    [InlineData(1, 999)]
    public async Task An_unknown_customer_or_product_is_a_404(int customerId, int productId)
    {
        var response = await fixture.Factory.CreateClient().SendAsync(
            Checkout(Guid.NewGuid().ToString(), new { customerId, productId }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task A_card_of_another_customer_is_a_422()
    {
        int cardId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
            var card = new CustomerCard
            {
                CustomerId = 2,
                CustomerToken = "CT" + Guid.NewGuid().ToString("N")[..16],
                MaskedCardNo = "411111XXXXXX1111",
            };
            db.CustomerCards.Add(card);
            await db.SaveChangesAsync();
            cardId = card.Id;
        }

        var response = await fixture.Factory.CreateClient().SendAsync(
            Checkout(Guid.NewGuid().ToString(), new { customerId = 1, productId = 1, customerCardId = cardId }));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task A_rejected_payment_token_request_marks_the_transaction_failed_with_2C2Ps_code()
    {
        var gateway = new RejectingGateway("9015", "Existing Invoice Number");
        using var factory = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IPaymentGateway>(gateway))));

        var response = await factory.CreateClient().SendAsync(
            Checkout(Guid.NewGuid().ToString(), new { customerId = 1, productId = 1 }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CheckoutResponse>();
        Assert.Equal("Failed", body!.Status);
        Assert.Null(body.RedirectUrl);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        var stored = await db.Transactions.AsNoTracking().SingleAsync(t => t.InvoiceNo == body.InvoiceNo);
        Assert.Equal(TransactionStatus.Failed, stored.Status);
        Assert.Equal("9015", stored.RespCode);
        Assert.Equal("Existing Invoice Number", stored.RespDesc);
    }

    private static HttpRequestMessage Checkout(string key, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/checkout") { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key);
        return request;
    }

    private sealed class RejectingGateway(string code, string description) : IPaymentGateway
    {
        public Task<HostedPaymentResult> CreateHostedPaymentAsync(HostedPaymentRequest request, CancellationToken ct)
            => throw new PaymentGatewayException("rejected", code, providerDescription: description);

        public Task<SavedCardResult> ChargeSavedCardAsync(SavedCardRequest request, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<PaymentResult> InquireAsync(string invoiceNo, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<PaymentResult> ParseNotificationAsync(string payload, CancellationToken ct)
            => throw new NotSupportedException();
    }
}
