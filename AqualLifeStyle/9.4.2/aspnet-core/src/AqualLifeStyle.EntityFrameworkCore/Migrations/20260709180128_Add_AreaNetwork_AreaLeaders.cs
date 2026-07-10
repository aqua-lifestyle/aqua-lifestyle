using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    /// <inheritdoc />
    public partial class Add_AreaNetwork_AreaLeaders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateIndex(
                name: "IX_AreaLeaders_CustomerId",
                table: "AreaLeaders",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_AreaSpaces_AreaLeaderId",
                table: "AreaSpaces",
                column: "AreaLeaderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AreaLeaders");

            migrationBuilder.DropTable(
                name: "AreaSpaces");
        }
    }
}
