using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PaymentIntegration.Application.Interfaces;
using PaymentIntegration.Infrastructure.Persistence;
using PaymentIntegration.Infrastructure.Redis;
using PaymentIntegration.Infrastructure.Repositories;
using StackExchange.Redis;

namespace PaymentIntegration.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PaymentDb")
            ?? throw new InvalidOperationException(
                "Connection string 'ConnectionStrings:PaymentDb' is not configured.");

        services.AddDbContext<PaymentDbContext>(options => options.UseMySQL(connectionString));

        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICustomerCardRepository, CustomerCardRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();

        var redisConnectionString = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException(
                "Connection string 'ConnectionStrings:Redis' is not configured.");

        // One multiplexer for the whole app: it is thread-safe and holds the connection.
        // The cache and the lock both use it.
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(redisConnectionString);

            // Start even when Redis is down. The cache falls back to MySQL, and the
            // multiplexer keeps reconnecting in the background.
            options.AbortOnConnectFail = false;

            // While disconnected, fail commands at once instead of queueing them until the
            // timeout, so an outage costs a log line, not a 5-second wait per cached read.
            options.BacklogPolicy = BacklogPolicy.FailFast;

            return ConnectionMultiplexer.Connect(options);
        });

        services.AddSingleton<ICacheService, RedisCacheService>();
        services.AddSingleton<IDistributedLockService, RedisDistributedLockService>();

        return services;
    }
}
