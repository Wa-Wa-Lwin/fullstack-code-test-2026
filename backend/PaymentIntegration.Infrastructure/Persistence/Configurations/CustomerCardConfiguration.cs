using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentIntegration.Domain.Entities;

namespace PaymentIntegration.Infrastructure.Persistence.Configurations;

internal sealed class CustomerCardConfiguration : IEntityTypeConfiguration<CustomerCard>
{
    public void Configure(EntityTypeBuilder<CustomerCard> builder)
    {
        builder.ToTable("customer_cards");

        builder.HasKey(cc => cc.Id);

        // The gateway's token for the stored card. The raw card number is never stored.
        builder.Property(cc => cc.CustomerToken)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(cc => cc.MaskedCardNo)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(cc => cc.CreatedAt)
            .IsRequired();

        // Defines "the same card" for ICustomerCardRepository.AddIfNotExistsAsync, so two
        // concurrent saves of one card still produce one row. CustomerId is the leading
        // column, so this index also serves GetByCustomerIdAsync and the foreign key.
        builder.HasIndex(cc => new { cc.CustomerId, cc.CustomerToken })
            .IsUnique();

        builder.HasOne(cc => cc.Customer)
            .WithMany(c => c.Cards)
            .HasForeignKey(cc => cc.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
