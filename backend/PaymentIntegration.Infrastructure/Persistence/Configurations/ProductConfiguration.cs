using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentIntegration.Domain.Entities;

namespace PaymentIntegration.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.Description)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(p => p.Price)
            .HasPrecision(12, 2)
            .IsRequired();

        // ISO 4217 code, e.g. "SGD".
        builder.Property(p => p.Currency)
            .HasMaxLength(3)
            .IsFixedLength()
            .HasDefaultValue("SGD")
            .IsRequired();

        // Stored by name ('OneTime', 'Recurring'), so the column is readable without the code
        // and reordering the enum cannot change what stored rows mean. Renaming a member
        // means migrating the stored values.
        builder.Property(p => p.PaymentType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasData(SeedData.Products);
    }
}
