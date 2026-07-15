using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantAreaLeaderAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AreaLeaderId",
                table: "AbpTenants",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AbpTenants_AreaLeaderId",
                table: "AbpTenants",
                column: "AreaLeaderId");

            migrationBuilder.AddForeignKey(
                name: "FK_AbpTenants_AreaLeaders_AreaLeaderId",
                table: "AbpTenants",
                column: "AreaLeaderId",
                principalTable: "AreaLeaders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AbpTenants_AreaLeaders_AreaLeaderId",
                table: "AbpTenants");

            migrationBuilder.DropIndex(
                name: "IX_AbpTenants_AreaLeaderId",
                table: "AbpTenants");

            migrationBuilder.DropColumn(
                name: "AreaLeaderId",
                table: "AbpTenants");
        }
    }
}
