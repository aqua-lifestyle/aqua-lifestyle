-- REAL-SCHEMA VALID FIXTURE: applied only after all current EF migrations.
-- Direct-identifier/free-text sentinel values must never leave Zone 2.
INSERT INTO "AbpTenants" ("Id","TenancyName","Name","IsActive","CreationTime","IsDeleted") VALUES
(1,'b6-one','DIRECT-TENANT-LEAK',true,'2026-08-01Z',false),
(2,'b6-two','DIRECT-TENANT2-LEAK',true,'2026-08-01Z',false);

INSERT INTO "Areas" ("Id","TenantId","Code","Name","IsActive","CreationTime","IsDeleted") VALUES
('a0000000-0000-0000-0000-000000000001',1,'B6A','DIRECT-AREA-LEAK',true,'2026-08-01Z',false),
('a0000000-0000-0000-0000-000000000002',2,'B6B','DIRECT-AREA2-LEAK',true,'2026-08-01Z',false);

INSERT INTO "AbpUsers" ("Id","TenantId","UserName","EmailAddress","Name","Surname","NormalizedUserName","NormalizedEmailAddress","Password","Role","IsEmailConfirmed","IsActive","CreationTime","IsDeleted","AccessFailedCount","IsLockoutEnabled","IsPhoneNumberConfirmed","IsTwoFactorEnabled")
SELECT 1000+n, 1, 'b6-user-'||n, 'b6-user-'||n||'@example.test', 'DIRECT-ALICE-LEAK', n::text,
       'B6-USER-'||n, 'B6-USER-'||n||'@EXAMPLE.TEST', 'TEST-ONLY-NOT-A-REAL-PASSWORD', 3,
       true,true,'2026-08-01Z',false,0,false,false,false FROM generate_series(1,7) n;
INSERT INTO "AbpUsers" ("Id","TenantId","UserName","EmailAddress","Name","Surname","NormalizedUserName","NormalizedEmailAddress","Password","Role","IsEmailConfirmed","IsActive","CreationTime","IsDeleted","AccessFailedCount","IsLockoutEnabled","IsPhoneNumberConfirmed","IsTwoFactorEnabled") VALUES
(2001,2,'b6-user-b','b6-user-b@example.test','DIRECT-TENANT2-USER','B','B6-USER-B','B6-USER-B@EXAMPLE.TEST','TEST-ONLY-NOT-A-REAL-PASSWORD',3,true,true,'2026-08-01Z',false,0,false,false,false);

INSERT INTO "Customers" ("Id","TenantId","Name","Email","AreaId","IsActive","CreationTime","ClubMemberNumber","UserId","IsDeleted")
SELECT 99+n,1,'DIRECT-CUSTOMER-'||n,'direct-'||n||'@example.invalid','a0000000-0000-0000-0000-000000000001',true,'2026-08-01Z','DIRECT-CLUB-'||n,1000+n,false FROM generate_series(1,7) n;
INSERT INTO "Customers" ("Id","TenantId","Name","Email","AreaId","IsActive","CreationTime","ClubMemberNumber","UserId","IsDeleted") VALUES
(200,2,'DIRECT-TENANT2-CUSTOMER','tenant2@example.invalid','a0000000-0000-0000-0000-000000000002',true,'2026-08-01Z','DIRECT-T2-CLUB',2001,false);

INSERT INTO "Memberships" ("Id","TenantId","Name","IsActive","MembershipType","MonthlyObligationAmount") VALUES
(1,1,'B6 Onyx fixture',true,2,0);

