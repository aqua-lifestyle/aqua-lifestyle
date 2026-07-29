using System;
using AqualLifeStyle.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    [DbContext(typeof(AqualLifeStyleDbContext))]
    [Migration("20260729190000_HardenTransactionalEmailDelivery")]
    public partial class HardenTransactionalEmailDelivery : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProcessingToken",
                table: "TransactionalEmailOutboxMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResponseVersion",
                table: "Enquiries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AccountEmailThrottles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_AccountEmailThrottles", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_AccountEmailThrottles_ExpiresAt",
                table: "AccountEmailThrottles",
                column: "ExpiresAt");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AccountEmailThrottles");
            migrationBuilder.DropColumn(
                name: "ProcessingToken",
                table: "TransactionalEmailOutboxMessages");
            migrationBuilder.DropColumn(
                name: "ResponseVersion",
                table: "Enquiries");
        }
    }
}
