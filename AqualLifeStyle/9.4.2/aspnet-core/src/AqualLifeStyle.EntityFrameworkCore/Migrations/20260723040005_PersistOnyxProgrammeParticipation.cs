using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    /// <inheritdoc />
    public partial class PersistOnyxProgrammeParticipation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MemberPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    Purpose = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExternalReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    InitiatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_MemberPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberPayments_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EntryParticipations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    RecruiterCustomerId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActivatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RegistrationPaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActivationPaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    TermsVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TermsEffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RegistrationPaymentAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ActivationPaymentAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MonthlyCommitmentAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GracePeriodDays = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
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
                    table.PrimaryKey("PK_EntryParticipations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntryParticipations_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EntryParticipations_Customers_RecruiterCustomerId",
                        column: x => x.RecruiterCustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EntryParticipations_MemberPayments_ActivationPaymentId",
                        column: x => x.ActivationPaymentId,
                        principalTable: "MemberPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EntryParticipations_MemberPayments_RegistrationPaymentId",
                        column: x => x.RegistrationPaymentId,
                        principalTable: "MemberPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EntryRecruiterCorrections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousRecruiterCustomerId = table.Column<int>(type: "integer", nullable: true),
                    NewRecruiterCustomerId = table.Column<int>(type: "integer", nullable: true),
                    AdministratorUserId = table.Column<long>(type: "bigint", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CorrectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EntryParticipationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntryRecruiterCorrections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntryRecruiterCorrections_EntryParticipations_EntryParticip~",
                        column: x => x.EntryParticipationId,
                        principalTable: "EntryParticipations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OnyxParticipations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    RecruiterCustomerId = table.Column<int>(type: "integer", nullable: true),
                    OnyxMembershipId = table.Column<int>(type: "integer", nullable: false),
                    AdmissionRoute = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActivatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DirectEntryPaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntryParticipationId = table.Column<Guid>(type: "uuid", nullable: true),
                    FundingAgreementId = table.Column<Guid>(type: "uuid", nullable: true),
                    TermsVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TermsEffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DirectEntryAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
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
                    table.PrimaryKey("PK_OnyxParticipations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnyxParticipations_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OnyxParticipations_Customers_RecruiterCustomerId",
                        column: x => x.RecruiterCustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OnyxParticipations_EntryParticipations_EntryParticipationId",
                        column: x => x.EntryParticipationId,
                        principalTable: "EntryParticipations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OnyxParticipations_MemberPayments_DirectEntryPaymentId",
                        column: x => x.DirectEntryPaymentId,
                        principalTable: "MemberPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OnyxParticipations_Memberships_OnyxMembershipId",
                        column: x => x.OnyxMembershipId,
                        principalTable: "Memberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EntryParticipations_ActivationPaymentId",
                table: "EntryParticipations",
                column: "ActivationPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_EntryParticipations_CustomerId",
                table: "EntryParticipations",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_EntryParticipations_RecruiterCustomerId",
                table: "EntryParticipations",
                column: "RecruiterCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_EntryParticipations_RegistrationPaymentId",
                table: "EntryParticipations",
                column: "RegistrationPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_EntryParticipations_TenantId_CustomerId",
                table: "EntryParticipations",
                columns: new[] { "TenantId", "CustomerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EntryRecruiterCorrections_EntryParticipationId",
                table: "EntryRecruiterCorrections",
                column: "EntryParticipationId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberPayments_CustomerId",
                table: "MemberPayments",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberPayments_Provider_ExternalReference",
                table: "MemberPayments",
                columns: new[] { "Provider", "ExternalReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemberPayments_TenantId_CustomerId_Purpose",
                table: "MemberPayments",
                columns: new[] { "TenantId", "CustomerId", "Purpose" });

            migrationBuilder.CreateIndex(
                name: "IX_OnyxParticipations_CustomerId",
                table: "OnyxParticipations",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_OnyxParticipations_DirectEntryPaymentId",
                table: "OnyxParticipations",
                column: "DirectEntryPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_OnyxParticipations_EntryParticipationId",
                table: "OnyxParticipations",
                column: "EntryParticipationId");

            migrationBuilder.CreateIndex(
                name: "IX_OnyxParticipations_OnyxMembershipId",
                table: "OnyxParticipations",
                column: "OnyxMembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_OnyxParticipations_RecruiterCustomerId",
                table: "OnyxParticipations",
                column: "RecruiterCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_OnyxParticipations_TenantId_CustomerId",
                table: "OnyxParticipations",
                columns: new[] { "TenantId", "CustomerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EntryRecruiterCorrections");

            migrationBuilder.DropTable(
                name: "OnyxParticipations");

            migrationBuilder.DropTable(
                name: "EntryParticipations");

            migrationBuilder.DropTable(
                name: "MemberPayments");
        }
    }
}
