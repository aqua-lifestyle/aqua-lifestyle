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
