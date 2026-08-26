using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqualLifeStyle.Migrations
{
    /// <inheritdoc />
    public partial class AddAQGreenRecruitmentAttributionFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_ProgrammeInvitations_Id_Tenant_Participation",
                table: "ProgrammeInvitations",
                columns: new[] { "Id", "TenantId", "ProgrammeParticipationId" });

            migrationBuilder.CreateTable(
                name: "AQGreenRecruitmentAttributions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreditedSponsorParticipantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttributionKind = table.Column<int>(type: "integer", nullable: false),
                    AcquisitionSource = table.Column<int>(type: "integer", nullable: false),
                    SourceReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttributedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AttributedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    AssignmentReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RulesVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AQGreenRecruitmentAttributions", x => x.Id);
                    table.UniqueConstraint("AK_AQGRecruitAttr_Tenant_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_AQGRecruitAttr_Actor", "\"AttributedByUserId\" IS NULL OR \"AttributedByUserId\" > 0");
                    table.CheckConstraint("CK_AQGRecruitAttr_Kind", "\"AttributionKind\" IN (1, 2)");
                    table.CheckConstraint("CK_AQGRecruitAttr_NoSelfSponsor", "\"CreditedSponsorParticipantId\" IS NULL OR \"ParticipantId\" <> \"CreditedSponsorParticipantId\"");
                    table.CheckConstraint("CK_AQGRecruitAttr_Rules_NotBlank", "length(trim(\"RulesVersion\")) > 0");
                    table.CheckConstraint("CK_AQGRecruitAttr_Source", "\"AcquisitionSource\" IN (1, 2)");
                    table.CheckConstraint("CK_AQGRecruitAttr_SourceRef", "\"SourceReferenceId\" <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("CK_AQGRecruitAttr_SourceShape", "(\"AttributionKind\" = 1 AND \"AcquisitionSource\" = 1 AND \"CreditedSponsorParticipantId\" IS NOT NULL AND \"AssignmentReason\" IS NULL) OR (\"AttributionKind\" = 2 AND \"AcquisitionSource\" = 2 AND \"CreditedSponsorParticipantId\" IS NULL AND \"AttributedByUserId\" IS NOT NULL AND \"AssignmentReason\" IS NOT NULL AND length(trim(\"AssignmentReason\")) > 0)");
                    table.CheckConstraint("CK_AQGRecruitAttr_Tenant_Positive", "\"TenantId\" > 0");
                    table.ForeignKey(
                        name: "FK_AQGRecruitAttr_InvitationEvidence",
                        columns: x => new
                        {
                            x.SourceReferenceId,
                            x.TenantId,
                            x.CreditedSponsorParticipantId
                        },
                        principalTable: "ProgrammeInvitations",
                        principalColumns: new[]
                        {
                            "Id",
                            "TenantId",
                            "ProgrammeParticipationId"
                        },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AQGRecruitAttr_Participant",
                        columns: x => new { x.TenantId, x.ParticipantId },
                        principalTable: "EntryParticipations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AQGRecruitAttr_Sponsor",
                        columns: x => new { x.TenantId, x.CreditedSponsorParticipantId },
                        principalTable: "EntryParticipations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AQGreenRecruitmentAttributionConfirmations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    AttributionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConfirmedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ConfirmationMethod = table.Column<int>(type: "integer", nullable: false),
                    EvidenceReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    RulesVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AQGreenRecruitmentAttributionConfirmations", x => x.Id);
                    table.CheckConstraint("CK_AQGRecruitConfirm_Actor", "\"ConfirmedByUserId\" IS NULL OR \"ConfirmedByUserId\" > 0");
                    table.CheckConstraint("CK_AQGRecruitConfirm_EvidenceRef", "\"EvidenceReferenceId\" <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("CK_AQGRecruitConfirm_Method", "\"ConfirmationMethod\" IN (1, 2)");
                    table.CheckConstraint("CK_AQGRecruitConfirm_Rules_NotBlank", "length(trim(\"RulesVersion\")) > 0");
                    table.CheckConstraint("CK_AQGRecruitConfirm_Tenant_Positive", "\"TenantId\" > 0");
                    table.ForeignKey(
                        name: "FK_AQGRecruitConfirm_Attribution",
                        columns: x => new { x.TenantId, x.AttributionId },
                        principalTable: "AQGreenRecruitmentAttributions",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_AQGRecruitConfirm_Tenant_Attribution",
                table: "AQGreenRecruitmentAttributionConfirmations",
                columns: new[] { "TenantId", "AttributionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AQGreenRecruitmentAttributions_SourceReferenceId_TenantId_CreditedSponsorParticipantId",
                table: "AQGreenRecruitmentAttributions",
                columns: new[]
                {
                    "SourceReferenceId",
                    "TenantId",
                    "CreditedSponsorParticipantId"
                });

            migrationBuilder.CreateIndex(
                name: "IX_AQGRecruitAttr_Tenant_Sponsor",
                table: "AQGreenRecruitmentAttributions",
                columns: new[] { "TenantId", "CreditedSponsorParticipantId" });

            migrationBuilder.CreateIndex(
                name: "UX_AQGRecruitAttr_Tenant_Participant",
                table: "AQGreenRecruitmentAttributions",
                columns: new[] { "TenantId", "ParticipantId" },
                unique: true);

            if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.Sql("""
                    CREATE FUNCTION "ValidateAQGreenRecruitmentAttributionInsert"()
                    RETURNS trigger
                    LANGUAGE plpgsql
                    SET search_path = pg_catalog
                    AS $function$
                    BEGIN
                        IF NEW."RulesVersion" !~ '[^[:space:]]'
                        THEN
                            RAISE EXCEPTION 'AQGreen attribution rules version must not be blank.';
                        END IF;

                        IF NEW."AssignmentReason" IS NOT NULL
                           AND NEW."AssignmentReason" !~ '[^[:space:]]'
                        THEN
                            RAISE EXCEPTION 'AQGreen attribution assignment reason must not be blank.';
                        END IF;

                        IF NEW."AttributionKind" = 1
                           AND NEW."AcquisitionSource" = 1
                           AND NEW."TenantId" > 0
                           AND NEW."CreditedSponsorParticipantId" IS NOT NULL
                           AND NEW."SourceReferenceId" <>
                               '00000000-0000-0000-0000-000000000000'::uuid
                        THEN
                            PERFORM 1
                            FROM public."ProgrammeInvitations" invitation
                            WHERE invitation."Id" = NEW."SourceReferenceId"
                              AND invitation."TenantId" = NEW."TenantId"
                              AND invitation."ProgrammeKey" = 'AQGREEN'
                              AND invitation."ProgrammeParticipationId" =
                                  NEW."CreditedSponsorParticipantId"
                              AND invitation."IsDeleted" = FALSE
                            FOR UPDATE;

                            IF NOT FOUND
                            THEN
                                RAISE EXCEPTION 'AQGreen member attribution requires matching invitation evidence.';
                            END IF;
                        END IF;

                        RETURN NEW;
                    END;
                    $function$;

                    CREATE TRIGGER "TR_AQGRecruitAttr_ValidateInsert"
                    BEFORE INSERT ON public."AQGreenRecruitmentAttributions"
                    FOR EACH ROW
                    EXECUTE FUNCTION "ValidateAQGreenRecruitmentAttributionInsert"();

                    ALTER TABLE public."AQGreenRecruitmentAttributions"
                        ENABLE ALWAYS TRIGGER "TR_AQGRecruitAttr_ValidateInsert";

                    CREATE FUNCTION "PreventAQGreenInvitationEvidenceMutation"()
                    RETURNS trigger
                    LANGUAGE plpgsql
                    SET search_path = pg_catalog
                    AS $function$
                    BEGIN
                        IF EXISTS (
                               SELECT 1
                               FROM public."AQGreenRecruitmentAttributions" attribution
                               WHERE attribution."SourceReferenceId" = OLD."Id"
                                 AND attribution."TenantId" = OLD."TenantId"
                                 AND attribution."CreditedSponsorParticipantId" =
                                     OLD."ProgrammeParticipationId"
                                 AND attribution."AcquisitionSource" = 1)
                           AND (TG_OP = 'DELETE'
                                OR NEW."Id" IS DISTINCT FROM OLD."Id"
                                OR NEW."TenantId" IS DISTINCT FROM OLD."TenantId"
                                OR NEW."ProgrammeKey" IS DISTINCT FROM OLD."ProgrammeKey"
                                OR NEW."ProgrammeParticipationId" IS DISTINCT FROM
                                    OLD."ProgrammeParticipationId")
                        THEN
                            RAISE EXCEPTION 'Referenced AQGreen invitation evidence cannot be deleted or rebound.';
                        END IF;

                        IF TG_OP = 'DELETE'
                        THEN
                            RETURN OLD;
                        END IF;

                        RETURN NEW;
                    END;
                    $function$;

                    CREATE TRIGGER "TR_AQGInvitationEvidence_PreventRebinding"
                    BEFORE UPDATE OF "Id", "TenantId", "ProgrammeKey", "ProgrammeParticipationId"
                    ON public."ProgrammeInvitations"
                    FOR EACH ROW
                    EXECUTE FUNCTION "PreventAQGreenInvitationEvidenceMutation"();

                    ALTER TABLE public."ProgrammeInvitations"
                        ENABLE ALWAYS TRIGGER "TR_AQGInvitationEvidence_PreventRebinding";

                    CREATE TRIGGER "TR_AQGInvitationEvidence_PreventDelete"
                    BEFORE DELETE ON public."ProgrammeInvitations"
                    FOR EACH ROW
                    EXECUTE FUNCTION "PreventAQGreenInvitationEvidenceMutation"();

                    ALTER TABLE public."ProgrammeInvitations"
                        ENABLE ALWAYS TRIGGER "TR_AQGInvitationEvidence_PreventDelete";

                    CREATE FUNCTION "ValidateAQGreenRecruitmentConfirmationInsert"()
                    RETURNS trigger
                    LANGUAGE plpgsql
                    SET search_path = pg_catalog
                    AS $function$
                    DECLARE
                        attribution_time timestamp with time zone;
                        attribution_kind integer;
                    BEGIN
                        IF NEW."RulesVersion" !~ '[^[:space:]]'
                        THEN
                            RAISE EXCEPTION 'AQGreen confirmation rules version must not be blank.';
                        END IF;

                        SELECT attribution."AttributedAt", attribution."AttributionKind"
                        INTO attribution_time, attribution_kind
                        FROM public."AQGreenRecruitmentAttributions" attribution
                        WHERE attribution."TenantId" = NEW."TenantId"
                          AND attribution."Id" = NEW."AttributionId";

                        IF NOT FOUND
                        THEN
                            RAISE EXCEPTION 'AQGreen confirmation requires attribution in the same Tenant.';
                        END IF;

                        IF (NEW."ConfirmationMethod" = 1 AND attribution_kind <> 1)
                           OR (NEW."ConfirmationMethod" = 2 AND attribution_kind <> 2)
                        THEN
                            RAISE EXCEPTION 'AQGreen confirmation method does not match attribution source.';
                        END IF;

                        IF NEW."ConfirmedAt" < attribution_time
                        THEN
                            RAISE EXCEPTION 'AQGreen confirmation cannot precede attribution.';
                        END IF;

                        RETURN NEW;
                    END;
                    $function$;

                    CREATE TRIGGER "TR_AQGRecruitConfirm_ValidateInsert"
                    BEFORE INSERT ON public."AQGreenRecruitmentAttributionConfirmations"
                    FOR EACH ROW
                    EXECUTE FUNCTION "ValidateAQGreenRecruitmentConfirmationInsert"();

                    ALTER TABLE public."AQGreenRecruitmentAttributionConfirmations"
                        ENABLE ALWAYS TRIGGER "TR_AQGRecruitConfirm_ValidateInsert";

                    CREATE FUNCTION "PreventAQGreenRecruitmentAttributionMutation"()
                    RETURNS trigger
                    LANGUAGE plpgsql
                    SET search_path = pg_catalog
                    AS $function$
                    BEGIN
                        RAISE EXCEPTION 'AQGreen recruitment attribution evidence is append-only.';
                    END;
                    $function$;

                    CREATE TRIGGER "TR_AQGRecruitAttr_AppendOnly"
                    BEFORE UPDATE OR DELETE ON public."AQGreenRecruitmentAttributions"
                    FOR EACH ROW
                    EXECUTE FUNCTION "PreventAQGreenRecruitmentAttributionMutation"();

                    ALTER TABLE public."AQGreenRecruitmentAttributions"
                        ENABLE ALWAYS TRIGGER "TR_AQGRecruitAttr_AppendOnly";

                    CREATE TRIGGER "TR_AQGRecruitAttr_PreventTruncate"
                    BEFORE TRUNCATE ON public."AQGreenRecruitmentAttributions"
                    FOR EACH STATEMENT
                    EXECUTE FUNCTION "PreventAQGreenRecruitmentAttributionMutation"();

                    ALTER TABLE public."AQGreenRecruitmentAttributions"
                        ENABLE ALWAYS TRIGGER "TR_AQGRecruitAttr_PreventTruncate";

                    CREATE TRIGGER "TR_AQGRecruitConfirm_AppendOnly"
                    BEFORE UPDATE OR DELETE ON public."AQGreenRecruitmentAttributionConfirmations"
                    FOR EACH ROW
                    EXECUTE FUNCTION "PreventAQGreenRecruitmentAttributionMutation"();

                    ALTER TABLE public."AQGreenRecruitmentAttributionConfirmations"
                        ENABLE ALWAYS TRIGGER "TR_AQGRecruitConfirm_AppendOnly";

                    CREATE TRIGGER "TR_AQGRecruitConfirm_PreventTruncate"
                    BEFORE TRUNCATE ON public."AQGreenRecruitmentAttributionConfirmations"
                    FOR EACH STATEMENT
                    EXECUTE FUNCTION "PreventAQGreenRecruitmentAttributionMutation"();

                    ALTER TABLE public."AQGreenRecruitmentAttributionConfirmations"
                        ENABLE ALWAYS TRIGGER "TR_AQGRecruitConfirm_PreventTruncate";
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                // Recruitment credit is financially significant evidence. Destructive rollback is safe only while empty.
                migrationBuilder.Sql("""
                    LOCK TABLE public."AQGreenRecruitmentAttributions",
                               public."AQGreenRecruitmentAttributionConfirmations"
                    IN ACCESS EXCLUSIVE MODE;

                    DO $block$
                    BEGIN
                        IF EXISTS (
                            SELECT 1
                            FROM public."AQGreenRecruitmentAttributions")
                           OR EXISTS (
                            SELECT 1
                            FROM public."AQGreenRecruitmentAttributionConfirmations")
                        THEN
                            RAISE EXCEPTION 'Cannot remove AQGreen recruitment attribution after evidence has been recorded.';
                        END IF;
                    END;
                    $block$;

                    DROP TRIGGER "TR_AQGRecruitConfirm_ValidateInsert"
                        ON public."AQGreenRecruitmentAttributionConfirmations";
                    DROP TRIGGER "TR_AQGRecruitAttr_ValidateInsert"
                        ON public."AQGreenRecruitmentAttributions";
                    DROP TRIGGER "TR_AQGRecruitConfirm_AppendOnly"
                        ON public."AQGreenRecruitmentAttributionConfirmations";
                    DROP TRIGGER "TR_AQGRecruitConfirm_PreventTruncate"
                        ON public."AQGreenRecruitmentAttributionConfirmations";
                    DROP TRIGGER "TR_AQGRecruitAttr_AppendOnly"
                        ON public."AQGreenRecruitmentAttributions";
                    DROP TRIGGER "TR_AQGRecruitAttr_PreventTruncate"
                        ON public."AQGreenRecruitmentAttributions";
                    DROP TRIGGER "TR_AQGInvitationEvidence_PreventRebinding"
                        ON public."ProgrammeInvitations";
                    DROP TRIGGER "TR_AQGInvitationEvidence_PreventDelete"
                        ON public."ProgrammeInvitations";
                    DROP FUNCTION "ValidateAQGreenRecruitmentConfirmationInsert"();
                    DROP FUNCTION "ValidateAQGreenRecruitmentAttributionInsert"();
                    DROP FUNCTION "PreventAQGreenInvitationEvidenceMutation"();
                    DROP FUNCTION "PreventAQGreenRecruitmentAttributionMutation"();
                    """);
            }

            migrationBuilder.DropTable(
                name: "AQGreenRecruitmentAttributionConfirmations");

            migrationBuilder.DropTable(
                name: "AQGreenRecruitmentAttributions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ProgrammeInvitations_Id_Tenant_Participation",
                table: "ProgrammeInvitations");
        }
    }
}
