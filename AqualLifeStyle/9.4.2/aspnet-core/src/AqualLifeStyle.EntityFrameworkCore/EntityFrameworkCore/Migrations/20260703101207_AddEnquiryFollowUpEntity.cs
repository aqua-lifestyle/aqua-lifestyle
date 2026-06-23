using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AqualLifeStyle.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddEnquiryFollowUpEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ActivationDate",
                table: "Memberships",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastObligationMetDate",
                table: "Memberships",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyObligationAmount",
                table: "Memberships",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "AssignedToMemberId",
                table: "Enquiries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ConversionProbability",
                table: "Enquiries",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConvertedAt",
                table: "Enquiries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsConverted",
                table: "Enquiries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastFollowUpDate",
                table: "Enquiries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EnquiryFollowUps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EnquiryId = table.Column<int>(type: "integer", nullable: false),
                    FollowUpDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FollowUpByMemberId = table.Column<int>(type: "integer", nullable: true),
                    FollowUpNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    ConversionProbability = table.Column<decimal>(type: "numeric", nullable: false),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnquiryFollowUps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnquiryFollowUps_Enquiries_EnquiryId",
                        column: x => x.EnquiryId,
                        principalTable: "Enquiries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EnquiryFollowUps_EnquiryId",
                table: "EnquiryFollowUps",
                column: "EnquiryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EnquiryFollowUps");

            migrationBuilder.DropColumn(
                name: "ActivationDate",
                table: "Memberships");

            migrationBuilder.DropColumn(
                name: "LastObligationMetDate",
                table: "Memberships");

            migrationBuilder.DropColumn(
                name: "MonthlyObligationAmount",
                table: "Memberships");

            migrationBuilder.DropColumn(
                name: "AssignedToMemberId",
                table: "Enquiries");

            migrationBuilder.DropColumn(
                name: "ConversionProbability",
                table: "Enquiries");

            migrationBuilder.DropColumn(
                name: "ConvertedAt",
                table: "Enquiries");

            migrationBuilder.DropColumn(
                name: "IsConverted",
                table: "Enquiries");

            migrationBuilder.DropColumn(
                name: "LastFollowUpDate",
                table: "Enquiries");
        }
    }
}
