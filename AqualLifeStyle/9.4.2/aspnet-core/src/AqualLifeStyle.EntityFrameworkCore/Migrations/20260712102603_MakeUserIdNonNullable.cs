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

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
