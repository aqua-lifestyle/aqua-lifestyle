using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    /// <inheritdoc />
    public partial class AddUserRoleToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Enquiries_ReferredByFacilitatorId",
                table: "Enquiries");

            migrationBuilder.AddColumn<int>(
                name: "Role",
                table: "AbpUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_ReferredCustomerId",
                table: "Referrals",
                column: "ReferredCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_TenantId_ReferredCustomerId",
                table: "Referrals",
                columns: new[] { "TenantId", "ReferredCustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_TenantId_ReferrerAreaLeaderId",
                table: "Referrals",
                columns: new[] { "TenantId", "ReferrerAreaLeaderId" });

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_TenantId_ReferrerFacilitatorId",
                table: "Referrals",
                columns: new[] { "TenantId", "ReferrerFacilitatorId" });

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_TenantId_SourceEnquiryId",
                table: "Referrals",
                columns: new[] { "TenantId", "SourceEnquiryId" });

            migrationBuilder.CreateIndex(
                name: "IX_Facilitators_CustomerId",
                table: "Facilitators",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Facilitators_TenantId_AreaLeaderId",
                table: "Facilitators",
                columns: new[] { "TenantId", "AreaLeaderId" });

            migrationBuilder.CreateIndex(
                name: "IX_AreaSpaces_TenantId_AreaLeaderId",
                table: "AreaSpaces",
                columns: new[] { "TenantId", "AreaLeaderId" });

            migrationBuilder.CreateIndex(
                name: "IX_AreaSpaces_TenantId_Status",
                table: "AreaSpaces",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AreaLeaders_AreaSpaceId",
                table: "AreaLeaders",
                column: "AreaSpaceId");

            migrationBuilder.CreateIndex(
                name: "IX_AreaLeaders_TenantId_AreaSpaceId",
                table: "AreaLeaders",
                columns: new[] { "TenantId", "AreaSpaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_AreaLeaders_TenantId_CustomerId",
                table: "AreaLeaders",
                columns: new[] { "TenantId", "CustomerId" });

            migrationBuilder.AddForeignKey(
                name: "FK_AreaLeaders_AreaSpaces_AreaSpaceId",
                table: "AreaLeaders",
                column: "AreaSpaceId",
                principalTable: "AreaSpaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AreaLeaders_Customers_CustomerId",
                table: "AreaLeaders",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AreaSpaces_AreaLeaders_AreaLeaderId",
                table: "AreaSpaces",
                column: "AreaLeaderId",
                principalTable: "AreaLeaders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Facilitators_AreaLeaders_AreaLeaderId",
                table: "Facilitators",
                column: "AreaLeaderId",
                principalTable: "AreaLeaders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Facilitators_Customers_CustomerId",
                table: "Facilitators",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Referrals_AreaLeaders_ReferrerAreaLeaderId",
                table: "Referrals",
                column: "ReferrerAreaLeaderId",
                principalTable: "AreaLeaders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Referrals_Customers_ReferredCustomerId",
                table: "Referrals",
                column: "ReferredCustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Referrals_Enquiries_SourceEnquiryId",
                table: "Referrals",
                column: "SourceEnquiryId",
                principalTable: "Enquiries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Referrals_Facilitators_ReferrerFacilitatorId",
                table: "Referrals",
                column: "ReferrerFacilitatorId",
                principalTable: "Facilitators",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AreaLeaders_AreaSpaces_AreaSpaceId",
                table: "AreaLeaders");

            migrationBuilder.DropForeignKey(
                name: "FK_AreaLeaders_Customers_CustomerId",
                table: "AreaLeaders");

            migrationBuilder.DropForeignKey(
                name: "FK_AreaSpaces_AreaLeaders_AreaLeaderId",
                table: "AreaSpaces");

            migrationBuilder.DropForeignKey(
                name: "FK_Facilitators_AreaLeaders_AreaLeaderId",
                table: "Facilitators");

            migrationBuilder.DropForeignKey(
                name: "FK_Facilitators_Customers_CustomerId",
                table: "Facilitators");

            migrationBuilder.DropForeignKey(
                name: "FK_Referrals_AreaLeaders_ReferrerAreaLeaderId",
                table: "Referrals");

            migrationBuilder.DropForeignKey(
                name: "FK_Referrals_Customers_ReferredCustomerId",
                table: "Referrals");

            migrationBuilder.DropForeignKey(
                name: "FK_Referrals_Enquiries_SourceEnquiryId",
                table: "Referrals");

            migrationBuilder.DropForeignKey(
                name: "FK_Referrals_Facilitators_ReferrerFacilitatorId",
                table: "Referrals");

            migrationBuilder.DropIndex(
                name: "IX_Referrals_ReferredCustomerId",
                table: "Referrals");

            migrationBuilder.DropIndex(
                name: "IX_Referrals_TenantId_ReferredCustomerId",
                table: "Referrals");

            migrationBuilder.DropIndex(
                name: "IX_Referrals_TenantId_ReferrerAreaLeaderId",
                table: "Referrals");

            migrationBuilder.DropIndex(
                name: "IX_Referrals_TenantId_ReferrerFacilitatorId",
                table: "Referrals");

            migrationBuilder.DropIndex(
                name: "IX_Referrals_TenantId_SourceEnquiryId",
                table: "Referrals");

            migrationBuilder.DropIndex(
                name: "IX_Facilitators_CustomerId",
                table: "Facilitators");

            migrationBuilder.DropIndex(
                name: "IX_Facilitators_TenantId_AreaLeaderId",
                table: "Facilitators");

            migrationBuilder.DropIndex(
                name: "IX_AreaSpaces_TenantId_AreaLeaderId",
                table: "AreaSpaces");

            migrationBuilder.DropIndex(
                name: "IX_AreaSpaces_TenantId_Status",
                table: "AreaSpaces");

            migrationBuilder.DropIndex(
                name: "IX_AreaLeaders_AreaSpaceId",
                table: "AreaLeaders");

            migrationBuilder.DropIndex(
                name: "IX_AreaLeaders_TenantId_AreaSpaceId",
                table: "AreaLeaders");

            migrationBuilder.DropIndex(
                name: "IX_AreaLeaders_TenantId_CustomerId",
                table: "AreaLeaders");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "AbpUsers");

            migrationBuilder.CreateIndex(
                name: "IX_Enquiries_ReferredByFacilitatorId",
                table: "Enquiries",
                column: "ReferredByFacilitatorId");
        }
    }
}
