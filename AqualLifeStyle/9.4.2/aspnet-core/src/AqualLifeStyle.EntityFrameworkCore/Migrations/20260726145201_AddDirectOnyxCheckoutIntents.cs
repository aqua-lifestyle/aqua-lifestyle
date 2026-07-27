using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectOnyxCheckoutIntents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DirectOnyxCheckoutIntents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    RecruiterCustomerId = table.Column<int>(type: "integer", nullable: true),
                    InviteCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    OnyxMembershipId = table.Column<int>(type: "integer", nullable: false),
                    TermsVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TermsEffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ProviderCheckoutId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CheckoutUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CheckoutCreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ParticipationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_DirectOnyxCheckoutIntents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DirectOnyxCheckoutIntents_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DirectOnyxCheckoutIntents_Customers_RecruiterCustomerId",
                        column: x => x.RecruiterCustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DirectOnyxCheckoutIntents_MemberPayments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "MemberPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DirectOnyxCheckoutIntents_Memberships_OnyxMembershipId",
                        column: x => x.OnyxMembershipId,
                        principalTable: "Memberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DirectOnyxCheckoutIntents_OnyxParticipations_ParticipationId",
                        column: x => x.ParticipationId,
                        principalTable: "OnyxParticipations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Earlier application versions created a direct-Onyx participation before
            // collecting payment. Preserve the customer's intended placement as a
            // checkout intent, then remove that premature participation. Active and
            // AQGreen-graduated Onyx records are intentionally untouched.
            migrationBuilder.Sql(
                """
                INSERT INTO "DirectOnyxCheckoutIntents" (
                    "Id", "TenantId", "CustomerId", "RecruiterCustomerId",
                    "InviteCode", "OnyxMembershipId", "TermsVersion",
                    "TermsEffectiveFrom", "Amount", "Currency", "Status",
                    "ProviderCheckoutId", "CheckoutUrl", "CreatedAt",
                    "CheckoutCreatedAt", "PaymentId", "ParticipationId",
                    "CompletedAt", "CreationTime", "CreatorUserId",
                    "LastModificationTime", "LastModifierUserId", "IsDeleted",
                    "DeleterUserId", "DeletionTime")
                SELECT
                    op."Id", op."TenantId", op."CustomerId", op."RecruiterCustomerId",
                    NULL, op."OnyxMembershipId", op."TermsVersion",
                    op."TermsEffectiveFrom", op."DirectEntryAmount", op."Currency", 0,
                    NULL, NULL, op."StartedAt", NULL, NULL, NULL, NULL,
                    op."CreationTime", op."CreatorUserId", op."LastModificationTime",
                    op."LastModifierUserId", FALSE, NULL, NULL
                FROM "OnyxParticipations" op
                WHERE op."AdmissionRoute" = 0
                  AND op."Status" = 0
                  AND op."IsDeleted" = FALSE;

                DELETE FROM "OnyxParticipations"
                WHERE "AdmissionRoute" = 0
                  AND "Status" = 0
                  AND "IsDeleted" = FALSE;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_DirectOnyxCheckoutIntents_CustomerId",
                table: "DirectOnyxCheckoutIntents",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectOnyxCheckoutIntents_OnyxMembershipId",
                table: "DirectOnyxCheckoutIntents",
                column: "OnyxMembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectOnyxCheckoutIntents_ParticipationId",
                table: "DirectOnyxCheckoutIntents",
                column: "ParticipationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DirectOnyxCheckoutIntents_PaymentId",
                table: "DirectOnyxCheckoutIntents",
                column: "PaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DirectOnyxCheckoutIntents_ProviderCheckoutId",
                table: "DirectOnyxCheckoutIntents",
                column: "ProviderCheckoutId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DirectOnyxCheckoutIntents_RecruiterCustomerId",
                table: "DirectOnyxCheckoutIntents",
                column: "RecruiterCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectOnyxCheckoutIntents_TenantId_CustomerId",
                table: "DirectOnyxCheckoutIntents",
                columns: new[] { "TenantId", "CustomerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "The direct Onyx checkout-intent migration cannot be rolled back. " +
                "It migrates existing premature Onyx participations into checkout intents and deletes " +
                "the original participation rows, so the pre-migration state cannot be restored.");
        }
    }
}
