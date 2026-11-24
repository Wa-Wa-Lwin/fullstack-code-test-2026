using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PaymentIntegration.Application.DTOs;
using PaymentIntegration.Domain.Entities;
using PaymentIntegration.Domain.Enums;
using PaymentIntegration.Infrastructure.Payments;
using PaymentIntegration.Infrastructure.Persistence;
using PaymentIntegration.Tests.TestSupport;

namespace PaymentIntegration.Tests.Integration;

/// <summary>Uses the real 2C2P gateway's notification parsing; no call leaves the process.</summary>
[Collection(ApiCollection.Name)]
public class PaymentNotificationTests(ApiFixture fixture)
{
    // Cloud Storage Plan, SGD 9.99, Recurring.
    private const int ProductId = 3;
    private const decimal Price = 9.99m;

    [Fact]
    public async Task The_same_notification_twice_saves_one_card_and_does_not_change_the_result()
    {
        const int customerId = 2;
        var transaction = await CreatePendingTransactionAsync(customerId);
        var customerToken = NewCustomerToken();
        var client = fixture.Factory.CreateClient();

        // Fill the cards cache first, so the test also shows the notification clears it.
        var before = await client.GetFromJsonAsync<List<CustomerCardDto>>($"/api/customers/{customerId}/cards");

        var payload = Sign(Notification(transaction.InvoiceNo, Price, customerToken));

        var first = await PostNotificationAsync(client, payload);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var afterFirst = await LoadAsync(transaction.InvoiceNo);
        Assert.Equal(TransactionStatus.Succeeded, afterFirst.Status);
        Assert.Equal("411111XXXXXX1111", afterFirst.MaskedCardNo);
        Assert.Equal("TR-1", afterFirst.ProviderRef);
        Assert.Equal("CC", afterFirst.PaymentChannel);
        Assert.NotNull(afterFirst.PaidAt);

        var second = await PostNotificationAsync(client, payload);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var afterSecond = await LoadAsync(transaction.InvoiceNo);
        Assert.Equal(TransactionStatus.Succeeded, afterSecond.Status);
        Assert.Equal(afterFirst.PaidAt, afterSecond.PaidAt);

        Assert.Equal(1, await CountCardsAsync(customerId, customerToken));

        var after = await client.GetFromJsonAsync<List<CustomerCardDto>>($"/api/customers/{customerId}/cards");
        Assert.Equal(before!.Count + 1, after!.Count);
    }

