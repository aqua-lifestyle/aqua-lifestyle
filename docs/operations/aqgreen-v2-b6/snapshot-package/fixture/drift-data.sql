-- Test-only mutation applied only to an already verified package-owned SOURCE runtime.
UPDATE "EntryParticipations"
SET "RecruiterCustomerId" = 104
WHERE "Id" = '00000000-0100-0001-8000-000000000001'::uuid;
