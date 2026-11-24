using PaymentIntegration.Domain.Entities;
using PaymentIntegration.Domain.Enums;

namespace PaymentIntegration.Infrastructure.Persistence;

/// <summary>
/// Rows that every database built from the migrations starts with, because the API has no
/// endpoints for creating customers or products. The IDs are fixed: HasData matches seed rows
/// by key between migrations, and the README and the .http file refer to these IDs.
/// Use date literals here, never DateTime.UtcNow, or every new migration will update the rows.
/// </summary>
public static class SeedData
{
    public static Customer[] Customers =>
    [
        new() { Id = 1, FirstName = "Alice", LastName = "Tan", Email = "alice.tan@example.com" },
        new() { Id = 2, FirstName = "Ben", LastName = "Lim", Email = "ben.lim@example.com" },
        new() { Id = 3, FirstName = "Chloe", LastName = "Ng", Email = "chloe.ng@example.com" },
    ];

    public static Product[] Products =>
    [
        new()
        {
            Id = 1,
            Name = "Wireless Mouse",
            Description = "Compact Bluetooth mouse with a rechargeable battery.",
            Price = 29.90m,
            Currency = "SGD",
            PaymentType = PaymentType.OneTime,
        },
        new()
        {
            Id = 2,
            Name = "Mechanical Keyboard",
            Description = "Tenkeyless keyboard with hot-swappable switches.",
            Price = 149.00m,
            Currency = "SGD",
            PaymentType = PaymentType.OneTime,
        },
        new()
        {
            Id = 3,
            Name = "Cloud Storage Plan",
            Description = "100 GB of cloud storage, billed monthly to a saved card.",
            Price = 9.99m,
            Currency = "SGD",
            PaymentType = PaymentType.Recurring,
        },
        new()
        {
            Id = 4,
            Name = "Music Streaming Membership",
            Description = "Ad-free music streaming, billed monthly to a saved card.",
            Price = 12.98m,
            Currency = "SGD",
            PaymentType = PaymentType.Recurring,
        },
    ];
}
