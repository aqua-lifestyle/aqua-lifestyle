using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    /// <inheritdoc />
    public partial class Add_AreaNetwork_Facilitators : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReferredByFacilitatorId",
                table: "Enquiries",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Facilitators",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    AreaLeaderId = table.Column<int>(type: "integer", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    DirectReferrals = table.Column<int>(type: "integer", nullable: false),
                    IndirectReferrals = table.Column<int>(type: "integer", nullable: false),
                    AwardBalance = table.Column<decimal>(type: "numeric", nullable: false),
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
                    table.PrimaryKey("PK_Facilitators", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Referrals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    ReferrerFacilitatorId = table.Column<int>(type: "integer", nullable: true),
                    ReferrerAreaLeaderId = table.Column<int>(type: "integer", nullable: true),
                    ReferredCustomerId = table.Column<int>(type: "integer", nullable: false),
                    SourceEnquiryId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    AwardAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    AwardIssued = table.Column<bool>(type: "boolean", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConvertedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_Referrals", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Facilitators_AreaLeaderId",
                table: "Facilitators",
                column: "AreaLeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_Facilitators_CustomerId",
                table: "Facilitators",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_ReferrerAreaLeaderId",
                table: "Referrals",
                column: "ReferrerAreaLeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_ReferrerFacilitatorId",
                table: "Referrals",
                column: "ReferrerFacilitatorId");

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_SourceEnquiryId",
                table: "Referrals",
                column: "SourceEnquiryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Facilitators");

            migrationBuilder.DropTable(
                name: "Referrals");

            migrationBuilder.DropColumn(
                name: "ReferredByFacilitatorId",
                table: "Enquiries");
        }
    }
}
