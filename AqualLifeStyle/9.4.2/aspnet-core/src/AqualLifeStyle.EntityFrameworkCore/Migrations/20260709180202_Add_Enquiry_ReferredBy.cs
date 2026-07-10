using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    /// <inheritdoc />
    public partial class Add_Enquiry_ReferredBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Enquiries_ReferredByFacilitatorId",
                table: "Enquiries",
                column: "ReferredByFacilitatorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Enquiries_ReferredByFacilitatorId",
                table: "Enquiries");
        }
    }
}
