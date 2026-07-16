using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class BackfillAdministratorManagementPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                WITH "AdministratorPermissions" ("Name", "HostOnly") AS (
                    VALUES
                        ('Aqua.Admin', FALSE),
                        ('Aqua.Admin.Dashboard', FALSE),
                        ('Aqua.Admin.Reports', FALSE),
                        ('Aqua.Admin.Audit', FALSE),
                        ('Aqua.Admin.Settings', FALSE),
                        ('Aqua.Admin.Users', FALSE),
                        ('Aqua.Admin.Users.View', FALSE),
                        ('Aqua.Admin.Users.Create', FALSE),
                        ('Aqua.Admin.Users.Edit', FALSE),
                        ('Aqua.Admin.Users.Delete', FALSE),
                        ('Aqua.Admin.Users.AssignRole', FALSE),
                        ('Aqua.Admin.Users.ResetPassword', FALSE),
                        ('Aqua.Admin.Customers', FALSE),
                        ('Aqua.Admin.Customers.View', FALSE),
                        ('Aqua.Admin.Customers.Create', FALSE),
                        ('Aqua.Admin.Customers.Edit', FALSE),
                        ('Aqua.Admin.Customers.Delete', FALSE),
                        ('Aqua.Admin.Customers.Import', FALSE),
                        ('Aqua.Admin.AreaLeaders', FALSE),
                        ('Aqua.Admin.AreaLeaders.View', FALSE),
                        ('Aqua.Admin.AreaLeaders.Approve', FALSE),
                        ('Aqua.Admin.AreaLeaders.Promote', FALSE),
                        ('Aqua.Admin.AreaLeaders.Demote', FALSE),
                        ('Aqua.Admin.AreaLeaders.Remove', FALSE),
                        ('Aqua.Admin.Facilitators', FALSE),
                        ('Aqua.Admin.Facilitators.View', FALSE),
                        ('Aqua.Admin.Facilitators.Approve', FALSE),
                        ('Aqua.Admin.Facilitators.Promote', FALSE),
                        ('Aqua.Admin.Facilitators.Demote', FALSE),
                        ('Aqua.Admin.Facilitators.Remove', FALSE),
                        ('Aqua.Admin.Members', FALSE),
                        ('Aqua.Admin.Members.View', FALSE),
                        ('Aqua.Admin.Members.Edit', FALSE),
                        ('Aqua.Admin.Members.Suspend', FALSE),
                        ('Aqua.Admin.Members.ChangeTier', FALSE),
                        ('Aqua.Admin.AllTenants', TRUE),
                        ('Aqua.Admin.Tenants', TRUE),
                        ('Aqua.Admin.Tenants.View', TRUE),
                        ('Aqua.Admin.Tenants.Create', TRUE),
                        ('Aqua.Admin.Tenants.Edit', TRUE),
                        ('Aqua.Admin.Tenants.Activate', TRUE),
                        ('Aqua.Admin.Tenants.AssignLeader', TRUE)
                )
                INSERT INTO "AbpPermissions"
                    ("TenantId", "Name", "IsGranted", "Discriminator", "RoleId", "UserId", "CreationTime", "CreatorUserId")
                SELECT
                    role."TenantId",
                    permission."Name",
                    TRUE,
                    'RolePermissionSetting',
                    role."Id",
                    NULL,
                    CURRENT_TIMESTAMP,
                    NULL
                FROM "AbpRoles" AS role
                CROSS JOIN "AdministratorPermissions" AS permission
                WHERE role."IsDeleted" = FALSE
                  AND role."Name" IN ('Admin', 'SystemAdmin')
                  AND (permission."HostOnly" = FALSE OR role."TenantId" IS NULL)
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "AbpPermissions" AS existing
                      WHERE existing."RoleId" = role."Id"
                        AND existing."Name" = permission."Name"
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // This migration repairs existing role grants. Reversing it could remove
            // permissions that an administrator explicitly granted after deployment.
        }
    }
}
