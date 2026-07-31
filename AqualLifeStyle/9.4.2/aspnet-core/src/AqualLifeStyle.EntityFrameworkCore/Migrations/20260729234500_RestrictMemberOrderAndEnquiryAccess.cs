using AqualLifeStyle.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    [DbContext(typeof(AqualLifeStyleDbContext))]
    [Migration("20260729234500_RestrictMemberOrderAndEnquiryAccess")]
    public partial class RestrictMemberOrderAndEnquiryAccess : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM ""AbpPermissions"" AS permission
USING ""AbpRoles"" AS role
WHERE permission.""RoleId"" = role.""Id""
  AND role.""Name"" = 'Member'
  AND permission.""Name"" IN (
      'Aqua.Enquiries.View',
      'Aqua.Orders.View',
      'Pages.Enquiries',
      'Pages.Orders');
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally do not restore broad Area-wide access to customer data.
        }
    }
}
