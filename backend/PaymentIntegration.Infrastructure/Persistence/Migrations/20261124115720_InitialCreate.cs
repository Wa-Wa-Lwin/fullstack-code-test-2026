using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PaymentIntegration.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    first_name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customers", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false),
                    price = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "char(3)", fixedLength: true, maxLength: 3, nullable: false, defaultValue: "SGD"),
                    payment_type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_products", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "customer_cards",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    customer_id = table.Column<int>(type: "int", nullable: false),
                    customer_token = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    masked_card_no = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_cards", x => x.id);
                    table.ForeignKey(
                        name: "fk_customer_cards_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "transactions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    customer_id = table.Column<int>(type: "int", nullable: false),
                    product_id = table.Column<int>(type: "int", nullable: false),
                    customer_card_id = table.Column<int>(type: "int", nullable: true),
                    invoice_no = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    idempotency_key = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "char(3)", fixedLength: true, maxLength: 3, nullable: false, defaultValue: "SGD"),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    payment_token = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: true),
                    redirect_url = table.Column<string>(type: "varchar(2048)", maxLength: 2048, nullable: true),
                    payment_channel = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    masked_card_no = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true),
                    provider_ref = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    resp_code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    resp_desc = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    paid_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transactions", x => x.id);
                    table.ForeignKey(
                        name: "fk_transactions_customer_cards_customer_card_id",
                        column: x => x.customer_card_id,
                        principalTable: "customer_cards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transactions_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transactions_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.InsertData(
                table: "customers",
                columns: new[] { "id", "email", "first_name", "last_name" },
                values: new object[,]
                {
                    { 1, "alice.tan@example.com", "Alice", "Tan" },
                    { 2, "ben.lim@example.com", "Ben", "Lim" },
                    { 3, "chloe.ng@example.com", "Chloe", "Ng" }
                });

            migrationBuilder.InsertData(
                table: "products",
                columns: new[] { "id", "currency", "description", "name", "payment_type", "price" },
                values: new object[,]
                {
                    { 1, "SGD", "Compact Bluetooth mouse with a rechargeable battery.", "Wireless Mouse", "OneTime", 29.90m },
                    { 2, "SGD", "Tenkeyless keyboard with hot-swappable switches.", "Mechanical Keyboard", "OneTime", 149.00m },
                    { 3, "SGD", "100 GB of cloud storage, billed monthly to a saved card.", "Cloud Storage Plan", "Recurring", 9.99m },
                    { 4, "SGD", "Ad-free music streaming, billed monthly to a saved card.", "Music Streaming Membership", "Recurring", 12.98m }
                });

            migrationBuilder.CreateIndex(
                name: "ix_customer_cards_customer_id_customer_token",
                table: "customer_cards",
                columns: new[] { "customer_id", "customer_token" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_customers_email",
                table: "customers",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_transactions_customer_card_id",
                table: "transactions",
                column: "customer_card_id");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_customer_id",
                table: "transactions",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_idempotency_key",
                table: "transactions",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_transactions_invoice_no",
                table: "transactions",
                column: "invoice_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_transactions_product_id",
                table: "transactions",
                column: "product_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "transactions");

            migrationBuilder.DropTable(
                name: "customer_cards");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "customers");
        }
    }
}
