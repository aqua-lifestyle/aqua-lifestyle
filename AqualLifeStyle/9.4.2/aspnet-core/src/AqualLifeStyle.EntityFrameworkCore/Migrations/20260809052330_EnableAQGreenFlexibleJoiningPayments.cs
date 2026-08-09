using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    /// <inheritdoc />
    public partial class EnableAQGreenFlexibleJoiningPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "EntryParticipations"
                SET "JoiningInstallmentAmount" = 600.00,
                    "TermsVersion" = '2026-08-flexible-1200'
                WHERE "TermsVersion" = '2026-08-single-1200'
                  AND "JoiningPaymentAmount" = 1200.00
                  AND "JoiningInstallmentAmount" = 0.00
                  AND "Status" = 0
                  AND "JoiningPaymentId" IS NULL
                  AND "RegistrationPaymentId" IS NULL
                  AND "ActivationPaymentId" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "EntryParticipations"
                        WHERE "TermsVersion" = '2026-08-flexible-1200'
                          AND (
                              "JoiningPaymentSchedule" = 1
                              OR "RegistrationPaymentId" IS NOT NULL
                              OR "ActivationPaymentId" IS NOT NULL
                          )
                    ) OR EXISTS (
                        SELECT 1
                        FROM "AQGreenJoiningCheckouts"
                        WHERE "Schedule" = 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot disable flexible AQGreen joining payments while two-instalment history exists.';
                    END IF;
                END $$;

                UPDATE "EntryParticipations"
                SET "JoiningInstallmentAmount" = 0.00,
                    "TermsVersion" = '2026-08-single-1200'
                WHERE "TermsVersion" = '2026-08-flexible-1200';
                """);
        }
    }
}
