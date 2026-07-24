using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    /// <inheritdoc />
    public partial class AddEntryCommissionLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EntryCommissionPeriods",
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
                    table.PrimaryKey("PK_EntryCommissionPeriods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EntryWeeklyCommissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    EntryParticipationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    CommissionPeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    HighestCompletedLevel = table.Column<int>(type: "integer", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    RulesVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PayoutStatus = table.Column<int>(type: "integer", nullable: false),
                    HoldReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_EntryWeeklyCommissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntryWeeklyCommissions_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EntryWeeklyCommissions_EntryCommissionPeriods_CommissionPer~",
                        column: x => x.CommissionPeriodId,
                        principalTable: "EntryCommissionPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EntryWeeklyCommissions_EntryParticipations_EntryParticipati~",
                        column: x => x.EntryParticipationId,
                        principalTable: "EntryParticipations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EntryCommissionComponents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EntryWeeklyCommissionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntryCommissionComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntryCommissionComponents_EntryWeeklyCommissions_EntryWeekl~",
                        column: x => x.EntryWeeklyCommissionId,
                        principalTable: "EntryWeeklyCommissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EntryCommissionComponents_EntryWeeklyCommissionId_Level",
                table: "EntryCommissionComponents",
                columns: new[] { "EntryWeeklyCommissionId", "Level" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EntryCommissionPeriods_TenantId_PeriodStart_PeriodEnd",
                table: "EntryCommissionPeriods",
                columns: new[] { "TenantId", "PeriodStart", "PeriodEnd" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EntryWeeklyCommissions_CommissionPeriodId",
                table: "EntryWeeklyCommissions",
                column: "CommissionPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_EntryWeeklyCommissions_CustomerId",
                table: "EntryWeeklyCommissions",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_EntryWeeklyCommissions_EntryParticipationId_CommissionPerio~",
                table: "EntryWeeklyCommissions",
                columns: new[] { "EntryParticipationId", "CommissionPeriodId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EntryWeeklyCommissions_TenantId_CustomerId_PayoutStatus",
                table: "EntryWeeklyCommissions",
                columns: new[] { "TenantId", "CustomerId", "PayoutStatus" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EntryCommissionComponents");

            migrationBuilder.DropTable(
                name: "EntryWeeklyCommissions");

            migrationBuilder.DropTable(
                name: "EntryCommissionPeriods");
        }
    }
}
