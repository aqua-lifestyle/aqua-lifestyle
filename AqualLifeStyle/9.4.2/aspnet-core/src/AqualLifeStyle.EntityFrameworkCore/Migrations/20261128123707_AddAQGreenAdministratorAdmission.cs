using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    /// <inheritdoc />
    public partial class AddAQGreenAdministratorAdmission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AQGRecruitAttr_Kind",
                table: "AQGreenRecruitmentAttributions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AQGRecruitAttr_Source",
                table: "AQGreenRecruitmentAttributions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AQGRecruitAttr_SourceShape",
                table: "AQGreenRecruitmentAttributions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AQGRecruitConfirm_Method",
                table: "AQGreenRecruitmentAttributionConfirmations");

            migrationBuilder.CreateTable(
                name: "AQGreenHistoricalTreeReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpectedMembers = table.Column<int>(type: "integer", nullable: false),
                    OpenedBy = table.Column<long>(type: "bigint", nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClearedBy = table.Column<long>(type: "bigint", nullable: true),
                    ClearedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AQGreenHistoricalTreeReviews", x => x.Id);
                    table.UniqueConstraint("AK_AQGreenHistoricalTreeReviews_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_AQGHistory_Review", "\"ExpectedMembers\" BETWEEN 1 AND 1000 AND \"OpenedBy\" > 0 AND ((\"ClearedBy\" IS NULL AND \"ClearedAt\" IS NULL) OR (\"ClearedBy\" IS NOT NULL AND \"ClearedBy\" > 0 AND \"ClearedAt\" IS NOT NULL AND \"ClearedAt\" >= \"OpenedAt\"))");
                    table.ForeignKey(
                        name: "FK_AQGreenHistoricalTreeReviews_AQGreenPlacementTreeScopes_Ten~",
                        columns: x => new { x.TenantId, x.ScopeId },
                        principalTable: "AQGreenPlacementTreeScopes",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AQGreenHistoricalMemberReferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ReferenceKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AQGreenHistoricalMemberReferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AQGreenHistoricalMemberReferences_AQGreenHistoricalTreeRevi~",
                        columns: x => new { x.TenantId, x.ReviewId },
                        principalTable: "AQGreenHistoricalTreeReviews",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AQGreenHistoricalMemberReferences_EntryParticipations_Tenan~",
                        columns: x => new { x.TenantId, x.ParticipantId },
                        principalTable: "EntryParticipations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_AQGRecruitAttr_Kind",
                table: "AQGreenRecruitmentAttributions",
                sql: "\"AttributionKind\" IN (1, 2, 3)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AQGRecruitAttr_Source",
                table: "AQGreenRecruitmentAttributions",
                sql: "\"AcquisitionSource\" IN (1, 2, 3)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AQGRecruitAttr_SourceShape",
                table: "AQGreenRecruitmentAttributions",
                sql: "(\"AttributionKind\" = 1 AND \"AcquisitionSource\" = 1 AND \"CreditedSponsorParticipantId\" IS NOT NULL AND \"AssignmentReason\" IS NULL) OR (\"AttributionKind\" = 2 AND \"AcquisitionSource\" = 2 AND \"CreditedSponsorParticipantId\" IS NULL AND \"AttributedByUserId\" IS NOT NULL AND \"AssignmentReason\" IS NOT NULL AND length(trim(\"AssignmentReason\")) > 0) OR (\"AttributionKind\" = 3 AND \"AcquisitionSource\" = 3 AND \"CreditedSponsorParticipantId\" IS NULL AND \"AttributedByUserId\" IS NOT NULL AND \"AssignmentReason\" IS NOT NULL AND length(trim(\"AssignmentReason\")) > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AQGRecruitConfirm_Method",
                table: "AQGreenRecruitmentAttributionConfirmations",
                sql: "\"ConfirmationMethod\" IN (1, 2, 3)");

            migrationBuilder.CreateIndex(
                name: "IX_AQGreenHistoricalMemberReferences_TenantId_ParticipantId",
                table: "AQGreenHistoricalMemberReferences",
                columns: new[] { "TenantId", "ParticipantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AQGreenHistoricalMemberReferences_TenantId_ReferenceKey",
                table: "AQGreenHistoricalMemberReferences",
                columns: new[] { "TenantId", "ReferenceKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AQGreenHistoricalMemberReferences_TenantId_ReviewId",
                table: "AQGreenHistoricalMemberReferences",
                columns: new[] { "TenantId", "ReviewId" });

            migrationBuilder.CreateIndex(
                name: "IX_AQGreenHistoricalTreeReviews_TenantId_ScopeId",
                table: "AQGreenHistoricalTreeReviews",
                columns: new[] { "TenantId", "ScopeId" },
                unique: true);

            if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
                migrationBuilder.Sql("""
                    CREATE FUNCTION public."ValidateAQGreenCompanyEvidence"() RETURNS trigger
                    LANGUAGE plpgsql SET search_path = pg_catalog AS $body$
                    BEGIN
                        IF TG_TABLE_NAME = 'AQGreenRecruitmentAttributions' THEN
                            IF NEW."AttributionKind" = 3 AND NOT EXISTS (
                                SELECT 1 FROM public."EntryParticipations" AS p
                                JOIN public."AQGreenNetworkPlacements" AS r ON r."TenantId" = p."TenantId"
                                WHERE p."TenantId" = NEW."TenantId" AND p."Id" = NEW."ParticipantId"
                                  AND NOT p."IsDeleted" AND p."RecruiterCustomerId" IS NULL
                                  AND r."PlacementTreeScopeId" = NEW."SourceReferenceId" AND r."PlacementParentParticipantId" IS NULL)
                            THEN RAISE EXCEPTION 'Company admission requires a same-Tenant tree, not a credited sponsor'; END IF;
                        ELSE
                            IF EXISTS (SELECT 1 FROM public."AQGreenRecruitmentAttributions" a
                                       WHERE a."TenantId" = NEW."TenantId" AND a."Id" = NEW."AttributionId" AND a."AttributionKind" = 3)
                               OR NEW."ConfirmationMethod" = 3 THEN
                                IF NOT EXISTS (
                                    SELECT 1 FROM public."AQGreenRecruitmentAttributions" a
                                    JOIN public."EntryParticipations" p ON p."Id" = a."ParticipantId" AND p."TenantId" = a."TenantId"
                                    JOIN public."Customers" c ON c."Id" = p."CustomerId" AND c."TenantId" = p."TenantId"
                                    WHERE a."TenantId" = NEW."TenantId" AND a."Id" = NEW."AttributionId"
                                      AND a."AttributionKind" = 3 AND NEW."ConfirmationMethod" = 3 AND NOT p."IsDeleted" AND NOT c."IsDeleted"
                                      AND c."UserId" = NEW."ConfirmedByUserId" AND a."SourceReferenceId" = NEW."EvidenceReferenceId")
                                THEN RAISE EXCEPTION 'Company consent must bind the member and recorded tree'; END IF;
                            END IF;
                        END IF;
                        RETURN NEW;
                    END; $body$;
                    CREATE TRIGGER "TR_AQGCompanyAttribution" BEFORE INSERT ON public."AQGreenRecruitmentAttributions"
                        FOR EACH ROW EXECUTE FUNCTION public."ValidateAQGreenCompanyEvidence"();
                    CREATE TRIGGER "TR_AQGCompanyConsent" BEFORE INSERT ON public."AQGreenRecruitmentAttributionConfirmations"
                        FOR EACH ROW EXECUTE FUNCTION public."ValidateAQGreenCompanyEvidence"();
                    ALTER TABLE public."AQGreenRecruitmentAttributions" ENABLE ALWAYS TRIGGER "TR_AQGCompanyAttribution";
                    ALTER TABLE public."AQGreenRecruitmentAttributionConfirmations" ENABLE ALWAYS TRIGGER "TR_AQGCompanyConsent";

                    CREATE FUNCTION public."ProtectAQGreenHistoricalReview"() RETURNS trigger
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
                    CREATE TRIGGER "TR_AQGHistoryReview_Protect" BEFORE UPDATE OR DELETE ON public."AQGreenHistoricalTreeReviews"
                        FOR EACH ROW EXECUTE FUNCTION public."ProtectAQGreenHistoricalReview"();
                    CREATE TRIGGER "TR_AQGHistoryReference_Protect" BEFORE UPDATE OR DELETE ON public."AQGreenHistoricalMemberReferences"
                        FOR EACH ROW EXECUTE FUNCTION public."ProtectAQGreenHistoricalReview"();
                    CREATE TRIGGER "TR_AQGHistoryReview_NoTruncate" BEFORE TRUNCATE ON public."AQGreenHistoricalTreeReviews"
                        FOR EACH STATEMENT EXECUTE FUNCTION public."ProtectAQGreenHistoricalReview"();
                    CREATE TRIGGER "TR_AQGHistoryReference_NoTruncate" BEFORE TRUNCATE ON public."AQGreenHistoricalMemberReferences"
                        FOR EACH STATEMENT EXECUTE FUNCTION public."ProtectAQGreenHistoricalReview"();
                    ALTER TABLE public."AQGreenHistoricalTreeReviews" ENABLE ALWAYS TRIGGER "TR_AQGHistoryReview_Protect";
                    ALTER TABLE public."AQGreenHistoricalTreeReviews" ENABLE ALWAYS TRIGGER "TR_AQGHistoryReview_NoTruncate";
                    ALTER TABLE public."AQGreenHistoricalMemberReferences" ENABLE ALWAYS TRIGGER "TR_AQGHistoryReference_Protect";
                    ALTER TABLE public."AQGreenHistoricalMemberReferences" ENABLE ALWAYS TRIGGER "TR_AQGHistoryReference_NoTruncate";
                    """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
                migrationBuilder.Sql("""
                    LOCK TABLE public."AQGreenHistoricalTreeReviews", public."AQGreenHistoricalMemberReferences",
                        public."AQGreenRecruitmentAttributions", public."AQGreenRecruitmentAttributionConfirmations" IN ACCESS EXCLUSIVE MODE;
                    DO $body$ BEGIN
                        IF EXISTS (SELECT 1 FROM public."AQGreenHistoricalTreeReviews") OR
                           EXISTS (SELECT 1 FROM public."AQGreenHistoricalMemberReferences") OR
                           EXISTS (SELECT 1 FROM public."AQGreenRecruitmentAttributions" WHERE "AttributionKind" = 3)
                        THEN RAISE EXCEPTION 'Cannot roll back administrator admission after evidence has been recorded'; END IF;
                    END; $body$;
                    DROP TRIGGER "TR_AQGCompanyAttribution" ON public."AQGreenRecruitmentAttributions";
                    DROP TRIGGER "TR_AQGCompanyConsent" ON public."AQGreenRecruitmentAttributionConfirmations";
                    DROP FUNCTION public."ValidateAQGreenCompanyEvidence"();
                    DROP TRIGGER "TR_AQGHistoryReview_Protect" ON public."AQGreenHistoricalTreeReviews";
                    DROP TRIGGER "TR_AQGHistoryReview_NoTruncate" ON public."AQGreenHistoricalTreeReviews";
                    DROP TRIGGER "TR_AQGHistoryReference_Protect" ON public."AQGreenHistoricalMemberReferences";
                    DROP TRIGGER "TR_AQGHistoryReference_NoTruncate" ON public."AQGreenHistoricalMemberReferences";
                    DROP FUNCTION public."ProtectAQGreenHistoricalReview"();
                    """);
            migrationBuilder.DropTable(
                name: "AQGreenHistoricalMemberReferences");

            migrationBuilder.DropTable(
                name: "AQGreenHistoricalTreeReviews");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AQGRecruitAttr_Kind",
                table: "AQGreenRecruitmentAttributions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AQGRecruitAttr_Source",
                table: "AQGreenRecruitmentAttributions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AQGRecruitAttr_SourceShape",
                table: "AQGreenRecruitmentAttributions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AQGRecruitConfirm_Method",
                table: "AQGreenRecruitmentAttributionConfirmations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AQGRecruitAttr_Kind",
                table: "AQGreenRecruitmentAttributions",
                sql: "\"AttributionKind\" IN (1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AQGRecruitAttr_Source",
                table: "AQGreenRecruitmentAttributions",
                sql: "\"AcquisitionSource\" IN (1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AQGRecruitAttr_SourceShape",
                table: "AQGreenRecruitmentAttributions",
                sql: "(\"AttributionKind\" = 1 AND \"AcquisitionSource\" = 1 AND \"CreditedSponsorParticipantId\" IS NOT NULL AND \"AssignmentReason\" IS NULL) OR (\"AttributionKind\" = 2 AND \"AcquisitionSource\" = 2 AND \"CreditedSponsorParticipantId\" IS NULL AND \"AttributedByUserId\" IS NOT NULL AND \"AssignmentReason\" IS NOT NULL AND length(trim(\"AssignmentReason\")) > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AQGRecruitConfirm_Method",
                table: "AQGreenRecruitmentAttributionConfirmations",
                sql: "\"ConfirmationMethod\" IN (1, 2)");
        }
    }
}
