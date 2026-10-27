START TRANSACTION;

ALTER TABLE "AQGreenWeeklySalesEvidenceReferences" ADD CONSTRAINT "AK_AQGreenWeeklySalesEvidenceReferences_TenantId_DecisionId_Id" UNIQUE ("TenantId", "DecisionId", "Id");

CREATE TABLE "AQGreenSalesEvidenceInvalidations" (
    "Id" uuid NOT NULL,
    "TenantId" integer NOT NULL,
    "DecisionId" uuid NOT NULL,
    "EvidenceId" uuid NOT NULL,
    "Reason" character varying(1000) NOT NULL,
    "RecordedByUserId" bigint NOT NULL,
    "RecordedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_AQGreenSalesEvidenceInvalidations" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_AQGSalesInvalidation_Authority" CHECK ("TenantId" > 0 AND "RecordedByUserId" > 0),
    CONSTRAINT "CK_AQGSalesInvalidation_Reason" CHECK (length(trim("Reason")) > 0 AND "Reason" = trim("Reason")),
    CONSTRAINT "FK_AQGreenSalesEvidenceInvalidations_AQGreenWeeklySalesEvidenc~" FOREIGN KEY ("TenantId", "DecisionId", "EvidenceId") REFERENCES "AQGreenWeeklySalesEvidenceReferences" ("TenantId", "DecisionId", "Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_AQGreenSalesEvidenceInvalidations_TenantId_DecisionId" ON "AQGreenSalesEvidenceInvalidations" ("TenantId", "DecisionId");

CREATE INDEX "IX_AQGreenSalesEvidenceInvalidations_TenantId_DecisionId_Evide~" ON "AQGreenSalesEvidenceInvalidations" ("TenantId", "DecisionId", "EvidenceId");

CREATE UNIQUE INDEX "IX_AQGreenSalesEvidenceInvalidations_TenantId_EvidenceId" ON "AQGreenSalesEvidenceInvalidations" ("TenantId", "EvidenceId");

CREATE TRIGGER "TR_AQGSalesInvalidation_AppendOnly"
BEFORE UPDATE OR DELETE ON public."AQGreenSalesEvidenceInvalidations"
FOR EACH ROW EXECUTE FUNCTION public."PreventAQGreenRecruitmentAttributionMutation"();
ALTER TABLE public."AQGreenSalesEvidenceInvalidations" ENABLE ALWAYS TRIGGER "TR_AQGSalesInvalidation_AppendOnly";
CREATE TRIGGER "TR_AQGSalesInvalidation_NoTruncate"
BEFORE TRUNCATE ON public."AQGreenSalesEvidenceInvalidations"
FOR EACH STATEMENT EXECUTE FUNCTION public."PreventAQGreenRecruitmentAttributionMutation"();
ALTER TABLE public."AQGreenSalesEvidenceInvalidations" ENABLE ALWAYS TRIGGER "TR_AQGSalesInvalidation_NoTruncate";
CREATE FUNCTION public."ValidateAQGreenSalesInvalidation"()
RETURNS trigger LANGUAGE plpgsql SET search_path = pg_catalog AS $function$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM public."AQGreenWeeklySalesEligibilityDecisions" decision
        WHERE decision."TenantId" = NEW."TenantId" AND decision."Id" = NEW."DecisionId"
          AND decision."ReviewStatus" = 2 AND NEW."RecordedAtUtc" >= decision."ReviewedAt") THEN
        RAISE EXCEPTION 'Invalidation requires finalized confirmed evidence and an authoritative later time.';
    END IF;
    RETURN NEW;
END;
$function$;
CREATE TRIGGER "TR_AQGSalesInvalidation_Validate"
BEFORE INSERT ON public."AQGreenSalesEvidenceInvalidations"
FOR EACH ROW EXECUTE FUNCTION public."ValidateAQGreenSalesInvalidation"();
ALTER TABLE public."AQGreenSalesEvidenceInvalidations" ENABLE ALWAYS TRIGGER "TR_AQGSalesInvalidation_Validate";

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261021051549_AddAQGreenSalesEvidenceInvalidation', '8.0.8');

COMMIT;

