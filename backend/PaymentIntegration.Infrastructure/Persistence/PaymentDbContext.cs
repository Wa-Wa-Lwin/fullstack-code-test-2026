using Microsoft.EntityFrameworkCore;
using PaymentIntegration.Domain.Entities;

namespace PaymentIntegration.Infrastructure.Persistence;

public class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<CustomerCard> CustomerCards => Set<CustomerCard>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Transaction> Transactions => Set<Transaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Picks up every IEntityTypeConfiguration<T> in Persistence/Configurations.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentDbContext).Assembly);

        // Must run last, after every configuration has added its properties and indexes.
        modelBuilder.ApplySnakeCaseNames();
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // MySQL DATETIME has no time zone, so values come back with Kind = Unspecified.
        // Every timestamp is stored in UTC, so mark them as UTC when they are read.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }
}
