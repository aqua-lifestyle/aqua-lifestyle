using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    /// <inheritdoc />
    public partial class AddOnyxTravelBenefitEntitlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "HighestCompletedLevel",
                table: "OnyxWeeklyCommissions",
                newName: "HighestQualifiedNetworkLevel");

            migrationBuilder.AddColumn<int>(
                name: "HighestCommissionedLevel",
                table: "OnyxWeeklyCommissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE "OnyxWeeklyCommissions"
                SET "HighestCommissionedLevel" =
                    CASE
                        WHEN "HighestQualifiedNetworkLevel" >= 1 THEN 1
                        ELSE 0
                    END;
                """);

            migrationBuilder.CreateTable(
                name: "OnyxTravelBenefitEntitlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    OnyxParticipationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    QualifiedNetworkLevel = table.Column<int>(type: "integer", nullable: false),
                    RequiredNetworkLevel = table.Column<int>(type: "integer", nullable: false),
                    EligibleAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WaitingPeriodEndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActivatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    WaitingPeriodMonths = table.Column<int>(type: "integer", nullable: false),
                    MemberTripContributionPercent = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    TermsVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TermsEffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_OnyxTravelBenefitEntitlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnyxTravelBenefitEntitlements_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OnyxTravelBenefitEntitlements_OnyxParticipations_OnyxPartic~",
                        column: x => x.OnyxParticipationId,
                        principalTable: "OnyxParticipations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OnyxTravelBenefitEntitlements_CustomerId",
                table: "OnyxTravelBenefitEntitlements",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_OnyxTravelBenefitEntitlements_OnyxParticipationId",
                table: "OnyxTravelBenefitEntitlements",
                column: "OnyxParticipationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnyxTravelBenefitEntitlements_TenantId_Status_WaitingPeriod~",
                table: "OnyxTravelBenefitEntitlements",
                columns: new[] { "TenantId", "Status", "WaitingPeriodEndsAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OnyxTravelBenefitEntitlements");

            migrationBuilder.DropColumn(
                name: "HighestCommissionedLevel",
                table: "OnyxWeeklyCommissions");

            migrationBuilder.RenameColumn(
                name: "HighestQualifiedNetworkLevel",
                table: "OnyxWeeklyCommissions",
                newName: "HighestCompletedLevel");
        }
    }
}
