using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    /// <inheritdoc />
    public partial class AddAQGreenPlacementV2Foundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_EntryParticipations_TenantId_Id",
                table: "EntryParticipations",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateTable(
                name: "AQGreenPlacementTreeScopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AQGreenPlacementTreeScopes", x => x.Id);
                    table.UniqueConstraint("AK_AQGreenPlacementTreeScopes_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_AQGreenPlacementTreeScopes_TenantId_Positive", "\"TenantId\" > 0");
                    table.ForeignKey(
                        name: "FK_AQGreenPlacementTreeScopes_AbpTenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "AbpTenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AQGreenNetworkPlacements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    PlacementTreeScopeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlacementParentParticipantId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlacementSlot = table.Column<int>(type: "integer", nullable: true),
                    CanonicalPath = table.Column<string>(type: "text", nullable: false),
                    PlacedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RulesVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AQGreenNetworkPlacements", x => x.Id);
                    table.UniqueConstraint("AK_AQGreenNetworkPlacements_TenantId_PlacementTreeScopeId_Part~", x => new { x.TenantId, x.PlacementTreeScopeId, x.ParticipantId });
                    table.CheckConstraint("CK_AQGreenNetworkPlacements_CanonicalPath_Characters", "length(replace(replace(replace(replace(replace(\"CanonicalPath\", '1', ''), '2', ''), '3', ''), '4', ''), '5', '')) = 0");
                    table.CheckConstraint("CK_AQGreenNetworkPlacements_NoSelfParent", "\"PlacementParentParticipantId\" IS NULL OR \"ParticipantId\" <> \"PlacementParentParticipantId\"");
                    table.CheckConstraint("CK_AQGreenNetworkPlacements_PlacementSlot_Range", "\"PlacementSlot\" IS NULL OR (\"PlacementSlot\" >= 1 AND \"PlacementSlot\" <= 5)");
                    table.CheckConstraint("CK_AQGreenNetworkPlacements_RootOrNonRootShape", "(\"PlacementParentParticipantId\" IS NULL AND \"PlacementSlot\" IS NULL AND \"CanonicalPath\" = '') OR (\"PlacementParentParticipantId\" IS NOT NULL AND \"PlacementSlot\" IS NOT NULL AND \"CanonicalPath\" <> '')");
                    table.CheckConstraint("CK_AQGreenNetworkPlacements_RulesVersion_NotBlank", "length(trim(\"RulesVersion\")) > 0");
                    table.CheckConstraint("CK_AQGreenNetworkPlacements_TenantId_Positive", "\"TenantId\" > 0");
                    table.ForeignKey(
                        name: "FK_AQGreenNetworkPlacements_AQGreenNetworkPlacements_TenantId_~",
                        columns: x => new { x.TenantId, x.PlacementTreeScopeId, x.PlacementParentParticipantId },
                        principalTable: "AQGreenNetworkPlacements",
                        principalColumns: new[] { "TenantId", "PlacementTreeScopeId", "ParticipantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AQGreenNetworkPlacements_AQGreenPlacementTreeScopes_TenantI~",
                        columns: x => new { x.TenantId, x.PlacementTreeScopeId },
                        principalTable: "AQGreenPlacementTreeScopes",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AQGreenNetworkPlacements_EntryParticipations_TenantId_Parti~",
                        columns: x => new { x.TenantId, x.ParticipantId },
                        principalTable: "EntryParticipations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AQGreenNetworkPlacements_TenantId_ParticipantId",
                table: "AQGreenNetworkPlacements",
                columns: new[] { "TenantId", "ParticipantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AQGreenNetworkPlacements_TenantId_PlacementTreeScopeId",
                table: "AQGreenNetworkPlacements",
                columns: new[] { "TenantId", "PlacementTreeScopeId" },
                unique: true,
                filter: "\"PlacementParentParticipantId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AQGreenNetworkPlacements_TenantId_PlacementTreeScopeId_Plac~",
                table: "AQGreenNetworkPlacements",
                columns: new[] { "TenantId", "PlacementTreeScopeId", "PlacementParentParticipantId", "PlacementSlot" },
                unique: true,
                filter: "\"PlacementParentParticipantId\" IS NOT NULL AND \"PlacementSlot\" IS NOT NULL");

            if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.Sql("""
                    CREATE FUNCTION "ValidateAQGreenNetworkPlacementInsert"()
                    RETURNS trigger
                    LANGUAGE plpgsql
                    SET search_path = pg_catalog
                    AS $function$
                    DECLARE
                        parent_path text;
                        parent_placed_at timestamp with time zone;
                    BEGIN
                        IF NEW."RulesVersion" !~ '[^[:space:]]'
                        THEN
                            RAISE EXCEPTION 'AQGreen placement rules version must not be blank.';
                        END IF;

                        IF NEW."PlacementParentParticipantId" IS NULL
                           OR NEW."PlacementSlot" IS NULL
                           OR NEW."PlacementSlot" < 1
                           OR NEW."PlacementSlot" > 5
                           OR NEW."ParticipantId" = NEW."PlacementParentParticipantId"
                        THEN
                            RETURN NEW;
                        END IF;

                        SELECT parent."CanonicalPath", parent."PlacedAt"
                        INTO parent_path, parent_placed_at
                        FROM public."AQGreenNetworkPlacements" parent
                        WHERE parent."TenantId" = NEW."TenantId"
                          AND parent."PlacementTreeScopeId" = NEW."PlacementTreeScopeId"
                          AND parent."ParticipantId" = NEW."PlacementParentParticipantId";

                        IF NOT FOUND
                        THEN
                            RAISE EXCEPTION 'AQGreen placement parent must exist in the same Tenant and placement-tree scope.';
                        END IF;

                        IF NEW."CanonicalPath" IS DISTINCT FROM
                           parent_path || NEW."PlacementSlot"::text
                        THEN
                            RAISE EXCEPTION 'AQGreen canonical path must equal the parent path plus placement slot.';
                        END IF;

                        IF NEW."PlacedAt" < parent_placed_at
                        THEN
                            RAISE EXCEPTION 'AQGreen child placement cannot precede its parent placement.';
                        END IF;

                        RETURN NEW;
                    END;
                    $function$;

                    CREATE TRIGGER "TR_AQGreenNetworkPlacements_ValidateInsert"
                    BEFORE INSERT ON "AQGreenNetworkPlacements"
                    FOR EACH ROW
                    EXECUTE FUNCTION "ValidateAQGreenNetworkPlacementInsert"();

                    CREATE FUNCTION "EnsureAQGreenPlacementTreeScopeHasOneRoot"()
                    RETURNS trigger
                    LANGUAGE plpgsql
                    SET search_path = pg_catalog
                    AS $function$
                    DECLARE
                        root_count bigint;
                    BEGIN
                        SELECT COUNT(*)
                        INTO root_count
                        FROM public."AQGreenNetworkPlacements" placement
                        WHERE placement."TenantId" = NEW."TenantId"
                          AND placement."PlacementTreeScopeId" = NEW."Id"
                          AND placement."PlacementParentParticipantId" IS NULL;

                        IF root_count <> 1
                        THEN
                            RAISE EXCEPTION 'Each AQGreen placement-tree scope must have exactly one root.';
                        END IF;

                        RETURN NULL;
                    END;
                    $function$;

                    CREATE CONSTRAINT TRIGGER "TR_AQGreenPlacementTreeScopes_RequireRoot"
                    AFTER INSERT ON "AQGreenPlacementTreeScopes"
                    DEFERRABLE INITIALLY DEFERRED
                    FOR EACH ROW
                    EXECUTE FUNCTION "EnsureAQGreenPlacementTreeScopeHasOneRoot"();

                    CREATE FUNCTION "PreventAQGreenPlacementTopologyMutation"()
                    RETURNS trigger
                    LANGUAGE plpgsql
                    SET search_path = pg_catalog
                    AS $function$
                    BEGIN
                        RAISE EXCEPTION 'AQGreen placement topology is append-only.';
                    END;
                    $function$;

                    CREATE TRIGGER "TR_AQGreenNetworkPlacements_AppendOnly"
                    BEFORE UPDATE OR DELETE ON "AQGreenNetworkPlacements"
                    FOR EACH ROW
                    EXECUTE FUNCTION "PreventAQGreenPlacementTopologyMutation"();

                    CREATE TRIGGER "TR_AQGreenNetworkPlacements_PreventTruncate"
                    BEFORE TRUNCATE ON "AQGreenNetworkPlacements"
                    FOR EACH STATEMENT
                    EXECUTE FUNCTION "PreventAQGreenPlacementTopologyMutation"();

                    CREATE TRIGGER "TR_AQGreenPlacementTreeScopes_AppendOnly"
                    BEFORE UPDATE OR DELETE ON "AQGreenPlacementTreeScopes"
                    FOR EACH ROW
                    EXECUTE FUNCTION "PreventAQGreenPlacementTopologyMutation"();

                    CREATE TRIGGER "TR_AQGreenPlacementTreeScopes_PreventTruncate"
                    BEFORE TRUNCATE ON "AQGreenPlacementTreeScopes"
                    FOR EACH STATEMENT
                    EXECUTE FUNCTION "PreventAQGreenPlacementTopologyMutation"();
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                // Durable topology must be preserved; once evidence exists, use reviewed forward remediation or restoration.
                migrationBuilder.Sql("""
                    LOCK TABLE public."AQGreenNetworkPlacements",
                               public."AQGreenPlacementTreeScopes"
                    IN ACCESS EXCLUSIVE MODE;

                    DO $block$
                    BEGIN
                        IF EXISTS (SELECT 1 FROM "AQGreenNetworkPlacements")
                           OR EXISTS (SELECT 1 FROM "AQGreenPlacementTreeScopes")
                        THEN
                            RAISE EXCEPTION 'Cannot remove AQGreen placement topology after evidence has been recorded.';
                        END IF;
                    END;
                    $block$;

                    DROP TRIGGER "TR_AQGreenNetworkPlacements_ValidateInsert"
                        ON "AQGreenNetworkPlacements";
                    DROP TRIGGER "TR_AQGreenPlacementTreeScopes_RequireRoot"
                        ON "AQGreenPlacementTreeScopes";
                    DROP TRIGGER "TR_AQGreenNetworkPlacements_AppendOnly"
                        ON "AQGreenNetworkPlacements";
                    DROP TRIGGER "TR_AQGreenNetworkPlacements_PreventTruncate"
                        ON "AQGreenNetworkPlacements";
                    DROP TRIGGER "TR_AQGreenPlacementTreeScopes_AppendOnly"
                        ON "AQGreenPlacementTreeScopes";
                    DROP TRIGGER "TR_AQGreenPlacementTreeScopes_PreventTruncate"
                        ON "AQGreenPlacementTreeScopes";
                    DROP FUNCTION "ValidateAQGreenNetworkPlacementInsert"();
                    DROP FUNCTION "EnsureAQGreenPlacementTreeScopeHasOneRoot"();
                    DROP FUNCTION "PreventAQGreenPlacementTopologyMutation"();
                    """);
            }

            migrationBuilder.DropTable(
                name: "AQGreenNetworkPlacements");

            migrationBuilder.DropTable(
                name: "AQGreenPlacementTreeScopes");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_EntryParticipations_TenantId_Id",
                table: "EntryParticipations");
        }
    }
}
