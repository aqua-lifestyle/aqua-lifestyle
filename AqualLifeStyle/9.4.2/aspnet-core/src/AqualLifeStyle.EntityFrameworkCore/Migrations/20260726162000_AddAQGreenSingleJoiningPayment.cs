using System;
using AqualLifeStyle.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    [DbContext(typeof(AqualLifeStyleDbContext))]
    [Migration("20260726162000_AddAQGreenSingleJoiningPayment")]
    public partial class AddAQGreenSingleJoiningPayment : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Preserve original programme terms for rows that will be migrated
            // so they can be restored exactly if Downgrade is executed before any
            // post-migration AQGreen payment checkouts exist.
            migrationBuilder.CreateTable(
                name: "AQGreenMigrationBackup",
                columns: table => new
                {
                    ParticipationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OldTermsVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    OldTermsEffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AQGreenMigrationBackup", x => x.ParticipationId);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO "AQGreenMigrationBackup" ("ParticipationId", "OldTermsVersion", "OldTermsEffectiveFrom")
                SELECT "Id", "TermsVersion", "TermsEffectiveFrom"
                FROM "EntryParticipations"
                WHERE "Status" = 0
                  AND "RegistrationPaymentId" IS NULL
                  AND "ActivationPaymentId" IS NULL
                  AND "IsDeleted" = FALSE;
                """);

            migrationBuilder.AddColumn<decimal>(
                name: "JoiningPaymentAmount",
                table: "EntryParticipations",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "JoiningPaymentId",
                table: "EntryParticipations",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "EntryParticipations"
                SET "JoiningPaymentAmount" = 1200.00,
                    "TermsVersion" = '2026-07-single-1200',
                    "TermsEffectiveFrom" = TIMESTAMPTZ '2026-07-26 00:00:00+00'
                WHERE "Status" = 0
                  AND "RegistrationPaymentId" IS NULL
                  AND "ActivationPaymentId" IS NULL
                  AND "IsDeleted" = FALSE;
                """);

            migrationBuilder.CreateTable(
                name: "AQGreenJoiningCheckouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ProviderCheckoutId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CheckoutUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CheckoutCreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorUserId = table.Column<long>(type: "bigint", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierUserId = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeleterUserId = table.Column<long>(type: "bigint", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AQGreenJoiningCheckouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AQGreenJoiningCheckouts_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AQGreenJoiningCheckouts_EntryParticipations_ParticipationId",
                        column: x => x.ParticipationId,
                        principalTable: "EntryParticipations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AQGreenJoiningCheckouts_MemberPayments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "MemberPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EntryParticipations_JoiningPaymentId",
                table: "EntryParticipations",
                column: "JoiningPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_AQGreenJoiningCheckouts_CustomerId",
                table: "AQGreenJoiningCheckouts",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_AQGreenJoiningCheckouts_ParticipationId",
                table: "AQGreenJoiningCheckouts",
                column: "ParticipationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AQGreenJoiningCheckouts_PaymentId",
                table: "AQGreenJoiningCheckouts",
                column: "PaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AQGreenJoiningCheckouts_ProviderCheckoutId",
                table: "AQGreenJoiningCheckouts",
                column: "ProviderCheckoutId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_EntryParticipations_MemberPayments_JoiningPaymentId",
                table: "EntryParticipations",
                column: "JoiningPaymentId",
                principalTable: "MemberPayments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Block downgrade if new-term transactional or in-flight data exists.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "AQGreenJoiningCheckouts"
                    ) THEN
                        RAISE EXCEPTION 'Cannot downgrade the AQGreen single-joining-payment migration: '
                            'AQGreen payment checkouts exist. Downgrade would destroy pending or confirmed '
                            'payment records. Restore the database from a pre-migration snapshot instead.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM "EntryParticipations" ep
                        INNER JOIN "AQGreenMigrationBackup" backup
                            ON ep."Id" = backup."ParticipationId"
                        WHERE ep."JoiningPaymentId" IS NOT NULL
                    ) THEN
                        RAISE EXCEPTION 'Cannot downgrade the AQGreen single-joining-payment migration: '
                            'confirmed AQGreen joining payments exist. Downgrade would falsify financial '
                            'history. Restore the database from a pre-migration snapshot instead.';
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql(
                """
                UPDATE "EntryParticipations" ep
                SET "TermsVersion" = backup."OldTermsVersion",
                    "TermsEffectiveFrom" = backup."OldTermsEffectiveFrom"
                FROM "AQGreenMigrationBackup" backup
                WHERE ep."Id" = backup."ParticipationId";
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_EntryParticipations_MemberPayments_JoiningPaymentId",
                table: "EntryParticipations");

            migrationBuilder.DropTable(name: "AQGreenJoiningCheckouts");

            migrationBuilder.DropIndex(
                name: "IX_EntryParticipations_JoiningPaymentId",
                table: "EntryParticipations");

            migrationBuilder.DropColumn(
                name: "JoiningPaymentId",
                table: "EntryParticipations");

            migrationBuilder.DropColumn(
                name: "JoiningPaymentAmount",
                table: "EntryParticipations");

            migrationBuilder.DropTable(name: "AQGreenMigrationBackup");
        }
    }
}
