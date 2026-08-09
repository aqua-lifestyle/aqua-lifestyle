using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    /// <inheritdoc />
    public partial class EnforceSingleProgrammeParticipationDecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "EntryParticipationApprovalDecisions"
                        GROUP BY "EntryParticipationId"
                        HAVING COUNT(*) > 1
                    ) OR EXISTS (
                        SELECT 1
                        FROM "OnyxParticipationApprovalDecisions"
                        GROUP BY "OnyxParticipationId"
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Duplicate programme participation decisions require authorised reconciliation before migration.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_OnyxParticipationApprovalDecisions_OnyxParticipationId",
                table: "OnyxParticipationApprovalDecisions");

            migrationBuilder.DropIndex(
                name: "IX_EntryParticipationApprovalDecisions_EntryParticipationId",
                table: "EntryParticipationApprovalDecisions");

            migrationBuilder.CreateIndex(
                name: "IX_OnyxParticipationApprovalDecisions_OnyxParticipationId",
                table: "OnyxParticipationApprovalDecisions",
                column: "OnyxParticipationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EntryParticipationApprovalDecisions_EntryParticipationId",
                table: "EntryParticipationApprovalDecisions",
                column: "EntryParticipationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OnyxParticipationApprovalDecisions_OnyxParticipationId",
                table: "OnyxParticipationApprovalDecisions");

            migrationBuilder.DropIndex(
                name: "IX_EntryParticipationApprovalDecisions_EntryParticipationId",
                table: "EntryParticipationApprovalDecisions");

            migrationBuilder.CreateIndex(
                name: "IX_OnyxParticipationApprovalDecisions_OnyxParticipationId",
                table: "OnyxParticipationApprovalDecisions",
                column: "OnyxParticipationId");

            migrationBuilder.CreateIndex(
                name: "IX_EntryParticipationApprovalDecisions_EntryParticipationId",
                table: "EntryParticipationApprovalDecisions",
                column: "EntryParticipationId");
        }
    }
}
