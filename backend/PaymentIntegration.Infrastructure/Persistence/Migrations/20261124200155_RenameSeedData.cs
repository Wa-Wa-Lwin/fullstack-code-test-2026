using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentIntegration.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameSeedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "customers",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "email", "first_name", "last_name" },
                values: new object[] { "lisa.la@example.com", "Lisa", "La" });

            migrationBuilder.UpdateData(
                table: "customers",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "email", "first_name", "last_name" },
                values: new object[] { "rose.park@example.com", "Rose", "Park" });

            migrationBuilder.UpdateData(
                table: "customers",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "email", "first_name", "last_name" },
                values: new object[] { "jennie.kim@example.com", "Jennie", "Kim" });

            migrationBuilder.InsertData(
                table: "customers",
                columns: new[] { "id", "email", "first_name", "last_name" },
                values: new object[] { 4, "jisoo.kim@example.com", "Jisoo", "Kim" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "description", "name", "price" },
                values: new object[] { "One standing ticket for the Bangkok stop of the concert tour.", "Bangkok Tour Ticket", 188.00m });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "description", "name", "price" },
                values: new object[] { "The Square One single album, CD edition.", "Square One Album", 29.90m });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "description", "name" },
                values: new object[] { "Monthly access to the live show stream, billed to a saved card.", "Monthly Live Show" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "id",
                keyValue: 4,
                columns: new[] { "description", "name", "price" },
                values: new object[] { "A new photocard set every year, billed to a saved card.", "Yearly Photocard", 99.00m });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "customers",
                keyColumn: "id",
                keyValue: 4);

            migrationBuilder.UpdateData(
                table: "customers",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "email", "first_name", "last_name" },
                values: new object[] { "alice.tan@example.com", "Alice", "Tan" });

            migrationBuilder.UpdateData(
                table: "customers",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "email", "first_name", "last_name" },
                values: new object[] { "ben.lim@example.com", "Ben", "Lim" });

            migrationBuilder.UpdateData(
                table: "customers",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "email", "first_name", "last_name" },
                values: new object[] { "chloe.ng@example.com", "Chloe", "Ng" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "description", "name", "price" },
                values: new object[] { "Compact Bluetooth mouse with a rechargeable battery.", "Wireless Mouse", 29.90m });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "description", "name", "price" },
                values: new object[] { "Tenkeyless keyboard with hot-swappable switches.", "Mechanical Keyboard", 149.00m });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "description", "name" },
                values: new object[] { "100 GB of cloud storage, billed monthly to a saved card.", "Cloud Storage Plan" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "id",
                keyValue: 4,
                columns: new[] { "description", "name", "price" },
                values: new object[] { "Ad-free music streaming, billed monthly to a saved card.", "Music Streaming Membership", 12.98m });
        }
    }
}
