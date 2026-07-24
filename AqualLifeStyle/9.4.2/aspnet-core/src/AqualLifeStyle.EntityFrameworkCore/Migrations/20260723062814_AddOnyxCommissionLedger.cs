using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    /// <inheritdoc />
    public partial class AddOnyxCommissionLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OnyxCommissionPeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RulesVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
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
                    table.PrimaryKey("PK_OnyxCommissionPeriods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OnyxWeeklyCommissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    OnyxParticipationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    CommissionPeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    HighestCompletedLevel = table.Column<int>(type: "integer", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    RulesVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PayoutStatus = table.Column<int>(type: "integer", nullable: false),
                    ReleasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReleaseReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaymentReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
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
                    table.PrimaryKey("PK_OnyxWeeklyCommissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnyxWeeklyCommissions_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OnyxWeeklyCommissions_OnyxCommissionPeriods_CommissionPerio~",
                        column: x => x.CommissionPeriodId,
                        principalTable: "OnyxCommissionPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OnyxWeeklyCommissions_OnyxParticipations_OnyxParticipationId",
                        column: x => x.OnyxParticipationId,
                        principalTable: "OnyxParticipations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OnyxCommissionComponents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OnyxWeeklyCommissionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnyxCommissionComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnyxCommissionComponents_OnyxWeeklyCommissions_OnyxWeeklyCo~",
                        column: x => x.OnyxWeeklyCommissionId,
                        principalTable: "OnyxWeeklyCommissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OnyxCommissionComponents_OnyxWeeklyCommissionId_Level",
                table: "OnyxCommissionComponents",
                columns: new[] { "OnyxWeeklyCommissionId", "Level" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnyxCommissionPeriods_TenantId_PeriodStart_PeriodEnd",
                table: "OnyxCommissionPeriods",
                columns: new[] { "TenantId", "PeriodStart", "PeriodEnd" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnyxWeeklyCommissions_CommissionPeriodId",
                table: "OnyxWeeklyCommissions",
                column: "CommissionPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_OnyxWeeklyCommissions_CustomerId",
                table: "OnyxWeeklyCommissions",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_OnyxWeeklyCommissions_OnyxParticipationId_CommissionPeriodId",
                table: "OnyxWeeklyCommissions",
                columns: new[] { "OnyxParticipationId", "CommissionPeriodId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnyxWeeklyCommissions_TenantId_CustomerId_PayoutStatus",
                table: "OnyxWeeklyCommissions",
                columns: new[] { "TenantId", "CustomerId", "PayoutStatus" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OnyxCommissionComponents");

            migrationBuilder.DropTable(
                name: "OnyxWeeklyCommissions");

            migrationBuilder.DropTable(
                name: "OnyxCommissionPeriods");
        }
    }
}
