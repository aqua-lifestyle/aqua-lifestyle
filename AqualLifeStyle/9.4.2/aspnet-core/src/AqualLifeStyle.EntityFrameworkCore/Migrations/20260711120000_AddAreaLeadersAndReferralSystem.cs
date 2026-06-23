using System;
using AqualLifeStyle.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    [DbContext(typeof(AqualLifeStyleDbContext))]
    [Migration("20260711120000_AddAreaLeadersAndReferralSystem")]
    /// <inheritdoc />
    public partial class AddAreaLeadersAndReferralSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Memberships",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReferredByFacilitatorId",
                table: "Enquiries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Enquiries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Customers",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AreaLeaders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    LicenseType = table.Column<int>(type: "integer", nullable: false),
                    LicenseFee = table.Column<decimal>(type: "numeric", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    AreaSpaceId = table.Column<int>(type: "integer", nullable: true),
                    MonthlySubscription = table.Column<decimal>(type: "numeric", nullable: false),
                    DirectReferrals = table.Column<int>(type: "integer", nullable: false),
                    IndirectReferrals = table.Column<int>(type: "integer", nullable: false),
                    OrderTarget = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_AreaLeaders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AreaSpaces",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    AreaLeaderId = table.Column<int>(type: "integer", nullable: false),
                    AddressLine = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Capacity = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    InterestedMembers = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReviewStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PresentationsCompleted = table.Column<int>(type: "integer", nullable: false),
                    StartupOrdersCompleted = table.Column<int>(type: "integer", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_AreaSpaces", x => x.Id);
                });

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
                name: "IX_AreaLeaders_CustomerId",
                table: "AreaLeaders",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_AreaSpaces_AreaLeaderId",
                table: "AreaSpaces",
                column: "AreaLeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId",
                table: "Customers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Enquiries_ReferredByFacilitatorId",
                table: "Enquiries",
                column: "ReferredByFacilitatorId");

            migrationBuilder.CreateIndex(
                name: "IX_Enquiries_TenantId",
                table: "Enquiries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Facilitators_AreaLeaderId",
                table: "Facilitators",
                column: "AreaLeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_Facilitators_CustomerId",
                table: "Facilitators",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_TenantId",
                table: "Memberships",
                column: "TenantId");

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
                name: "AreaLeaders");

            migrationBuilder.DropTable(
                name: "AreaSpaces");

            migrationBuilder.DropTable(
                name: "Facilitators");

            migrationBuilder.DropTable(
                name: "Referrals");

            migrationBuilder.DropIndex(
                name: "IX_Customers_TenantId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Enquiries_ReferredByFacilitatorId",
                table: "Enquiries");

            migrationBuilder.DropIndex(
                name: "IX_Enquiries_TenantId",
                table: "Enquiries");

            migrationBuilder.DropIndex(
                name: "IX_Memberships_TenantId",
                table: "Memberships");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "ReferredByFacilitatorId",
                table: "Enquiries");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Enquiries");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Memberships");
        }
    }
}