-- Mixed-endian GUID pair is intentional: .NET Guid.CompareTo orders P1 before P2,
-- while comparing Guid.ToByteArray() prefixes would suggest the reverse.
INSERT INTO "EntryParticipations" ("Id","TenantId","CustomerId","RecruiterCustomerId","Status","StartedAt","ActivatedAt","TermsVersion","TermsEffectiveFrom","JoiningPaymentAmount","JoiningInstallmentAmount","RegistrationPaymentAmount","ActivationPaymentAmount","MonthlyCommitmentAmount","GracePeriodDays","Currency","CreationTime","IsDeleted") VALUES
('00000000-0001-0100-8000-000000000001',1,100,NULL,2,'2026-08-01Z','2026-08-02Z','entry-terms-v1','2026-08-01Z',1200,600,600,600,600,7,'ZAR','2026-08-01Z',false),
('00000000-0100-0001-8000-000000000001',1,101,100,2,'2026-08-03Z','2026-08-04Z','entry-terms-v1','2026-08-01Z',1200,600,600,600,600,7,'ZAR','2026-08-03Z',false),
('00000000-0200-0001-8000-000000000001',1,102,100,0,'2026-08-05Z',NULL,'entry-terms-v1','2026-08-01Z',1200,600,600,600,600,7,'ZAR','2026-08-05Z',false),
('00000000-0300-0001-8000-000000000001',1,103,100,2,'2026-08-06Z','2026-08-07Z','entry-terms-v1','2026-08-01Z',1200,600,600,600,600,7,'ZAR','2026-08-06Z',false),
('10000000-0000-0000-8000-000000000010',1,104,NULL,2,'2026-08-10Z','2026-08-11Z','entry-terms-v1','2026-08-01Z',1200,600,600,600,600,7,'ZAR','2026-08-10Z',false),
('10000000-0000-0000-8000-000000000011',1,105,104,2,'2026-08-12Z','2026-08-13Z','entry-terms-v1','2026-08-01Z',1200,600,600,600,600,7,'ZAR','2026-08-12Z',false),
-- Isolated current Active root candidate with no approval row: it must remain
-- visible as a quarantined anomaly rather than disappearing from component output.
('11000000-0000-0000-8000-000000000012',1,106,NULL,2,'2026-08-14Z','2026-08-15Z','entry-terms-v1','2026-08-01Z',1200,600,600,600,600,7,'ZAR','2026-08-14Z',true),
('20000000-0000-0000-8000-000000000001',2,200,NULL,2,'2026-08-01Z','2026-08-02Z','entry-terms-v1','2026-08-01Z',1200,600,600,600,600,7,'ZAR','2026-08-01Z',false);

INSERT INTO "EntryParticipationApprovalDecisions" ("Id","EntryParticipationId","Approved","DecidedAt","AdministratorUserId","Reason") VALUES
('21000000-0000-0000-8000-000000000002','00000000-0100-0001-8000-000000000001',true,'2026-08-04Z',1001,'DIRECT-APPROVAL-REASON-LEAK'),
('21000000-0000-0000-8000-000000000004','00000000-0300-0001-8000-000000000001',true,'2026-08-07Z',1001,'DIRECT-APPROVAL-REASON-LEAK'),
('21000000-0000-0000-8000-000000000010','10000000-0000-0000-8000-000000000010',true,'2026-08-11Z',1001,'DIRECT-APPROVAL-REASON-LEAK'),
('21000000-0000-0000-8000-000000000011','10000000-0000-0000-8000-000000000011',true,'2026-08-13Z',1001,'DIRECT-APPROVAL-REASON-LEAK');

-- One cutoff-effective correction before activation and one correction after the
-- analysis cutoff exercise the exact EffectiveProgrammeNetwork ordering contract.
INSERT INTO "EntryRecruiterCorrections" ("Id","EntryParticipationId","PreviousRecruiterCustomerId","NewRecruiterCustomerId","CorrectedAt","AdministratorUserId","Reason") VALUES
('22000000-0000-0000-8000-000000000010','10000000-0000-0000-8000-000000000011',100,104,'2026-08-12T06:00:00Z',1001,'DIRECT-CORRECTION-REASON-LEAK'),
('22000000-0000-0000-8000-000000000011','00000000-0300-0001-8000-000000000001',102,100,'2026-09-03T01:00:00Z',1001,'DIRECT-CORRECTION-REASON-LEAK');

