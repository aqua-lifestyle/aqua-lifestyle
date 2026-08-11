using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    /// <inheritdoc />
    public partial class SeparateAreaFromTenantBoundary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AreaId",
                table: "Customers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Areas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Areas", x => x.Id);
                    table.UniqueConstraint("AK_Areas_TenantId_Id", x => new { x.TenantId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "AreaAdminAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    AreaId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_AreaAdminAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AreaAdminAssignments_AbpUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AbpUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AreaAdminAssignments_Areas_TenantId_AreaId",
                        columns: x => new { x.TenantId, x.AreaId },
                        principalTable: "Areas",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerAreaAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    AreaId = table.Column<Guid>(type: "uuid", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsMigrationBaseline = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
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
                    table.PrimaryKey("PK_CustomerAreaAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerAreaAssignments_Areas_TenantId_AreaId",
                        columns: x => new { x.TenantId, x.AreaId },
                        principalTable: "Areas",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerAreaAssignments_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // This is a system-introduction baseline, not a claim about when
            // Johannesburg became a business Area or when a member moved there.
            // The owner-authorised production mapping applies only to the
            // existing technical Tenant named Default. Future Tenants must
            // provision their own Areas deliberately.
            migrationBuilder.Sql(
                """
                INSERT INTO "Areas" (
                    "Id", "TenantId", "Name", "Code", "IsActive",
                    "CreationTime", "IsDeleted")
                SELECT
                    'a0000000-0000-0000-0000-000000000001'::uuid,
                    tenant."Id",
                    'Johannesburg',
                    'JHB',
                    TRUE,
                    TIMESTAMPTZ '2026-08-11 15:02:51+00',
                    FALSE
                FROM "AbpTenants" tenant
                WHERE tenant."TenancyName" = 'Default'
                  AND NOT tenant."IsDeleted"
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "Areas" existing
                      WHERE existing."TenantId" = tenant."Id"
                        AND existing."Code" = 'JHB');

                UPDATE "Customers" customer
                SET "AreaId" = area."Id"
                FROM "AbpTenants" tenant, "Areas" area
                WHERE tenant."TenancyName" = 'Default'
                  AND NOT tenant."IsDeleted"
                  AND area."TenantId" = tenant."Id"
                  AND area."Code" = 'JHB'
                  AND customer."TenantId" = tenant."Id"
                  AND customer."AreaId" IS NULL;

                INSERT INTO "CustomerAreaAssignments" (
                    "Id", "TenantId", "CustomerId", "AreaId",
                    "EffectiveFrom", "EffectiveTo", "IsMigrationBaseline",
                    "Reason", "CreationTime", "IsDeleted")
                SELECT
                    md5('area-baseline:' || customer."Id"::text)::uuid,
                    customer."TenantId",
                    customer."Id",
                    area."Id",
                    TIMESTAMPTZ '2026-08-11 15:02:51+00',
                    NULL,
                    TRUE,
                    'Owner-authorised Johannesburg system introduction baseline',
                    TIMESTAMPTZ '2026-08-11 15:02:51+00',
                    FALSE
                FROM "Customers" customer
                JOIN "AbpTenants" tenant
                  ON tenant."Id" = customer."TenantId"
                 AND tenant."TenancyName" = 'Default'
                 AND NOT tenant."IsDeleted"
                JOIN "Areas" area
                  ON area."TenantId" = tenant."Id"
                 AND area."Code" = 'JHB'
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM "CustomerAreaAssignments" existing
                    WHERE existing."TenantId" = customer."TenantId"
                      AND existing."CustomerId" = customer."Id"
                      AND existing."EffectiveTo" IS NULL);

                INSERT INTO "AreaAdminAssignments" (
                    "Id", "TenantId", "AreaId", "UserId", "EffectiveFrom",
                    "RevokedAt", "CreationTime", "IsDeleted")
                SELECT
                    md5('area-admin-baseline:' || user_role."UserId"::text)::uuid,
                    tenant."Id",
                    area."Id",
                    user_role."UserId",
                    TIMESTAMPTZ '2026-08-11 15:02:51+00',
                    NULL,
                    TIMESTAMPTZ '2026-08-11 15:02:51+00',
                    FALSE
                FROM "AbpTenants" tenant
                JOIN "Areas" area
                  ON area."TenantId" = tenant."Id"
                 AND area."Code" = 'JHB'
                JOIN "AbpUserRoles" user_role
                  ON user_role."TenantId" = tenant."Id"
                JOIN "AbpRoles" role
                  ON role."Id" = user_role."RoleId"
                 AND role."TenantId" = tenant."Id"
                 AND role."Name" IN ('Admin', 'SystemAdmin')
                 AND NOT role."IsDeleted"
                JOIN "AbpUsers" administrator
                  ON administrator."Id" = user_role."UserId"
                 AND administrator."TenantId" = tenant."Id"
                 AND administrator."IsActive"
                 AND NOT administrator."IsDeleted"
                WHERE tenant."TenancyName" = 'Default'
                  AND NOT tenant."IsDeleted"
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "AreaAdminAssignments" existing
                      WHERE existing."TenantId" = tenant."Id"
                        AND existing."AreaId" = area."Id"
                        AND existing."UserId" = user_role."UserId"
                        AND existing."RevokedAt" IS NULL);

                DO $area_validation$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "EntryParticipations" participation
                        JOIN "AbpTenants" tenant
                          ON tenant."Id" = participation."TenantId"
                         AND tenant."TenancyName" = 'Default'
                        LEFT JOIN "Customers" customer
                          ON customer."Id" = participation."CustomerId"
                         AND customer."TenantId" = participation."TenantId"
                        WHERE customer."Id" IS NULL OR customer."AreaId" IS NULL)
                    THEN
                        RAISE EXCEPTION 'Area baseline failed: an AQGreen participation has no same-Tenant Customer Area assignment.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM "OnyxParticipations" participation
                        JOIN "AbpTenants" tenant
                          ON tenant."Id" = participation."TenantId"
                         AND tenant."TenancyName" = 'Default'
                        LEFT JOIN "Customers" customer
                          ON customer."Id" = participation."CustomerId"
                         AND customer."TenantId" = participation."TenantId"
                        WHERE customer."Id" IS NULL OR customer."AreaId" IS NULL)
                    THEN
                        RAISE EXCEPTION 'Area baseline failed: an Onyx participation has no same-Tenant Customer Area assignment.';
                    END IF;
                END
                $area_validation$;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId_AreaId",
                table: "Customers",
                columns: new[] { "TenantId", "AreaId" });

            migrationBuilder.CreateIndex(
                name: "IX_AreaAdminAssignments_TenantId_AreaId_UserId",
                table: "AreaAdminAssignments",
                columns: new[] { "TenantId", "AreaId", "UserId" },
                unique: true,
                filter: "\"RevokedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AreaAdminAssignments_UserId",
                table: "AreaAdminAssignments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Areas_TenantId_Code",
                table: "Areas",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAreaAssignments_CustomerId",
                table: "CustomerAreaAssignments",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAreaAssignments_TenantId_AreaId",
                table: "CustomerAreaAssignments",
                columns: new[] { "TenantId", "AreaId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAreaAssignments_TenantId_CustomerId",
                table: "CustomerAreaAssignments",
                columns: new[] { "TenantId", "CustomerId" },
                unique: true,
                filter: "\"EffectiveTo\" IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_Areas_TenantId_AreaId",
                table: "Customers",
                columns: new[] { "TenantId", "AreaId" },
                principalTable: "Areas",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_Areas_TenantId_AreaId",
                table: "Customers");

            migrationBuilder.DropTable(
                name: "AreaAdminAssignments");

            migrationBuilder.DropTable(
                name: "CustomerAreaAssignments");

            migrationBuilder.DropTable(
                name: "Areas");

            migrationBuilder.DropIndex(
                name: "IX_Customers_TenantId_AreaId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "AreaId",
                table: "Customers");
        }
    }
}
