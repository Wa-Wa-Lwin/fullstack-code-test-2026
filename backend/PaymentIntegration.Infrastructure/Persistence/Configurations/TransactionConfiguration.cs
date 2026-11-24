using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentIntegration.Domain.Entities;

namespace PaymentIntegration.Infrastructure.Persistence.Configurations;

internal sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.InvoiceNo)
            .HasMaxLength(50)
            .IsRequired();

        // The gateway identifies a payment by its invoice number, and the notification and
        // inquiry code find the transaction with GetByInvoiceNoAsync. If two rows shared one,
        // a notification could update the wrong transaction.
        builder.HasIndex(t => t.InvoiceNo)
            .IsUnique();

        builder.Property(t => t.IdempotencyKey)
            .HasMaxLength(100)
            .IsRequired();

        // The backup for the Redis lock. If the lock expires while a slow request is still
        // running, or Redis is down, two requests with the same Idempotency-Key can both find
        // nothing and both insert. This index makes the second insert fail instead of
        // charging the customer twice.
        builder.HasIndex(t => t.IdempotencyKey)
            .IsUnique();

        builder.Property(t => t.Amount)
            .HasPrecision(12, 2)
            .IsRequired();

        builder.Property(t => t.Currency)
            .HasMaxLength(3)
            .IsFixedLength()
            .HasDefaultValue("SGD")
            .IsRequired();

        // Stored by name ('Pending', 'Succeeded', 'Failed'), so the column is readable without
        // the code and reordering the enum cannot change what stored rows mean. Renaming a
        // member means migrating the stored values. The private setter is mapped as is; only
        // MarkSucceeded/MarkFailed change it.
        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.PaymentToken)
            .HasMaxLength(512);

        builder.Property(t => t.RedirectUrl)
            .HasMaxLength(2048);

        builder.Property(t => t.PaymentChannel)
            .HasMaxLength(50);

        builder.Property(t => t.MaskedCardNo)
            .HasMaxLength(32);

        builder.Property(t => t.ProviderRef)
            .HasMaxLength(100);

        builder.Property(t => t.RespCode)
            .HasMaxLength(50);

        builder.Property(t => t.RespDesc)
            .HasMaxLength(500);

        builder.Property(t => t.CreatedAt)
            .IsRequired();

        // A transaction is a financial record, so deleting a customer, product or card that a
        // transaction points to is refused rather than cascaded or nulled out.
        builder.HasOne(t => t.Customer)
            .WithMany()
            .HasForeignKey(t => t.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Product)
            .WithMany()
            .HasForeignKey(t => t.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.CustomerCard)
            .WithMany()
            .HasForeignKey(t => t.CustomerCardId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
