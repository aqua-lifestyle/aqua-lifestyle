using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    /// <inheritdoc />
    public partial class AddAQGreenMonthlyObligationCheckouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.Sql("""
                    DO $block$
                    BEGIN
                        IF EXISTS (
                            SELECT "PaymentId"
                            FROM "EntryMonthlyObligations"
                            WHERE "PaymentId" IS NOT NULL
                            GROUP BY "PaymentId"
                            HAVING COUNT(*) > 1)
                        THEN
                            RAISE EXCEPTION 'Cannot enforce one AQGreen monthly obligation per payment while duplicate payment associations exist.';
                        END IF;
                    END;
                    $block$;
                    """);
            }

            migrationBuilder.DropIndex(
                name: "IX_EntryMonthlyObligations_PaymentId",
                table: "EntryMonthlyObligations");

            migrationBuilder.CreateTable(
                name: "AQGreenMonthlyObligationCheckouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryMonthlyObligationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryParticipationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodYear = table.Column<int>(type: "integer", nullable: false),
                    PeriodMonth = table.Column<int>(type: "integer", nullable: false),
                    AllocationStatus = table.Column<int>(type: "integer", nullable: false),
                    AllocationEvidence = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorUserId = table.Column<long>(type: "bigint", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierUserId = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeleterUserId = table.Column<long>(type: "bigint", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    TerminatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TerminatedByAdministratorUserId = table.Column<long>(type: "bigint", nullable: true),
                    TerminalEvidence = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AQGreenMonthlyObligationCheckouts", x => x.Id);
                    table.CheckConstraint("CK_AQGreenMonthlyObligationCheckouts_AllocationResult", "(\"AllocationStatus\" = 0 AND \"Status\" IN (0, 1, 3, 4, 5) AND \"PaymentId\" IS NULL AND \"AllocationEvidence\" IS NULL) OR (\"AllocationStatus\" = 1 AND \"PaymentId\" IS NOT NULL AND \"Status\" = 2 AND \"AllocationEvidence\" IS NULL) OR (\"AllocationStatus\" = 2 AND \"PaymentId\" IS NOT NULL AND \"Status\" = 2 AND length(trim(\"AllocationEvidence\")) > 0)");
                    table.CheckConstraint("CK_AQGreenMonthlyObligationCheckouts_AllocationStatus", "\"AllocationStatus\" >= 0 AND \"AllocationStatus\" <= 2");
                    table.CheckConstraint("CK_AQGreenMonthlyObligationCheckouts_PeriodMonth", "\"PeriodMonth\" >= 1 AND \"PeriodMonth\" <= 12");
                    table.CheckConstraint("CK_AQGreenMonthlyObligationCheckouts_PeriodYear", "\"PeriodYear\" >= 2000 AND \"PeriodYear\" <= 9999");
                    table.CheckConstraint("CK_AQGreenMonthlyObligationCheckouts_Status", "\"Status\" >= 0 AND \"Status\" <= 5");
                    table.ForeignKey(
                        name: "FK_AQGreenMonthlyObligationCheckouts_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AQGreenMonthlyObligationCheckouts_EntryMonthlyObligations_E~",
                        column: x => x.EntryMonthlyObligationId,
                        principalTable: "EntryMonthlyObligations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AQGreenMonthlyObligationCheckouts_EntryParticipations_Entry~",
                        column: x => x.EntryParticipationId,
                        principalTable: "EntryParticipations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AQGreenMonthlyObligationCheckouts_MemberPayments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "MemberPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EntryMonthlyObligations_PaymentId",
                table: "EntryMonthlyObligations",
                column: "PaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AQGreenMonthlyObligationCheckouts_CustomerId",
                table: "AQGreenMonthlyObligationCheckouts",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_AQGreenMonthlyObligationCheckouts_EntryMonthlyObligationId",
                table: "AQGreenMonthlyObligationCheckouts",
                column: "EntryMonthlyObligationId",
                unique: true,
                filter: "\"Status\" IN (0, 1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_AQGreenMonthlyObligationCheckouts_EntryParticipationId",
                table: "AQGreenMonthlyObligationCheckouts",
                column: "EntryParticipationId");

            migrationBuilder.CreateIndex(
                name: "IX_AQGreenMonthlyObligationCheckouts_PaymentId",
                table: "AQGreenMonthlyObligationCheckouts",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_AQGreenMonthlyObligationCheckouts_ProviderCheckoutId",
                table: "AQGreenMonthlyObligationCheckouts",
                column: "ProviderCheckoutId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AQGreenMonthlyObligationCheckouts_TenantId_CustomerId_Status",
                table: "AQGreenMonthlyObligationCheckouts",
                columns: new[] { "TenantId", "CustomerId", "Status" });

            migrationBuilder.Sql(
                """
                INSERT INTO "AbpPermissions"
                    ("TenantId", "Name", "IsGranted", "Discriminator", "RoleId", "UserId", "CreationTime", "CreatorUserId")
                SELECT
                    role."TenantId",
                    'Aqua.EntryMonthlyObligations.Pay',
                    TRUE,
                    'RolePermissionSetting',
                    role."Id",
                    NULL,
                    CURRENT_TIMESTAMP,
                    NULL
                FROM "AbpRoles" AS role
                WHERE role."IsDeleted" = FALSE
                  AND role."Name" = 'Member'
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "AbpPermissions" AS existing
                      WHERE existing."RoleId" = role."Id"
                        AND existing."Name" = 'Aqua.EntryMonthlyObligations.Pay'
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.Sql("""
                    DO $block$
                    BEGIN
                        IF EXISTS (SELECT 1 FROM "AQGreenMonthlyObligationCheckouts")
                        THEN
                            RAISE EXCEPTION 'Cannot remove AQGreen monthly checkout schema after checkout evidence has been recorded.';
                        END IF;
                    END;
                    $block$;
                    """);
            }

            migrationBuilder.DropTable(
                name: "AQGreenMonthlyObligationCheckouts");

            migrationBuilder.DropIndex(
                name: "IX_EntryMonthlyObligations_PaymentId",
                table: "EntryMonthlyObligations");

            migrationBuilder.CreateIndex(
                name: "IX_EntryMonthlyObligations_PaymentId",
                table: "EntryMonthlyObligations",
                column: "PaymentId");
        }
    }
}