    [Fact]
    public async Task Ten_identical_notifications_at_once_save_one_card_and_succeed_once()
    {
        const int customerId = 3;
        var transaction = await CreatePendingTransactionAsync(customerId);
        var customerToken = NewCustomerToken();
        var client = fixture.Factory.CreateClient();
        var payload = Sign(Notification(transaction.InvoiceNo, Price, customerToken));

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(_ => Task.Run(() => PostNotificationAsync(client, payload))));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(TransactionStatus.Succeeded, (await LoadAsync(transaction.InvoiceNo)).Status);
        Assert.Equal(1, await CountCardsAsync(customerId, customerToken));
    }

    [Fact]
    public async Task Concurrent_success_and_failure_results_settle_on_exactly_one()
    {
        var transaction = await CreatePendingTransactionAsync(customerId: 2);
        var customerToken = NewCustomerToken();
        var client = fixture.Factory.CreateClient();
        var success = Sign(Notification(transaction.InvoiceNo, Price, customerToken));
        var failure = Sign(Notification(transaction.InvoiceNo, Price, customerToken: null, respCode: "4005", respDesc: "Do not honor"));

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(i => Task.Run(() => PostNotificationAsync(client, i % 2 == 0 ? success : failure))));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var stored = await LoadAsync(transaction.InvoiceNo);

        // Whichever result won, the row is consistent with it, and a card exists only for a success.
        if (stored.Status == TransactionStatus.Succeeded)
        {
            Assert.Equal("0000", stored.RespCode);
            Assert.NotNull(stored.PaidAt);
            Assert.Equal(1, await CountCardsAsync(2, customerToken));
        }
        else
        {
            Assert.Equal(TransactionStatus.Failed, stored.Status);
            Assert.Equal("4005", stored.RespCode);
            Assert.Null(stored.PaidAt);
            Assert.Equal(0, await CountCardsAsync(2, customerToken));
        }
    }

    [Fact]
    public async Task A_failure_notification_after_success_does_not_overturn_it()
    {
        var transaction = await CreatePendingTransactionAsync(customerId: 1);
        var client = fixture.Factory.CreateClient();

        await PostNotificationAsync(client, Sign(Notification(transaction.InvoiceNo, Price, customerToken: null)));
        var declined = Notification(transaction.InvoiceNo, Price, customerToken: null, respCode: "4005", respDesc: "Do not honor");
        var late = await PostNotificationAsync(client, Sign(declined));

        Assert.Equal(HttpStatusCode.OK, late.StatusCode);
        var stored = await LoadAsync(transaction.InvoiceNo);
        Assert.Equal(TransactionStatus.Succeeded, stored.Status);
        Assert.Equal("0000", stored.RespCode);
    }

    [Fact]
    public async Task A_notification_with_a_bad_signature_is_rejected_and_changes_nothing()
    {
        var transaction = await CreatePendingTransactionAsync(customerId: 1);
        var customerToken = NewCustomerToken();
        var forged = new TwoC2PJwt("FEDCBA9876543210FEDCBA9876543210FEDCBA9876543210FEDCBA9876543210")
            .Sign(JsonSerializer.Serialize(Notification(transaction.InvoiceNo, Price, customerToken)));

        var response = await PostNotificationAsync(fixture.Factory.CreateClient(), forged);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(TransactionStatus.Pending, (await LoadAsync(transaction.InvoiceNo)).Status);
        Assert.Equal(0, await CountCardsAsync(1, customerToken));
    }

    [Fact]
    public async Task A_notification_for_a_different_amount_never_marks_the_transaction_succeeded()
    {
        var transaction = await CreatePendingTransactionAsync(customerId: 3);
        var customerToken = NewCustomerToken();
        var payload = Sign(Notification(transaction.InvoiceNo, 0.01m, customerToken));

        var response = await PostNotificationAsync(fixture.Factory.CreateClient(), payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await LoadAsync(transaction.InvoiceNo);
        Assert.Equal(TransactionStatus.Pending, stored.Status);
        Assert.Null(stored.PaidAt);
        Assert.Equal(0, await CountCardsAsync(3, customerToken));
    }

    private static Dictionary<string, object?> Notification(
        string invoiceNo,
        decimal amount,
        string? customerToken,
        string respCode = "0000",
        string respDesc = "Success")
        => new()
        {
            ["merchantID"] = "JT01",
            ["invoiceNo"] = invoiceNo,
            ["amount"] = amount,
            ["currencyCode"] = "SGD",
            ["channelCode"] = "CC",
            ["accountNo"] = "411111XXXXXX1111",
            ["tranRef"] = "TR-1",
            ["referenceNo"] = "RN-1",
            ["customerToken"] = customerToken ?? string.Empty,
            ["respCode"] = respCode,
            ["respDesc"] = respDesc,
        };

    private static string NewCustomerToken() => "CT" + Guid.NewGuid().ToString("N")[..16];

    private static string Sign(object payload)
        => new TwoC2PJwt(ApiFixture.SecretKey).Sign(JsonSerializer.Serialize(payload));

    private static Task<HttpResponseMessage> PostNotificationAsync(HttpClient client, string payload)
        => client.PostAsJsonAsync("/api/payments/2c2p/notification", new { payload });

    private async Task<Transaction> CreatePendingTransactionAsync(int customerId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

        var suffix = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        var transaction = new Transaction
        {
            CustomerId = customerId,
            ProductId = ProductId,
            InvoiceNo = "INVTEST" + suffix,
            IdempotencyKey = "test-" + suffix,
            Amount = Price,
            Currency = "SGD",
            PaymentToken = "PT-" + suffix,
            RedirectUrl = "https://pay.test/" + suffix,
        };

        db.Transactions.Add(transaction);
        await db.SaveChangesAsync();
        return transaction;
    }

    private async Task<Transaction> LoadAsync(string invoiceNo)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        return await db.Transactions.AsNoTracking().SingleAsync(t => t.InvoiceNo == invoiceNo);
    }

    private async Task<int> CountCardsAsync(int customerId, string customerToken)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        return await db.CustomerCards.CountAsync(c => c.CustomerId == customerId && c.CustomerToken == customerToken);
    }
}
