using System.Text;
using Microsoft.EntityFrameworkCore;

namespace PaymentIntegration.Infrastructure.Persistence;

internal static class SnakeCaseNaming
{
    /// <summary>
    /// Renames every column to snake_case (InvoiceNo -> invoice_no) and lower-cases every key,
    /// foreign key and index name. Table names are set in each entity's configuration.
    /// Call it after the configurations are applied, so it sees every property they define.
    /// </summary>
    public static void ApplySnakeCaseNames(this ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.GetColumnName()));
            }

            // EF builds these names from the table and column names set above
            // (IX_transactions_invoice_no), so only the prefix still needs lower-casing.
            foreach (var key in entity.GetKeys())
            {
                key.SetName(key.GetName()?.ToLowerInvariant());
            }

            foreach (var foreignKey in entity.GetForeignKeys())
            {
                foreignKey.SetConstraintName(foreignKey.GetConstraintName()?.ToLowerInvariant());
            }

            foreach (var index in entity.GetIndexes())
            {
                index.SetDatabaseName(index.GetDatabaseName()?.ToLowerInvariant());
            }
        }
    }

    private static string ToSnakeCase(string name)
    {
        var builder = new StringBuilder(name.Length + 8);

        for (var i = 0; i < name.Length; i++)
        {
            var current = name[i];

            if (!char.IsUpper(current))
            {
                builder.Append(current);
                continue;
            }

            // A capital starts a new word after a lower-case letter or digit (CustomerId),
            // or at the end of an acronym (URLValue -> url_value).
            var startsNewWord = i > 0
                && name[i - 1] != '_'
                && (char.IsLower(name[i - 1])
                    || char.IsDigit(name[i - 1])
                    || (i + 1 < name.Length && char.IsLower(name[i + 1])));

            if (startsNewWord)
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(current));
        }

        return builder.ToString();
    }
}
