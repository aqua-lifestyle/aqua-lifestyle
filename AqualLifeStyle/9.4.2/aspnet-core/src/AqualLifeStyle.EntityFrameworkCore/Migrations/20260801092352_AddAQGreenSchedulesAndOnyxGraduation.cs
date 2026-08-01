using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    /// <inheritdoc />
    public partial class AddAQGreenSchedulesAndOnyxGraduation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AQGreenJoiningCheckouts_ParticipationId",
                table: "AQGreenJoiningCheckouts");

            migrationBuilder.AddColumn<decimal>(
                name: "JoiningInstallmentAmount",
                table: "EntryParticipations",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "JoiningPaymentSchedule",
                table: "EntryParticipations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TerminalEvidence",
                table: "DirectOnyxCheckoutIntents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TerminatedAt",
                table: "DirectOnyxCheckoutIntents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TerminatedByAdministratorUserId",
                table: "DirectOnyxCheckoutIntents",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Schedule",
                table: "AQGreenJoiningCheckouts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Stage",
                table: "AQGreenJoiningCheckouts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TerminalEvidence",
                table: "AQGreenJoiningCheckouts",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TerminatedAt",
                table: "AQGreenJoiningCheckouts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TerminatedByAdministratorUserId",
                table: "AQGreenJoiningCheckouts",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OnyxGraduationDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    EntryParticipationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoanAgreementId = table.Column<Guid>(type: "uuid", nullable: false),
                    OnyxParticipationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdministratorUserId = table.Column<long>(type: "bigint", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Justification = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    EvaluatedNetworkLevel = table.Column<int>(type: "integer", nullable: false),
                    AQGreenWasActive = table.Column<bool>(type: "boolean", nullable: false),
                    LoanWasActive = table.Column<bool>(type: "boolean", nullable: false),
                    LoanWasAccepted = table.Column<bool>(type: "boolean", nullable: false),
                    LoanWasAdministratorApproved = table.Column<bool>(type: "boolean", nullable: false),
                    EvaluatedFundingAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EvaluatedFundingCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnyxGraduationDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnyxGraduationDecisions_EntryParticipations_EntryParticipat~",
                        column: x => x.EntryParticipationId,
                        principalTable: "EntryParticipations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OnyxGraduationDecisions_OnyxLoanAgreements_LoanAgreementId",
                        column: x => x.LoanAgreementId,
                        principalTable: "OnyxLoanAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OnyxGraduationDecisions_OnyxParticipations_OnyxParticipatio~",
                        column: x => x.OnyxParticipationId,
                        principalTable: "OnyxParticipations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AQGreenJoiningCheckouts_ParticipationId",
                table: "AQGreenJoiningCheckouts",
                column: "ParticipationId",
                unique: true,
                filter: "\"Status\" IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "IX_OnyxGraduationDecisions_EntryParticipationId",
                table: "OnyxGraduationDecisions",
                column: "EntryParticipationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnyxGraduationDecisions_LoanAgreementId",
                table: "OnyxGraduationDecisions",
                column: "LoanAgreementId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnyxGraduationDecisions_OnyxParticipationId",
                table: "OnyxGraduationDecisions",
                column: "OnyxParticipationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnyxGraduationDecisions_TenantId_CustomerId",
                table: "OnyxGraduationDecisions",
                columns: new[] { "TenantId", "CustomerId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OnyxGraduationDecisions");

            migrationBuilder.DropIndex(
                name: "IX_AQGreenJoiningCheckouts_ParticipationId",
                table: "AQGreenJoiningCheckouts");

            migrationBuilder.DropColumn(
                name: "JoiningInstallmentAmount",
                table: "EntryParticipations");

            migrationBuilder.DropColumn(
                name: "JoiningPaymentSchedule",
                table: "EntryParticipations");

            migrationBuilder.DropColumn(
                name: "TerminalEvidence",
                table: "DirectOnyxCheckoutIntents");

            migrationBuilder.DropColumn(
                name: "TerminatedAt",
                table: "DirectOnyxCheckoutIntents");

            migrationBuilder.DropColumn(
                name: "TerminatedByAdministratorUserId",
                table: "DirectOnyxCheckoutIntents");

            migrationBuilder.DropColumn(
                name: "Schedule",
                table: "AQGreenJoiningCheckouts");

            migrationBuilder.DropColumn(
                name: "Stage",
                table: "AQGreenJoiningCheckouts");

            migrationBuilder.DropColumn(
                name: "TerminalEvidence",
                table: "AQGreenJoiningCheckouts");

            migrationBuilder.DropColumn(
                name: "TerminatedAt",
                table: "AQGreenJoiningCheckouts");

            migrationBuilder.DropColumn(
                name: "TerminatedByAdministratorUserId",
                table: "AQGreenJoiningCheckouts");

            migrationBuilder.CreateIndex(
                name: "IX_AQGreenJoiningCheckouts_ParticipationId",
                table: "AQGreenJoiningCheckouts",
                column: "ParticipationId",
                unique: true);
        }
    }
}
