using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    /// <inheritdoc />
    public partial class UseLiveEntryParticipationUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EntryParticipations_TenantId_CustomerId",
                table: "EntryParticipations");

            migrationBuilder.CreateIndex(
                name: "IX_EntryParticipations_TenantId_CustomerId",
                table: "EntryParticipations",
                columns: new[] { "TenantId", "CustomerId" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Do not partially roll back the independent-root schema. The
            // preceding migration has the same append-only request safeguard;
            // stop here first so the live-row uniqueness predicate remains.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "AQGreenIndependentRootRequests") THEN
                        RAISE EXCEPTION 'AQGREEN_INDEPENDENT_ROOT_HISTORY_PRESENT';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_EntryParticipations_TenantId_CustomerId",
                table: "EntryParticipations");

            migrationBuilder.CreateIndex(
                name: "IX_EntryParticipations_TenantId_CustomerId",
                table: "EntryParticipations",
                columns: new[] { "TenantId", "CustomerId" },
                unique: true);
        }
    }
}
