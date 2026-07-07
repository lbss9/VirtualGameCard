using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VirtualGameCard.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchasePaymentLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "GiftCardPurchases",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100
            );

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "GiftCardPurchases",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "PaidAt",
                table: "GiftCardPurchases",
                type: "timestamp with time zone",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "PaymentReference",
                table: "GiftCardPurchases",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.Sql(
                """
                UPDATE "GiftCardPurchases"
                SET "IdempotencyKey" = "Id"::text,
                    "PaymentReference" = 'legacy_' || "Id"::text;
                """
            );

            migrationBuilder.CreateTable(
                name: "PaymentWebhookEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderEventId = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    PurchaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceivedAt = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentWebhookEvents", x => x.Id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_GiftCardPurchases_PaymentReference",
                table: "GiftCardPurchases",
                column: "PaymentReference",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_GiftCardPurchases_UserId_IdempotencyKey",
                table: "GiftCardPurchases",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_PaymentWebhookEvents_ProviderEventId",
                table: "PaymentWebhookEvents",
                column: "ProviderEventId",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_PaymentWebhookEvents_PurchaseId",
                table: "PaymentWebhookEvents",
                column: "PurchaseId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PaymentWebhookEvents");

            migrationBuilder.DropIndex(
                name: "IX_GiftCardPurchases_PaymentReference",
                table: "GiftCardPurchases"
            );

            migrationBuilder.DropIndex(
                name: "IX_GiftCardPurchases_UserId_IdempotencyKey",
                table: "GiftCardPurchases"
            );

            migrationBuilder.DropColumn(name: "IdempotencyKey", table: "GiftCardPurchases");

            migrationBuilder.DropColumn(name: "PaidAt", table: "GiftCardPurchases");

            migrationBuilder.DropColumn(name: "PaymentReference", table: "GiftCardPurchases");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "GiftCardPurchases",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true
            );
        }
    }
}
