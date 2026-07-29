using System;
using AqualLifeStyle.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    [DbContext(typeof(AqualLifeStyleDbContext))]
    [Migration("20260728140000_AddTransactionalEmailOutbox")]
    public partial class AddTransactionalEmailOutbox : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TransactionalEmailOutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: true),
                    NotificationType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Recipient = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    HtmlBody = table.Column<string>(type: "text", nullable: true),
                    TextBody = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessingStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProviderMessageId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LastError = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorUserId = table.Column<long>(type: "bigint", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierUserId = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeleterUserId = table.Column<long>(type: "bigint", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_TransactionalEmailOutboxMessages", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_TransactionalEmailOutboxMessages_IdempotencyKey",
                table: "TransactionalEmailOutboxMessages",
                column: "IdempotencyKey",
                unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_TransactionalEmailOutboxMessages_Status_NextAttemptAt",
                table: "TransactionalEmailOutboxMessages",
                columns: new[] { "Status", "NextAttemptAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
            => migrationBuilder.DropTable(name: "TransactionalEmailOutboxMessages");
    }
}
