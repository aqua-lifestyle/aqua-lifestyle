using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    /// <inheritdoc />
    public partial class AddProgrammeParticipationApprovalDecisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EntryParticipationApprovalDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdministratorUserId = table.Column<long>(type: "bigint", nullable: false),
                    Approved = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EntryParticipationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntryParticipationApprovalDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntryParticipationApprovalDecisions_EntryParticipations_Ent~",
                        column: x => x.EntryParticipationId,
                        principalTable: "EntryParticipations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OnyxParticipationApprovalDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdministratorUserId = table.Column<long>(type: "bigint", nullable: false),
                    Approved = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OnyxParticipationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnyxParticipationApprovalDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnyxParticipationApprovalDecisions_OnyxParticipations_OnyxP~",
                        column: x => x.OnyxParticipationId,
                        principalTable: "OnyxParticipations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EntryParticipationApprovalDecisions_EntryParticipationId",
                table: "EntryParticipationApprovalDecisions",
                column: "EntryParticipationId");

            migrationBuilder.CreateIndex(
                name: "IX_OnyxParticipationApprovalDecisions_OnyxParticipationId",
                table: "OnyxParticipationApprovalDecisions",
                column: "OnyxParticipationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EntryParticipationApprovalDecisions");

            migrationBuilder.DropTable(
                name: "OnyxParticipationApprovalDecisions");
        }
    }
}
