using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    /// <inheritdoc />
    public partial class MakeUserIdNonNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM pg_indexes 
                        WHERE indexname = 'IX_Customers_UserId'
                    ) THEN
                        DROP INDEX ""IX_Customers_UserId"";
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                UPDATE ""AbpUsers"" AS app_user
                SET
                    ""IsDeleted"" = FALSE,
                    ""IsActive"" = FALSE,
                    ""Password"" = 'MIGRATED_ACCOUNT_REQUIRES_PASSWORD_RESET',
                    ""AccessFailedCount"" = 0,
                    ""IsLockoutEnabled"" = TRUE,
                    ""IsPhoneNumberConfirmed"" = FALSE,
                    ""IsTwoFactorEnabled"" = FALSE,
                    ""IsEmailConfirmed"" = FALSE,
                    ""SecurityStamp"" = gen_random_uuid()::text,
                    ""ConcurrencyStamp"" = gen_random_uuid()::text,
                    ""NormalizedEmailAddress"" = UPPER(TRIM(app_user.""EmailAddress"")),
                    ""NormalizedUserName"" = UPPER(TRIM(app_user.""UserName""))
                WHERE ""IsDeleted"" = TRUE
                  AND EXISTS (
                      SELECT 1
                      FROM ""Customers"" AS customer
                      WHERE customer.""UserId"" IS NULL
                        AND customer.""TenantId"" IS NOT DISTINCT FROM app_user.""TenantId""
                        AND UPPER(TRIM(customer.""Email"")) = UPPER(TRIM(app_user.""EmailAddress""))
                  );

                INSERT INTO ""AbpUsers"" (
                    ""CreationTime"", ""IsDeleted"", ""UserName"", ""TenantId"", ""EmailAddress"",
                    ""Name"", ""Surname"", ""Password"", ""AccessFailedCount"", ""IsLockoutEnabled"",
                    ""IsPhoneNumberConfirmed"", ""IsTwoFactorEnabled"", ""IsEmailConfirmed"", ""IsActive"",
                    ""NormalizedUserName"", ""NormalizedEmailAddress"", ""SecurityStamp"", ""ConcurrencyStamp"", ""Role"")
                SELECT
                    NOW(), FALSE, 'customer_' || customer.""Id"", customer.""TenantId"", customer.""Email"",
                    LEFT(customer.""Name"", 64), 'Customer', 'MIGRATED_ACCOUNT_REQUIRES_PASSWORD_RESET', 0, TRUE,
                    FALSE, FALSE, FALSE, FALSE,
                    UPPER('customer_' || customer.""Id""), UPPER(TRIM(customer.""Email"")),
                    gen_random_uuid()::text, gen_random_uuid()::text, 0
                FROM ""Customers"" AS customer
                WHERE customer.""UserId"" IS NULL
                  AND NOT EXISTS (
                      SELECT 1
                      FROM ""AbpUsers"" AS existing_user
                      WHERE existing_user.""TenantId"" IS NOT DISTINCT FROM customer.""TenantId""
                        AND UPPER(TRIM(existing_user.""EmailAddress"")) = UPPER(TRIM(customer.""Email""))
                  );

                UPDATE ""Customers"" AS customer
                SET ""UserId"" = app_user.""Id""
                FROM ""AbpUsers"" AS app_user
                WHERE customer.""UserId"" IS NULL
                  AND customer.""TenantId"" IS NOT DISTINCT FROM app_user.""TenantId""
                  AND UPPER(TRIM(customer.""Email"")) = UPPER(TRIM(app_user.""EmailAddress""))
                  AND app_user.""IsDeleted"" = FALSE;

                DO $block$
                BEGIN
                    IF EXISTS (SELECT 1 FROM ""Customers"" WHERE ""UserId"" IS NULL) THEN
                        RAISE EXCEPTION 'Cannot enforce Customers.UserId: unmatched customers require manual review';
                    END IF;
                END
                $block$;
            ");

            migrationBuilder.AlterColumn<long>(
                name: "UserId",
                table: "Customers",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long?),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_UserId",
                table: "Customers",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Customers_UserId",
                table: "Customers");

            migrationBuilder.AlterColumn<long>(
                name: "UserId",
                table: "Customers",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: false);
        }
    }
}
