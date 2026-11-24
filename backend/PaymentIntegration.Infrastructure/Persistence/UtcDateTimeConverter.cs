using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace PaymentIntegration.Infrastructure.Persistence;

/// <summary>
/// Writes a <see cref="DateTime"/> as UTC and reads it back with <see cref="DateTimeKind.Utc"/>,
/// so the API serialises timestamps with a "Z" suffix instead of as local time.
/// </summary>
internal sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value,
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
