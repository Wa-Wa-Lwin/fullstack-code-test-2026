using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PaymentIntegration.Application.DTOs;
using PaymentIntegration.Application.Interfaces;
using PaymentIntegration.Infrastructure.Persistence;
using PaymentIntegration.Tests.TestSupport;

namespace PaymentIntegration.Tests.Integration;

[Collection(ApiCollection.Name)]
public class CheckoutConcurrencyTests(ApiFixture fixture)
{
    private const int ConcurrentRequests = 12;

    [Fact]
    public async Task Concurrent_checkouts_with_one_key_create_one_transaction_and_one_gateway_call()
    {
        var gateway = new SlowCountingGateway();
        using var factory = WithServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IPaymentGateway>(gateway)));

        var key = Guid.NewGuid().ToString();
        var responses = await SendConcurrentCheckoutsAsync(factory, key);

        await AssertOneTransactionAsync(factory, key, responses);
        Assert.Equal(1, gateway.Calls);
    }

    [Fact]
    public async Task Without_the_redis_lock_the_unique_index_still_lets_only_one_request_through()
    {
        var gateway = new SlowCountingGateway();
        using var factory = WithServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<IPaymentGateway>(gateway));
            services.Replace(ServiceDescriptor.Singleton<IDistributedLockService>(new UnavailableLockService()));
        });

        var key = Guid.NewGuid().ToString();
        var responses = await SendConcurrentCheckoutsAsync(factory, key);

        await AssertOneTransactionAsync(factory, key, responses);
        Assert.Equal(1, gateway.Calls);
    }

    [Fact]
    public async Task A_repeat_after_the_first_finished_returns_the_same_transaction_without_calling_the_gateway()
    {
        var gateway = new SlowCountingGateway();
        using var factory = WithServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IPaymentGateway>(gateway)));
        var client = factory.CreateClient();
        var key = Guid.NewGuid().ToString();

        var first = await client.SendAsync(CheckoutRequest(key));
        var second = await client.SendAsync(CheckoutRequest(key));

        var a = await first.Content.ReadFromJsonAsync<CheckoutResponse>();
        var b = await second.Content.ReadFromJsonAsync<CheckoutResponse>();
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(a!.InvoiceNo, b!.InvoiceNo);
        Assert.Equal(a.RedirectUrl, b.RedirectUrl);
        Assert.Equal(1, gateway.Calls);
    }

    private WebApplicationFactory<Program> WithServices(Action<IServiceCollection> configure)
        => fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(configure));

    private static async Task<HttpResponseMessage[]> SendConcurrentCheckoutsAsync(
        WebApplicationFactory<Program> factory,
        string key)
    {
        var client = factory.CreateClient();
        using var start = new ManualResetEventSlim();

        // All requests wait on one signal, so they hit the API at the same moment.
        var requests = Enumerable.Range(0, ConcurrentRequests)
            .Select(_ => Task.Run(async () =>
            {
                start.Wait();
                return await client.SendAsync(CheckoutRequest(key));
            }))
            .ToArray();

        start.Set();
        return await Task.WhenAll(requests);
    }

    private static async Task AssertOneTransactionAsync(
        WebApplicationFactory<Program> factory,
        string key,
        HttpResponseMessage[] responses)
    {
        // Every response is the transaction itself or a 409 asking to retry.
        Assert.All(responses, r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));
        Assert.All(
            responses.Where(r => r.StatusCode == HttpStatusCode.Conflict),
            r => Assert.True(r.Headers.RetryAfter is not null, "A 409 must carry Retry-After."));

        var ok = responses.Where(r => r.StatusCode == HttpStatusCode.OK).ToList();
        Assert.NotEmpty(ok);

        var invoices = new HashSet<string>();
        foreach (var response in ok)
        {
            invoices.Add((await response.Content.ReadFromJsonAsync<CheckoutResponse>())!.InvoiceNo);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        var rows = await db.Transactions.Where(t => t.IdempotencyKey == key).ToListAsync();

        var row = Assert.Single(rows);
        Assert.Equal(row.InvoiceNo, Assert.Single(invoices));
    }

    private static HttpRequestMessage CheckoutRequest(string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/checkout")
        {
            Content = JsonContent.Create(new { customerId = 1, productId = 1 }),
        };
        request.Headers.Add("Idempotency-Key", key);
        return request;
    }
}
