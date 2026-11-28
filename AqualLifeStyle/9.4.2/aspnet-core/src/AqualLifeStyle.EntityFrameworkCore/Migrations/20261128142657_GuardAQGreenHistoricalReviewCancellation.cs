using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    /// <inheritdoc />
    public partial class GuardAQGreenHistoricalReviewCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AQGreenHistoricalTreeReviews_TenantId_ScopeId",
                table: "AQGreenHistoricalTreeReviews");

            migrationBuilder.AddColumn<string>(
                name: "CancellationEvidenceReference",
                table: "AQGreenHistoricalTreeReviews",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "AQGreenHistoricalTreeReviews",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "AQGreenHistoricalTreeReviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CancelledBy",
                table: "AQGreenHistoricalTreeReviews",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PlacementCountAtOpen",
                table: "AQGreenHistoricalTreeReviews",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProtectedAt",
                table: "AQGreenHistoricalTreeReviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RosterDigest",
                table: "AQGreenHistoricalTreeReviews",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AQGreenHistoricalTreeReviews_TenantId_ScopeId",
                table: "AQGreenHistoricalTreeReviews",
                columns: new[] { "TenantId", "ScopeId" },
                unique: true,
                filter: "\"CancelledAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AQGreenHistoricalTreeReviews_TenantId_ScopeId_EvidenceRefer~",
                table: "AQGreenHistoricalTreeReviews",
                columns: new[] { "TenantId", "ScopeId", "EvidenceReference" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_AQGHistory_Cancellation",
                table: "AQGreenHistoricalTreeReviews",
                sql: "(\"PlacementCountAtOpen\" IS NULL OR \"PlacementCountAtOpen\" > 0) AND (\"ProtectedAt\" IS NULL OR \"ProtectedAt\" >= \"OpenedAt\") AND ((\"CancelledAt\" IS NULL AND \"CancelledBy\" IS NULL AND \"CancellationReason\" IS NULL AND \"CancellationEvidenceReference\" IS NULL) OR (\"CancelledAt\" IS NOT NULL AND \"CancelledAt\" >= \"OpenedAt\" AND \"CancelledBy\" IS NOT NULL AND \"CancelledBy\" > 0 AND \"ClearedAt\" IS NULL AND \"ProtectedAt\" IS NULL AND \"PlacementCountAtOpen\" IS NOT NULL AND \"CancellationReason\" IS NOT NULL AND length(trim(\"CancellationReason\")) > 0 AND \"CancellationEvidenceReference\" IS NOT NULL AND length(trim(\"CancellationEvidenceReference\")) > 0))");

            if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
                migrationBuilder.Sql("""
                    CREATE OR REPLACE FUNCTION public."ProtectAQGreenHistoricalReview"() RETURNS trigger
                    LANGUAGE plpgsql SET search_path = pg_catalog AS $body$
                    BEGIN
                        IF TG_OP = 'INSERT' AND TG_TABLE_NAME = 'AQGreenHistoricalMemberReferences' THEN
                            PERFORM 1 FROM public."AQGreenHistoricalTreeReviews" r
                                WHERE r."TenantId" = NEW."TenantId" AND r."Id" = NEW."ReviewId"
                                  AND r."CancelledAt" IS NULL AND r."ClearedAt" IS NULL FOR UPDATE;
                            IF FOUND THEN RETURN NEW; END IF;
                        ELSIF TG_OP = 'UPDATE' AND TG_TABLE_NAME = 'AQGreenHistoricalTreeReviews' THEN
                            IF OLD."CancelledAt" IS NULL AND OLD."ClearedAt" IS NULL THEN
                                IF OLD."RosterDigest" IS NULL AND NEW."RosterDigest" ~ '^[0-9a-f]{64}$'
                                   AND to_jsonb(NEW) - 'RosterDigest' = to_jsonb(OLD) - 'RosterDigest'
                                THEN RETURN NEW; END IF;
                                IF OLD."ProtectedAt" IS NULL AND NEW."ProtectedAt" IS NOT NULL
                                   AND to_jsonb(NEW) - 'ProtectedAt' = to_jsonb(OLD) - 'ProtectedAt'
                                THEN RETURN NEW; END IF;
                                IF OLD."RosterDigest" IS NOT NULL AND OLD."ProtectedAt" IS NOT NULL
                                   AND NEW."ClearedAt" IS NOT NULL AND NEW."ClearedAt" >= OLD."ProtectedAt"
                                   AND (to_jsonb(NEW) - 'ClearedBy' - 'ClearedAt') = (to_jsonb(OLD) - 'ClearedBy' - 'ClearedAt')
                                   AND (SELECT count(*) FROM public."AQGreenHistoricalMemberReferences" r
                                        WHERE r."TenantId" = NEW."TenantId" AND r."ReviewId" = NEW."Id") = NEW."ExpectedMembers"
                                THEN RETURN NEW; END IF;
                                IF OLD."PlacementCountAtOpen" IS NOT NULL AND OLD."ProtectedAt" IS NULL
                                   AND NEW."CancelledAt" IS NOT NULL AND NEW."CancellationEvidenceReference" <> OLD."EvidenceReference"
                                   AND (to_jsonb(NEW) - 'CancelledAt' - 'CancelledBy' - 'CancellationReason' - 'CancellationEvidenceReference') =
                                       (to_jsonb(OLD) - 'CancelledAt' - 'CancelledBy' - 'CancellationReason' - 'CancellationEvidenceReference')
                                   AND NOT EXISTS (SELECT 1 FROM public."AQGreenHistoricalMemberReferences" r
                                       WHERE r."TenantId" = OLD."TenantId" AND r."ReviewId" = OLD."Id")
                                   AND (SELECT count(*) FROM public."AQGreenNetworkPlacements" p
                                       WHERE p."TenantId" = OLD."TenantId" AND p."PlacementTreeScopeId" = OLD."ScopeId") = OLD."PlacementCountAtOpen"
                                THEN RETURN NEW; END IF;
                            END IF;
                        END IF;
                        RAISE EXCEPTION 'Historical review evidence cannot be rewritten or released without its required checks';
                    END; $body$;
                    CREATE TRIGGER "TR_AQGHistoryReference_OpenOnly" BEFORE INSERT ON public."AQGreenHistoricalMemberReferences"
                        FOR EACH ROW EXECUTE FUNCTION public."ProtectAQGreenHistoricalReview"();
                    ALTER TABLE public."AQGreenHistoricalMemberReferences" ENABLE ALWAYS TRIGGER "TR_AQGHistoryReference_OpenOnly";
                    """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
                migrationBuilder.Sql("""
                    LOCK TABLE public."AQGreenHistoricalTreeReviews", public."AQGreenHistoricalMemberReferences" IN ACCESS EXCLUSIVE MODE;
                    DO $body$ BEGIN
                        IF EXISTS (SELECT 1 FROM public."AQGreenHistoricalTreeReviews"
                            WHERE "CancelledAt" IS NOT NULL OR "RosterDigest" IS NOT NULL OR
                                  "ProtectedAt" IS NOT NULL OR "PlacementCountAtOpen" IS NOT NULL)
                        THEN RAISE EXCEPTION 'Cannot roll back historical review safeguards after review evidence has been recorded'; END IF;
                    END; $body$;
                    DROP TRIGGER "TR_AQGHistoryReference_OpenOnly" ON public."AQGreenHistoricalMemberReferences";
                    CREATE OR REPLACE FUNCTION public."ProtectAQGreenHistoricalReview"() RETURNS trigger
                    LANGUAGE plpgsql SET search_path = pg_catalog AS $body$
                    BEGIN
                        IF TG_OP = 'UPDATE' AND TG_TABLE_NAME = 'AQGreenHistoricalTreeReviews' THEN
                            IF OLD."ClearedAt" IS NULL AND NEW."ClearedAt" IS NOT NULL
                                AND (to_jsonb(NEW) - 'ClearedBy' - 'ClearedAt') = (to_jsonb(OLD) - 'ClearedBy' - 'ClearedAt')
                                AND (SELECT count(*) FROM public."AQGreenHistoricalMemberReferences" r
                                    WHERE r."TenantId" = NEW."TenantId" AND r."ReviewId" = NEW."Id") = NEW."ExpectedMembers"
                            THEN RETURN NEW; END IF;
                        END IF;
                        RAISE EXCEPTION 'Historical review evidence can only be cleared once, with a complete roster';
                    END; $body$;
                    """);

            migrationBuilder.DropIndex(
                name: "IX_AQGreenHistoricalTreeReviews_TenantId_ScopeId",
                table: "AQGreenHistoricalTreeReviews");

            migrationBuilder.DropIndex(
                name: "IX_AQGreenHistoricalTreeReviews_TenantId_ScopeId_EvidenceRefer~",
                table: "AQGreenHistoricalTreeReviews");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AQGHistory_Cancellation",
                table: "AQGreenHistoricalTreeReviews");

            migrationBuilder.DropColumn(
                name: "CancellationEvidenceReference",
                table: "AQGreenHistoricalTreeReviews");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "AQGreenHistoricalTreeReviews");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "AQGreenHistoricalTreeReviews");

            migrationBuilder.DropColumn(
                name: "CancelledBy",
                table: "AQGreenHistoricalTreeReviews");

            migrationBuilder.DropColumn(
                name: "PlacementCountAtOpen",
                table: "AQGreenHistoricalTreeReviews");

            migrationBuilder.DropColumn(
                name: "ProtectedAt",
                table: "AQGreenHistoricalTreeReviews");

            migrationBuilder.DropColumn(
                name: "RosterDigest",
                table: "AQGreenHistoricalTreeReviews");

            migrationBuilder.CreateIndex(
                name: "IX_AQGreenHistoricalTreeReviews_TenantId_ScopeId",
                table: "AQGreenHistoricalTreeReviews",
                columns: new[] { "TenantId", "ScopeId" },
                unique: true);
        }
    }
}
