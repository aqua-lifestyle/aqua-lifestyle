using AqualLifeStyle.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    [DbContext(typeof(AqualLifeStyleDbContext))]
    [Migration("20260722161500_EnableCustomerSelfRegistrationByDefault")]
    public partial class EnableCustomerSelfRegistrationByDefault : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE ""AbpSettings""
SET ""Value"" = 'true',
    ""LastModificationTime"" = NOW()
WHERE ""TenantId"" IN (
        SELECT ""Id""
        FROM ""AbpTenants""
        WHERE ""IsActive"" = TRUE)
  AND ""UserId"" IS NULL
  AND ""Name"" = 'Abp.Account.IsSelfRegistrationEnabled';

INSERT INTO ""AbpSettings"" (
    ""TenantId"",
    ""UserId"",
    ""Name"",
    ""Value"",
    ""CreationTime"")
SELECT
    tenant.""Id"",
    NULL,
    'Abp.Account.IsSelfRegistrationEnabled',
    'true',
    NOW()
FROM ""AbpTenants"" AS tenant
WHERE tenant.""IsActive"" = TRUE
  AND NOT EXISTS (
      SELECT 1
      FROM ""AbpSettings"" AS setting
      WHERE setting.""TenantId"" = tenant.""Id""
        AND setting.""UserId"" IS NULL
        AND setting.""Name"" = 'Abp.Account.IsSelfRegistrationEnabled');
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE ""AbpSettings""
SET ""Value"" = 'false',
    ""LastModificationTime"" = NOW()
WHERE ""TenantId"" IN (
        SELECT ""Id""
        FROM ""AbpTenants""
        WHERE ""IsActive"" = TRUE)
  AND ""UserId"" IS NULL
  AND ""Name"" = 'Abp.Account.IsSelfRegistrationEnabled';
");
        }
    }
}
