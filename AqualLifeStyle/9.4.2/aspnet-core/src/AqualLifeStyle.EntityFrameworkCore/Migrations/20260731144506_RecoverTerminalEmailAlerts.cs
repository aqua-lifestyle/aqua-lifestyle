using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    /// <inheritdoc />
    public partial class RecoverTerminalEmailAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "TerminalAlertEmittedAt",
                table: "TransactionalEmailOutboxMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransactionalEmailOutboxMessages_Status_TerminalAlertEmitte~",
                table: "TransactionalEmailOutboxMessages",
                columns: new[] { "Status", "TerminalAlertEmittedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TransactionalEmailOutboxMessages_Status_TerminalAlertEmitte~",
                table: "TransactionalEmailOutboxMessages");

            migrationBuilder.DropColumn(
                name: "TerminalAlertEmittedAt",
                table: "TransactionalEmailOutboxMessages");
        }
    }
}