INSERT INTO "ProgrammeInvitations" ("Id","TenantId","ProgrammeKey","ProgrammeParticipationId","Code","CreationTime","IsDeleted") VALUES
('30000000-0000-0000-8000-000000000011',1,'AQGREEN','10000000-0000-0000-8000-000000000010','B6AQG000001','2026-08-12Z',false),
('30000000-0000-0000-8000-000000000099',1,'AQGREEN','00000000-0001-0100-8000-000000000001','B6UNRELATED1','2026-08-12Z',false),
('30000000-0000-0000-8000-000000000100',1,'ONYX','10000000-0000-0000-8000-000000000010','B6ONYX00001','2026-08-12Z',false),
('30000000-0000-0000-8000-000000000200',2,'AQGREEN','20000000-0000-0000-8000-000000000001','B6TENANT002','2026-08-12Z',false);

INSERT INTO "AQGreenRecruitmentAttributions" ("Id","TenantId","ParticipantId","CreditedSponsorParticipantId","AttributionKind","AcquisitionSource","SourceReferenceId","AttributedAt","AttributedByUserId","AssignmentReason","RulesVersion") VALUES
('31000000-0000-0000-8000-000000000010',1,'10000000-0000-0000-8000-000000000010',NULL,2,2,'39000000-0000-0000-8000-000000000010','2026-08-10T01:00:00Z',1001,'Reviewed direct admission fixture','AQGreenRecruitmentAttributionV1'),
('31000000-0000-0000-8000-000000000011',1,'10000000-0000-0000-8000-000000000011','10000000-0000-0000-8000-000000000010',1,1,'30000000-0000-0000-8000-000000000011','2026-08-12T01:00:00Z',NULL,NULL,'AQGreenRecruitmentAttributionV1');
INSERT INTO "AQGreenRecruitmentAttributionConfirmations" ("Id","TenantId","AttributionId","ConfirmedAt","ConfirmedByUserId","ConfirmationMethod","EvidenceReferenceId","RulesVersion") VALUES
('32000000-0000-0000-8000-000000000010',1,'31000000-0000-0000-8000-000000000010','2026-08-10T02:00:00Z',1001,2,'39000000-0000-0000-8000-000000000010','AQGreenRecruitmentAttributionV1'),
('32000000-0000-0000-8000-000000000011',1,'31000000-0000-0000-8000-000000000011','2026-08-12T02:00:00Z',1001,1,'30000000-0000-0000-8000-000000000011','AQGreenRecruitmentAttributionV1');

-- Existing V2 evidence is conflict inventory only; it never authorises B6 cutover.
INSERT INTO "AQGreenPlacementTreeScopes" ("Id","TenantId") VALUES
('40000000-0000-0000-8000-000000000001',1);
INSERT INTO "AQGreenNetworkPlacements" ("Id","TenantId","PlacementTreeScopeId","ParticipantId","PlacementParentParticipantId","PlacementSlot","CanonicalPath","PlacedAt","RulesVersion") VALUES
('41000000-0000-0000-8000-000000000001',1,'40000000-0000-0000-8000-000000000001','00000000-0001-0100-8000-000000000001',NULL,NULL,'','2026-08-20Z','AQGreenPlacementV2');

INSERT INTO "EntryCommissionPeriods" ("Id","TenantId","PeriodStart","PeriodEnd","TimeZoneId","CalculatedAt","RulesVersion","CreationTime","IsDeleted") VALUES
('50000000-0000-0000-8000-000000000001',1,'2026-08-17Z','2026-08-24Z','Africa/Johannesburg','2026-08-25Z','AQGreenCommissionV1','2026-08-25Z',false),
('50000000-0000-0000-8000-000000000002',1,'2026-08-24Z','2026-08-31Z','Africa/Johannesburg','2026-09-01Z','AQGreenCommissionV1','2026-09-01Z',false);
INSERT INTO "EntryWeeklyCommissions" ("Id","TenantId","EntryParticipationId","CustomerId","CommissionPeriodId","StructuralModel","CommissionDecisionRulesVersion","HighestCompletedLevel","TotalAmount","Currency","RulesVersion","CalculatedAt","PayoutStatus","ReleasedAt","PaidAt","PaymentReference","CreationTime","IsDeleted") VALUES
('51000000-0000-0000-8000-000000000001',1,'10000000-0000-0000-8000-000000000011',105,'50000000-0000-0000-8000-000000000001',1,NULL,1,100,'ZAR','AQGreenCommissionV1','2026-08-25Z',4,'2026-08-26Z','2026-08-27Z','DIRECT-PAYMENT-REF-LEAK','2026-08-25Z',false),
('51000000-0000-0000-8000-000000000002',1,'00000000-0100-0001-8000-000000000001',101,'50000000-0000-0000-8000-000000000002',1,NULL,0,25,'ZAR','AQGreenCommissionV1','2026-09-01Z',3,'2026-09-02Z',NULL,'DIRECT-RELEASE-REF-LEAK','2026-09-01Z',false);

