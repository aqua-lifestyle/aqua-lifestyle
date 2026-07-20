using AqualLifeStyle.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    [DbContext(typeof(AqualLifeStyleDbContext))]
    [Migration("20260711123000_AddFacilitatorTenantCustomerUniqueIndex")]
    /// <inheritdoc />
    public partial class AddFacilitatorTenantCustomerUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Facilitators_CustomerId",
                table: "Facilitators");

            migrationBuilder.CreateIndex(
                name: "IX_Facilitators_TenantId_CustomerId",
                table: "Facilitators",
                columns: new[] { "TenantId", "CustomerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Facilitators_TenantId_CustomerId",
                table: "Facilitators");

            migrationBuilder.CreateIndex(
                name: "IX_Facilitators_CustomerId",
                table: "Facilitators",
                column: "CustomerId");
        }
    }
}
