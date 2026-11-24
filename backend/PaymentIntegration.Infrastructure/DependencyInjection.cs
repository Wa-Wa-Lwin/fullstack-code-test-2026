using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PaymentIntegration.Infrastructure.Persistence;

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

        return services;
    }
}