INSERT INTO "OnyxLoanAgreements" ("Id","TenantId","EntryParticipationId","CustomerId","Status","TermsVersion","PrincipalAmount","InterestRatePercent","TotalPayableAmount","OutstandingAmount","Currency","RepaymentPeriodMonths","InitialWeeklyRequirementCount","InitialWeeklyMinimumAmount","OfferedAt","MemberAcceptedByUserId","MemberConfirmation","MemberAcceptedAt","ApprovedByAdministratorUserId","ApprovedAt","EffectiveAt","RepaymentDeadlineAt","LastAssessedAt","SettledAt","CreationTime","IsDeleted") VALUES
('60000000-0000-0000-8000-000000000001',1,'00000000-0100-0001-8000-000000000001',101,2,'onyx-loan-v1',1000,5,1050,1050,'ZAR',12,4,50,'2026-08-14Z',1002,'DIRECT-MEMBER-CONFIRMATION-LEAK','2026-08-14T01:00:00Z',1001,'2026-08-14T02:00:00Z','2026-08-14T02:00:00Z','2027-08-14Z','2026-08-20Z',NULL,'2026-08-14Z',false);

INSERT INTO "OnyxParticipations" ("Id","TenantId","CustomerId","RecruiterCustomerId","OnyxMembershipId","AdmissionRoute","Status","StartedAt","ActivatedAt","DirectEntryPaymentId","EntryParticipationId","LoanAgreementId","TermsVersion","TermsEffectiveFrom","DirectEntryAmount","Currency","CreationTime","IsDeleted") VALUES
('61000000-0000-0000-8000-000000000001',1,101,NULL,1,1,1,'2026-08-15Z','2026-08-16Z',NULL,'00000000-0100-0001-8000-000000000001','60000000-0000-0000-8000-000000000001','onyx-terms-v1','2026-08-01Z',0,'ZAR','2026-08-15Z',false);

INSERT INTO "OnyxGraduationDecisions" ("Id","TenantId","CustomerId","EntryParticipationId","LoanAgreementId","OnyxParticipationId","AdministratorUserId","DecidedAt","Justification","StructuralModel","GraduationRulesVersion","EvaluatedNetworkLevel","AQGreenWasActive","LoanWasActive","LoanWasAccepted","LoanWasAdministratorApproved","EvaluatedFundingAmount","EvaluatedFundingCurrency","EvaluatedLoanTermsVersion","CreationTime") VALUES
('62000000-0000-0000-8000-000000000001',1,101,'00000000-0100-0001-8000-000000000001','60000000-0000-0000-8000-000000000001','61000000-0000-0000-8000-000000000001',1001,'2026-08-16T01:00:00Z','DIRECT-GRADUATION-REASON-LEAK',1,'OnyxGraduationV1',2,true,true,true,true,1000,'ZAR','onyx-loan-v1','2026-08-16T01:00:00Z');

-- Assertions establish fixture coherence at the authoritative PostgreSQL layer.
DO $fixture$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM "EntryWeeklyCommissions" WHERE "PayoutStatus"=4 AND "PaidAt" IS NOT NULL AND "ReleasedAt" IS NOT NULL) THEN
    RAISE EXCEPTION 'Paid fixture status/time combination is incoherent';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM "EntryWeeklyCommissions" WHERE "PayoutStatus"=3 AND "ReleasedAt" IS NOT NULL AND "PaidAt" IS NULL) THEN
    RAISE EXCEPTION 'Released fixture status/time combination is incoherent';
  END IF;
END $fixture$;
