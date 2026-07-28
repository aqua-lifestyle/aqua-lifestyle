using System;
using AqualLifeStyle.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    [DbContext(typeof(AqualLifeStyleDbContext))]
    [Migration("20260728110000_AddYocoWebhookReceipts")]
    public partial class AddYocoWebhookReceipts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "YocoWebhookReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    EventId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PaymentId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProviderCheckoutId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PayloadHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Programme = table.Column<int>(type: "integer", nullable: false),
                    CheckoutReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YocoWebhookReceipts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_YocoWebhookReceipts_EventId",
                table: "YocoWebhookReceipts",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YocoWebhookReceipts_PaymentId",
                table: "YocoWebhookReceipts",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_YocoWebhookReceipts_ProviderCheckoutId",
                table: "YocoWebhookReceipts",
                column: "ProviderCheckoutId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "YocoWebhookReceipts");
        }
    }
}
