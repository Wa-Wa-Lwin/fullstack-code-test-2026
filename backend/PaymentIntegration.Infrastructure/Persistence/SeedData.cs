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
        new() { Id = 1, FirstName = "Lisa", LastName = "La", Email = "lisa.la@example.com" },
        new() { Id = 2, FirstName = "Rose", LastName = "Park", Email = "rose.park@example.com" },
        new() { Id = 3, FirstName = "Jennie", LastName = "Kim", Email = "jennie.kim@example.com" },
        new() { Id = 4, FirstName = "Jisoo", LastName = "Kim", Email = "jisoo.kim@example.com" },
    ];

    public static Product[] Products =>
    [
        new()
        {
            Id = 1,
            Name = "Bangkok Tour Ticket",
            Description = "One standing ticket for the Bangkok stop of the concert tour.",
            Price = 188.00m,
            Currency = "SGD",
            PaymentType = PaymentType.OneTime,
        },
        new()
        {
            Id = 2,
            Name = "Square One Album",
            Description = "The Square One single album, CD edition.",
            Price = 29.90m,
            Currency = "SGD",
            PaymentType = PaymentType.OneTime,
        },
        new()
        {
            Id = 3,
            Name = "Monthly Live Show",
            Description = "Monthly access to the live show stream, billed to a saved card.",
            Price = 9.99m,
            Currency = "SGD",
            PaymentType = PaymentType.Recurring,
        },
        new()
        {
            Id = 4,
            Name = "Yearly Photocard",
            Description = "A new photocard set every year, billed to a saved card.",
            Price = 99.00m,
            Currency = "SGD",
            PaymentType = PaymentType.Recurring,
        },
    ];
}
