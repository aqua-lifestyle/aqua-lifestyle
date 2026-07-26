using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    public partial class AddProgrammeRecruitmentInvitations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClubMemberNumber",
                table: "Customers",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Customers"
                SET "ClubMemberNumber" = 'CLB-' || UPPER(LPAD(TO_HEX("Id"), 12, '0'))
                WHERE "ClubMemberNumber" IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "ClubMemberNumber",
                table: "Customers",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "OnyxRecruiterCorrections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousRecruiterCustomerId = table.Column<int>(type: "integer", nullable: true),
                    NewRecruiterCustomerId = table.Column<int>(type: "integer", nullable: true),
                    AdministratorUserId = table.Column<long>(type: "bigint", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CorrectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OnyxParticipationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnyxRecruiterCorrections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnyxRecruiterCorrections_OnyxParticipations_OnyxParticipati~",
                        column: x => x.OnyxParticipationId,
                        principalTable: "OnyxParticipations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProgrammeInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    ProgrammeKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProgrammeParticipationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorUserId = table.Column<long>(type: "bigint", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierUserId = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeleterUserId = table.Column<long>(type: "bigint", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_ProgrammeInvitations", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_Customers_ClubMemberNumber",
                table: "Customers",
                column: "ClubMemberNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnyxRecruiterCorrections_OnyxParticipationId",
                table: "OnyxRecruiterCorrections",
                column: "OnyxParticipationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgrammeInvitations_Code",
                table: "ProgrammeInvitations",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProgrammeInvitations_ProgrammeKey_ProgrammeParticipationId",
                table: "ProgrammeInvitations",
                columns: new[] { "ProgrammeKey", "ProgrammeParticipationId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "OnyxRecruiterCorrections");
            migrationBuilder.DropTable(name: "ProgrammeInvitations");
            migrationBuilder.DropIndex(
                name: "IX_Customers_ClubMemberNumber",
                table: "Customers");
            migrationBuilder.DropColumn(
                name: "ClubMemberNumber",
                table: "Customers");
        }
    }
}
