using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    /// <inheritdoc />
    public partial class PersistDataProtectionKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FriendlyName = table.Column<string>(type: "text", nullable: true),
                    Xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider.Contains("Npgsql"))
            {
                migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM ""DataProtectionKeys"") THEN
        RAISE EXCEPTION 'Cannot remove data-protection keys while key history exists.';
    END IF;
END $$;");
            }
            else if (ActiveProvider.Contains("SqlServer"))
            {
                migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [DataProtectionKeys])
    THROW 51000, 'Cannot remove data-protection keys while key history exists.', 1;");
            }

            migrationBuilder.DropTable(
                name: "DataProtectionKeys");
        }
    }
}
