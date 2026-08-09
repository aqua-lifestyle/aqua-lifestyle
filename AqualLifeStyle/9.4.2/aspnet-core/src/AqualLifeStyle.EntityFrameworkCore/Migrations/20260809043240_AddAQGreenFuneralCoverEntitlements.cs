using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    /// <inheritdoc />
    public partial class AddAQGreenFuneralCoverEntitlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The R30,000 Aqua inclusion predates this software implementation.
            // Backfill only modern in-system AQGreen records whose linked,
            // confirmed payment facts prove the same completion event used by
            // the runtime processor. 2026-07-26 identifies the supported modern
            // joining model; it is not a funeral-cover or insurer commencement
            // date. Legacy members absent from these tables require a separate
            // authorised import and must never receive fabricated payments here.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "EntryParticipations" participation
                        WHERE participation."JoiningPaymentAmount" > 0.00
                          AND participation."IsDeleted" = FALSE
                          AND (
                              NOT EXISTS (
                                  SELECT 1
                                  FROM "Customers" customer
                                  WHERE customer."Id" = participation."CustomerId"
                                    AND customer."TenantId" = participation."TenantId"
                                    AND customer."IsDeleted" = FALSE
                              )
                              OR participation."JoiningPaymentAmount" <> 1200.00
                              OR participation."Currency" <> 'ZAR'
                              OR participation."TermsEffectiveFrom" <
                                  TIMESTAMPTZ '2026-07-26 00:00:00+00'
                              OR participation."TermsVersion" NOT IN (
                                  '2026-07-single-1200',
                                  '2026-08-single-1200',
                                  '2026-08-flexible-1200'
                              )
                              OR (
                                  participation."TermsVersion" IN (
                                      '2026-07-single-1200',
                                      '2026-08-single-1200'
                                  )
                                  AND participation."JoiningInstallmentAmount" <> 0.00
                              )
                              OR (
                                  participation."TermsVersion" = '2026-08-flexible-1200'
                                  AND participation."JoiningInstallmentAmount" <> 600.00
                              )
                              OR participation."StartedAt" < participation."TermsEffectiveFrom"
                              OR (
                                  participation."JoiningPaymentId" IS NOT NULL
                                  AND (
                                      participation."RegistrationPaymentId" IS NOT NULL
                                      OR participation."ActivationPaymentId" IS NOT NULL
                                  )
                              )
                              OR (
                                  participation."RegistrationPaymentId" IS NOT NULL
                                  AND participation."ActivationPaymentId" =
                                      participation."RegistrationPaymentId"
                              )
                              OR (
                                  participation."Status" IN (2, 3, 4)
                                  AND NOT (
                                      EXISTS (
                                          SELECT 1
                                          FROM "MemberPayments" payment
                                          JOIN "Customers" customer
                                            ON customer."Id" = participation."CustomerId"
                                           AND customer."TenantId" = participation."TenantId"
                                           AND customer."IsDeleted" = FALSE
                                          WHERE payment."Id" = participation."JoiningPaymentId"
                                            AND payment."TenantId" = participation."TenantId"
                                            AND payment."CustomerId" = participation."CustomerId"
                                            AND payment."Purpose" = 7
                                            AND payment."Status" = 1
                                            AND payment."Amount" = 1200.00
                                            AND payment."Currency" = 'ZAR'
                                            AND payment."ConfirmedAt" IS NOT NULL
                                            AND payment."ConfirmedAt" >= participation."StartedAt"
                                            AND payment."IsDeleted" = FALSE
                                      )
                                      OR EXISTS (
                                          SELECT 1
                                          FROM "MemberPayments" first_payment
                                          JOIN "MemberPayments" second_payment
                                            ON second_payment."Id" = participation."ActivationPaymentId"
                                          JOIN "Customers" customer
                                            ON customer."Id" = participation."CustomerId"
                                           AND customer."TenantId" = participation."TenantId"
                                           AND customer."IsDeleted" = FALSE
                                          WHERE first_payment."Id" = participation."RegistrationPaymentId"
                                            AND first_payment."Id" <> second_payment."Id"
                                            AND first_payment."TenantId" = participation."TenantId"
                                            AND second_payment."TenantId" = participation."TenantId"
                                            AND first_payment."CustomerId" = participation."CustomerId"
                                            AND second_payment."CustomerId" = participation."CustomerId"
                                            AND first_payment."Purpose" = 7
                                            AND second_payment."Purpose" = 7
                                            AND first_payment."Status" = 1
                                            AND second_payment."Status" = 1
                                            AND first_payment."Amount" = 600.00
                                            AND second_payment."Amount" = 600.00
                                            AND first_payment."Currency" = 'ZAR'
                                            AND second_payment."Currency" = 'ZAR'
                                            AND first_payment."ConfirmedAt" IS NOT NULL
                                            AND second_payment."ConfirmedAt" IS NOT NULL
                                            AND GREATEST(
                                                first_payment."ConfirmedAt",
                                                second_payment."ConfirmedAt") >= participation."StartedAt"
                                            AND first_payment."IsDeleted" = FALSE
                                            AND second_payment."IsDeleted" = FALSE
                                      )
                                  )
                              )
                              OR (
                                  EXISTS (
                                      SELECT 1
                                      FROM "MemberPayments" payment
                                      WHERE payment."Id" = participation."JoiningPaymentId"
                                        AND payment."Status" = 1
                                  )
                                  AND NOT EXISTS (
                                      SELECT 1
                                      FROM "MemberPayments" payment
                                      JOIN "Customers" customer
                                        ON customer."Id" = participation."CustomerId"
                                       AND customer."TenantId" = participation."TenantId"
                                       AND customer."IsDeleted" = FALSE
                                      WHERE payment."Id" = participation."JoiningPaymentId"
                                        AND payment."TenantId" = participation."TenantId"
                                        AND payment."CustomerId" = participation."CustomerId"
                                        AND payment."Purpose" = 7
                                        AND payment."Status" = 1
                                        AND payment."Amount" = 1200.00
                                        AND payment."Currency" = 'ZAR'
                                        AND payment."ConfirmedAt" IS NOT NULL
                                        AND payment."ConfirmedAt" >= participation."StartedAt"
                                        AND payment."IsDeleted" = FALSE
                                  )
                              )
                              OR (
                                  EXISTS (
                                      SELECT 1
                                      FROM "MemberPayments" payment
                                      WHERE payment."Id" = participation."RegistrationPaymentId"
                                        AND payment."Status" = 1
                                  )
                                  AND NOT EXISTS (
                                      SELECT 1
                                      FROM "MemberPayments" payment
                                      WHERE payment."Id" = participation."RegistrationPaymentId"
                                        AND payment."TenantId" = participation."TenantId"
                                        AND payment."CustomerId" = participation."CustomerId"
                                        AND payment."Purpose" = 7
                                        AND payment."Status" = 1
                                        AND payment."Amount" = 600.00
                                        AND payment."Currency" = 'ZAR'
                                        AND payment."ConfirmedAt" IS NOT NULL
                                        AND payment."ConfirmedAt" >= participation."StartedAt"
                                        AND payment."IsDeleted" = FALSE
                                  )
                              )
                              OR (
                                  EXISTS (
                                      SELECT 1
                                      FROM "MemberPayments" payment
                                      WHERE payment."Id" = participation."ActivationPaymentId"
                                        AND payment."Status" = 1
                                  )
                                  AND NOT EXISTS (
                                      SELECT 1
                                      FROM "MemberPayments" payment
                                      WHERE payment."Id" = participation."ActivationPaymentId"
                                        AND payment."TenantId" = participation."TenantId"
                                        AND payment."CustomerId" = participation."CustomerId"
                                        AND payment."Purpose" = 7
                                        AND payment."Status" = 1
                                        AND payment."Amount" = 600.00
                                        AND payment."Currency" = 'ZAR'
                                        AND payment."ConfirmedAt" IS NOT NULL
                                        AND payment."ConfirmedAt" >= participation."StartedAt"
                                        AND payment."IsDeleted" = FALSE
                                  )
                              )
                              OR (
                                  EXISTS (
                                      SELECT 1
                                      FROM "MemberPayments" first_payment
                                      JOIN "MemberPayments" second_payment
                                        ON second_payment."Id" = participation."ActivationPaymentId"
                                      WHERE first_payment."Id" = participation."RegistrationPaymentId"
                                        AND first_payment."Status" = 1
                                        AND second_payment."Status" = 1
                                  )
                                  AND NOT EXISTS (
                                      SELECT 1
                                      FROM "MemberPayments" first_payment
                                      JOIN "MemberPayments" second_payment
                                        ON second_payment."Id" = participation."ActivationPaymentId"
                                      JOIN "Customers" customer
                                        ON customer."Id" = participation."CustomerId"
                                       AND customer."TenantId" = participation."TenantId"
                                       AND customer."IsDeleted" = FALSE
                                      WHERE first_payment."Id" = participation."RegistrationPaymentId"
                                        AND first_payment."Id" <> second_payment."Id"
                                        AND first_payment."TenantId" = participation."TenantId"
                                        AND second_payment."TenantId" = participation."TenantId"
                                        AND first_payment."CustomerId" = participation."CustomerId"
                                        AND second_payment."CustomerId" = participation."CustomerId"
                                        AND first_payment."Purpose" = 7
                                        AND second_payment."Purpose" = 7
                                        AND first_payment."Status" = 1
                                        AND second_payment."Status" = 1
                                        AND first_payment."Amount" = 600.00
                                        AND second_payment."Amount" = 600.00
                                        AND first_payment."Currency" = 'ZAR'
                                        AND second_payment."Currency" = 'ZAR'
                                        AND first_payment."ConfirmedAt" IS NOT NULL
                                        AND second_payment."ConfirmedAt" IS NOT NULL
                                        AND GREATEST(
                                            first_payment."ConfirmedAt",
                                            second_payment."ConfirmedAt") >= participation."StartedAt"
                                        AND first_payment."IsDeleted" = FALSE
                                        AND second_payment."IsDeleted" = FALSE
                                  )
                              )
                          )
                    ) THEN
                        RAISE EXCEPTION 'Contradictory historical AQGreen joining-payment data requires authorised reconciliation before funeral-cover migration.';
                    END IF;
                END $$;
                """);

            migrationBuilder.CreateTable(
                name: "AQGreenFuneralCoverEntitlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    EntryParticipationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    FuneralCoverAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    TermsVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IncludedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_AQGreenFuneralCoverEntitlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AQGreenFuneralCoverEntitlements_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AQGreenFuneralCoverEntitlements_EntryParticipations_EntryPa~",
                        column: x => x.EntryParticipationId,
                        principalTable: "EntryParticipations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AQGreenFuneralCoverEntitlements_CustomerId",
                table: "AQGreenFuneralCoverEntitlements",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_AQGreenFuneralCoverEntitlements_EntryParticipationId",
                table: "AQGreenFuneralCoverEntitlements",
                column: "EntryParticipationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AQGreenFuneralCoverEntitlements_TenantId_CustomerId_Status",
                table: "AQGreenFuneralCoverEntitlements",
                columns: new[] { "TenantId", "CustomerId", "Status" });

            migrationBuilder.Sql(
                """
                WITH qualifying_completions AS (
                    SELECT
                        participation."Id" AS "ParticipationId",
                        participation."TenantId",
                        participation."CustomerId",
                        payment."ConfirmedAt" AS "IncludedAt"
                    FROM "EntryParticipations" participation
                    JOIN "Customers" customer
                      ON customer."Id" = participation."CustomerId"
                     AND customer."TenantId" = participation."TenantId"
                     AND customer."IsDeleted" = FALSE
                    JOIN "MemberPayments" payment
                      ON payment."Id" = participation."JoiningPaymentId"
                     AND payment."TenantId" = participation."TenantId"
                     AND payment."CustomerId" = participation."CustomerId"
                     AND payment."Purpose" = 7
                     AND payment."Status" = 1
                     AND payment."Amount" = 1200.00
                     AND payment."Currency" = 'ZAR'
                     AND payment."ConfirmedAt" IS NOT NULL
                     AND payment."ConfirmedAt" >= participation."StartedAt"
                     AND payment."IsDeleted" = FALSE
                    WHERE participation."JoiningPaymentAmount" = 1200.00
                      AND participation."Currency" = 'ZAR'
                      AND participation."TermsEffectiveFrom" >=
                          TIMESTAMPTZ '2026-07-26 00:00:00+00'
                      AND participation."TermsVersion" IN (
                          '2026-07-single-1200',
                          '2026-08-single-1200',
                          '2026-08-flexible-1200'
                      )
                      AND participation."JoiningPaymentId" IS NOT NULL
                      AND participation."RegistrationPaymentId" IS NULL
                      AND participation."ActivationPaymentId" IS NULL
                      AND participation."IsDeleted" = FALSE

                    UNION ALL

                    SELECT
                        participation."Id" AS "ParticipationId",
                        participation."TenantId",
                        participation."CustomerId",
                        GREATEST(
                            first_payment."ConfirmedAt",
                            second_payment."ConfirmedAt") AS "IncludedAt"
                    FROM "EntryParticipations" participation
                    JOIN "Customers" customer
                      ON customer."Id" = participation."CustomerId"
                     AND customer."TenantId" = participation."TenantId"
                     AND customer."IsDeleted" = FALSE
                    JOIN "MemberPayments" first_payment
                      ON first_payment."Id" = participation."RegistrationPaymentId"
                     AND first_payment."TenantId" = participation."TenantId"
                     AND first_payment."CustomerId" = participation."CustomerId"
                     AND first_payment."Purpose" = 7
                     AND first_payment."Status" = 1
                     AND first_payment."Amount" = 600.00
                     AND first_payment."Currency" = 'ZAR'
                     AND first_payment."ConfirmedAt" IS NOT NULL
                     AND first_payment."IsDeleted" = FALSE
                    JOIN "MemberPayments" second_payment
                      ON second_payment."Id" = participation."ActivationPaymentId"
                     AND second_payment."Id" <> first_payment."Id"
                     AND second_payment."TenantId" = participation."TenantId"
                     AND second_payment."CustomerId" = participation."CustomerId"
                     AND second_payment."Purpose" = 7
                     AND second_payment."Status" = 1
                     AND second_payment."Amount" = 600.00
                     AND second_payment."Currency" = 'ZAR'
                     AND second_payment."ConfirmedAt" IS NOT NULL
                     AND second_payment."IsDeleted" = FALSE
                    WHERE participation."JoiningPaymentAmount" = 1200.00
                      AND participation."JoiningInstallmentAmount" = 600.00
                      AND participation."Currency" = 'ZAR'
                      AND participation."TermsEffectiveFrom" >=
                          TIMESTAMPTZ '2026-07-26 00:00:00+00'
                      AND participation."TermsVersion" IN (
                          '2026-07-single-1200',
                          '2026-08-single-1200',
                          '2026-08-flexible-1200'
                      )
                      AND participation."JoiningPaymentId" IS NULL
                      AND participation."RegistrationPaymentId" IS NOT NULL
                      AND participation."ActivationPaymentId" IS NOT NULL
                      AND GREATEST(
                          first_payment."ConfirmedAt",
                          second_payment."ConfirmedAt") >= participation."StartedAt"
                      AND participation."IsDeleted" = FALSE
                )
                INSERT INTO "AQGreenFuneralCoverEntitlements" (
                    "Id",
                    "TenantId",
                    "EntryParticipationId",
                    "CustomerId",
                    "FuneralCoverAmount",
                    "Currency",
                    "TermsVersion",
                    "IncludedAt",
                    "Status",
                    "CreationTime",
                    "CreatorUserId",
                    "LastModificationTime",
                    "LastModifierUserId",
                    "IsDeleted",
                    "DeleterUserId",
                    "DeletionTime"
                )
                SELECT
                    completion."ParticipationId",
                    completion."TenantId",
                    completion."ParticipationId",
                    completion."CustomerId",
                    30000.00,
                    'ZAR',
                    '2026-08-funeral-30000',
                    completion."IncludedAt",
                    0,
                    completion."IncludedAt",
                    NULL,
                    NULL,
                    NULL,
                    FALSE,
                    NULL,
                    NULL
                FROM qualifying_completions completion
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM "AQGreenFuneralCoverEntitlements" existing
                    WHERE existing."EntryParticipationId" = completion."ParticipationId"
                )
                ON CONFLICT ("EntryParticipationId") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AQGreenFuneralCoverEntitlements");
        }
    }
}
