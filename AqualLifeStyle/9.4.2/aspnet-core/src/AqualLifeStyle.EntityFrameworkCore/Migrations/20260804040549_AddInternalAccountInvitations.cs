using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    /// <inheritdoc />
    public partial class AddInternalAccountInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InternalAccountInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    InvitedEmailAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PublicCodeHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    SetupTokenHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EmailConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    RevocationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PreviousInvitationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InternalAccountInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InternalAccountInvitations_AbpUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AbpUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InternalAccountInvitations_InternalAccountInvitations_Previ~",
                        column: x => x.PreviousInvitationId,
                        principalTable: "InternalAccountInvitations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InternalAccountInvitations_PreviousInvitationId",
                table: "InternalAccountInvitations",
                column: "PreviousInvitationId");

            migrationBuilder.CreateIndex(
                name: "IX_InternalAccountInvitations_PublicCodeHash",
                table: "InternalAccountInvitations",
                column: "PublicCodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InternalAccountInvitations_TenantId_UserId",
                table: "InternalAccountInvitations",
                columns: new[] { "TenantId", "UserId" },
                unique: true,
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_InternalAccountInvitations_TenantId_UserId_CreationTime",
                table: "InternalAccountInvitations",
                columns: new[] { "TenantId", "UserId", "CreationTime" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_InternalAccountInvitations_UserId",
                table: "InternalAccountInvitations",
                column: "UserId");

            migrationBuilder.Sql(
                """
                INSERT INTO "AbpPermissions"
                    ("TenantId", "Name", "IsGranted", "Discriminator", "RoleId", "UserId", "CreationTime", "CreatorUserId")
                SELECT
                    role."TenantId",
                    'Aqua.Admin.Users.Invite',
                    TRUE,
                    'RolePermissionSetting',
                    role."Id",
                    NULL,
                    CURRENT_TIMESTAMP,
                    NULL
                FROM "AbpRoles" AS role
                WHERE role."IsDeleted" = FALSE
                  AND role."Name" IN ('Admin', 'SystemAdmin')
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "AbpPermissions" AS existing
                      WHERE existing."RoleId" = role."Id"
                        AND existing."Name" = 'Aqua.Admin.Users.Invite'
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InternalAccountInvitations");

            // Role-permission rows have no migration-origin marker. Removing them could
            // revoke a grant explicitly retained or recreated after this migration.
        }
    }
}
