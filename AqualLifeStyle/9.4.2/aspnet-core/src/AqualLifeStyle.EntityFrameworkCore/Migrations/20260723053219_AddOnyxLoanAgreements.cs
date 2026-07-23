using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    /// <inheritdoc />
    public partial class AddOnyxLoanAgreements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FundingAgreementId",
                table: "OnyxParticipations",
                newName: "LoanAgreementId");

            migrationBuilder.CreateTable(
                name: "OnyxLoanAgreements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    EntryParticipationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TermsVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PrincipalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    InterestRatePercent = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    TotalPayableAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OutstandingAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    RepaymentPeriodMonths = table.Column<int>(type: "integer", nullable: false),
                    InitialWeeklyRequirementCount = table.Column<int>(type: "integer", nullable: false),
                    InitialWeeklyMinimumAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OfferedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MemberAcceptedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    MemberConfirmation = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    MemberAcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedByAdministratorUserId = table.Column<long>(type: "bigint", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EffectiveAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RepaymentDeadlineAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastAssessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SettledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_OnyxLoanAgreements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnyxLoanAgreements_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OnyxLoanAgreements_EntryParticipations_EntryParticipationId",
                        column: x => x.EntryParticipationId,
                        principalTable: "EntryParticipations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OnyxLoanRepaymentAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    WeeklyRequirementNumber = table.Column<int>(type: "integer", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OnyxLoanAgreementId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnyxLoanRepaymentAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnyxLoanRepaymentAllocations_MemberPayments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "MemberPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OnyxLoanRepaymentAllocations_OnyxLoanAgreements_OnyxLoanAgr~",
                        column: x => x.OnyxLoanAgreementId,
                        principalTable: "OnyxLoanAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OnyxLoanWeeklyRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequirementNumber = table.Column<int>(type: "integer", nullable: false),
                    MinimumAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreditedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SatisfiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MarkedOverdueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OnyxLoanAgreementId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnyxLoanWeeklyRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnyxLoanWeeklyRequirements_OnyxLoanAgreements_OnyxLoanAgree~",
                        column: x => x.OnyxLoanAgreementId,
                        principalTable: "OnyxLoanAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OnyxParticipations_LoanAgreementId",
                table: "OnyxParticipations",
                column: "LoanAgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_OnyxLoanAgreements_CustomerId",
                table: "OnyxLoanAgreements",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_OnyxLoanAgreements_EntryParticipationId",
                table: "OnyxLoanAgreements",
                column: "EntryParticipationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnyxLoanAgreements_TenantId_CustomerId_Status",
                table: "OnyxLoanAgreements",
                columns: new[] { "TenantId", "CustomerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OnyxLoanRepaymentAllocations_OnyxLoanAgreementId",
                table: "OnyxLoanRepaymentAllocations",
                column: "OnyxLoanAgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_OnyxLoanRepaymentAllocations_PaymentId",
                table: "OnyxLoanRepaymentAllocations",
                column: "PaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnyxLoanWeeklyRequirements_OnyxLoanAgreementId_RequirementN~",
                table: "OnyxLoanWeeklyRequirements",
                columns: new[] { "OnyxLoanAgreementId", "RequirementNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_OnyxParticipations_OnyxLoanAgreements_LoanAgreementId",
                table: "OnyxParticipations",
                column: "LoanAgreementId",
                principalTable: "OnyxLoanAgreements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OnyxParticipations_OnyxLoanAgreements_LoanAgreementId",
                table: "OnyxParticipations");

            migrationBuilder.DropTable(
                name: "OnyxLoanRepaymentAllocations");

            migrationBuilder.DropTable(
                name: "OnyxLoanWeeklyRequirements");

            migrationBuilder.DropTable(
                name: "OnyxLoanAgreements");

            migrationBuilder.DropIndex(
                name: "IX_OnyxParticipations_LoanAgreementId",
                table: "OnyxParticipations");

            migrationBuilder.RenameColumn(
                name: "LoanAgreementId",
                table: "OnyxParticipations",
                newName: "FundingAgreementId");
        }
    }
}
