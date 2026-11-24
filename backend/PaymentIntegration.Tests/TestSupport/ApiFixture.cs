using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PaymentIntegration.Infrastructure.Persistence;
using Testcontainers.MySql;
using Testcontainers.Redis;

namespace PaymentIntegration.Tests.TestSupport;

/// <summary>
/// Starts MySQL and Redis in containers once for all integration tests, and hosts the real
/// API against them. The API applies its migrations (and seed data) at startup.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    public const string SecretKey = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";

    private readonly MySqlContainer _mysql = new MySqlBuilder("mysql:8.4")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine")
        .Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_mysql.StartAsync(), _redis.StartAsync());

        var mysqlConnection = _mysql.GetConnectionString();

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:PaymentDb", mysqlConnection);
            builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
            builder.UseSetting("TwoC2P:MerchantId", "JT01");
            builder.UseSetting("TwoC2P:SecretKey", SecretKey);
            builder.UseSetting("TwoC2P:BaseUrl", "https://pgw.invalid/payment/4.3/");
            builder.UseSetting("TwoC2P:FrontendReturnUrl", "http://localhost/api/payments/2c2p/return/{invoiceNo}");
            builder.UseSetting("TwoC2P:BackendReturnUrl", "http://localhost/api/payments/2c2p/notification");
        });

        // Starts the host, which runs the migrations.
        Factory.CreateClient().Dispose();

        // Never let a test write to a developer's database by accident.
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        var port = _mysql.GetMappedPublicPort(3306).ToString();
        if (!db.Database.GetConnectionString()!.Contains(port))
        {
            throw new InvalidOperationException("The test host is not using the MySQL test container.");
        }
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await Task.WhenAll(_mysql.DisposeAsync().AsTask(), _redis.DisposeAsync().AsTask());
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "api";
}
