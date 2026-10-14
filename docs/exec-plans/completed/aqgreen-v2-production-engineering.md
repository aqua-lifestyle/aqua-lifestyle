# AQGREEN V2 — production engineering

> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

## Goal and authority

Owner direction, 13 October 2026: progress from the verified local integration
candidate to production-capable V2 for existing members, preserve accepted evidence,
correct one consequential offline blocker, and leave production disabled. No
production access/mutation, real transactions, payouts, deployment, Render changes,
push, merge, or preview overrides.

Governing references: [system index](../../aqua-system/README.md),
[legacy-member integrity](../../aqua-system/05-data-history-migrations-and-legacy-members.md#3-legacy-aqgreen-members),
and [V2 migration/cutover authority](../../aqua-system/aqgreen-network-placement-specification.md#342-cutover-and-migration-decisions).
Existing owner-confirmed sales-review delegation remains in document 07; no policy
is confirmed by this worklog.

## Three consequential blockers — repository evidence

1. **Existing-member provenance and admission.** Legacy AQGreen membership may exist
   without modern participation/payment history (document 05 §3). At `e11edda`, V2
   invitation joining reused `StartEntryAsync`, which cleared the legacy membership
   assignment before creating modern participation and its joining obligation.
   Checkout could charge an existing pending modern participation despite that
   assignment; this task corrects those two paths. Existing
   V2 replay correctly requires immutable attribution; it is not an import path.
   `GetLegacyAQGreenReconciliationAsync` starts from `EntryParticipations` and
   filters `JoiningInstallmentAmount == 0m`, so it cannot discover legacy-only
   customers with no participation, nor inventory the whole V2 migration population.
   D03B/D09 and B6 must resolve identity, sponsor, root/scope, real joining-payment,
   activation and effective Area evidence; missing evidence must remain unknown.
2. **Authoritative payment reconciliation.** `ProgrammePaymentConfirmationProcessor`
   validates provider-linked payments; it does not import legacy cash/bank receipts
   or establish historical payment from current Active state. Document 05 §11 records
   detection without a general audited resolution action. Real Yoco finality and
   settlement acceptance remains separate (document 07). B6 needs actual receipt
   provenance and authorized exception handling; no repeat joining or fake payments.
3. **Production selection and financial cutover.** Production joining, approval,
   progress and sales-review gates are disabled; commission/graduation selectors
   retain V1. Demo/pilot selectors are isolated and cannot supply production authority.
   D10/B7 must specify effective instant, Tenant/population scope, cycle alignment and
   activation authority before joining, approval/placement, sales and accounting use
   V2 consistently. Reuse existing startup/migrator/worker locks and financial gates.

These are code/document findings, not observations of today's production data.
The shortest path is additive reconciliation of existing identity/history, frozen
whole-scope simulation under approved D03B/D09, then coordinated V2 selection at an
authorized D10 boundary. Preserve V1 history and operations throughout.

## Selected correction

Protect known legacy AQGreen membership from prospective V2 joining or direct
checkout. Preserve the assignment and fail closed before creating participation,
attribution, Area movement or payment checkout. Do not infer payment/activation,
import members, or introduce a new override. Retain V1 behavior.

## Evidence and current state

- Base and current HEAD: `e11edda8bd55f2d5717cd00a56bb613ae9a628d5`.
- Branch: `feature/aqgreen-v2-main-integration`.
- Clean candidate worktree moved from disposable `/tmp` to
  `/home/wtc/Downloads/newAqua/aqua-lifestyle-v2-production-candidate`.
- Accepted MVP/integration evidence carried forward at that exact commit. No
  dependency, merge-conflict investigation, or acceptance walkthrough repeated.
- `IMPLEMENTED`: V2-only legacy-membership guard at admission and checkout, using
  existing repositories/transactions and the existing joining selector. No schema,
  startup, migration, worker, provider, financial formula or authorization changes.
  Document 05 describes the behavior and the remaining B6/B7 boundary.
- Before correction: four new SQLite application cases executed against unchanged
  service source; fresh joining passed and legacy joining incorrectly succeeded.
  Two checkout negatives reached an unstubbed gateway response and failed with
  NullReferenceException; the fixture was corrected to return a synthetic checkout.
- `dotnet restore AqualLifeStyle.sln` and Release solution build passed (0 errors,
  2704 compiler/analyzer warnings). Logs retained in backend
  `TestResults/production-engineering/` outside `/tmp`.
- `TESTED`: scoped SQLite application and selector component execution. Suite
  selected all cases in `ClubMemberProgrammeParticipationAppServiceTests` (41),
  `ProgrammePaymentConfirmationProcessorTests` (14), `AQGreenV2PilotBoundaryTests`
  (19), and `AQGreenV2DemoSelectorsTests` (1). Initial execution: 73 passed, 2 failed,
  0 skipped. Two new cross-Area fixtures failed before admission because registration
  needed an explicit Area after a second active Area was added. Fixture correction
  supplies Johannesburg explicitly; product validation/assertions remain intact.
  Rebuilt test project and reran precisely those two cases: 2 passed, 0 failed,
  0 skipped. All 75 selected identities have passing evidence across these runs,
  with unchanged application code. No complete-suite rerun was needed.
- New cases prove preservation of legacy membership and Area history, absence of
  new participation/attribution/payment, both joining schedules blocked before any
  gateway call, and fresh V2 joining/replay/checkout still works. Existing V1 legacy
  conversion and payment-to-awaiting-approval tests passed.
- Exact commands (backend directory):
  `dotnet restore AqualLifeStyle.sln`;
  `dotnet build AqualLifeStyle.sln --configuration Release --no-restore --verbosity quiet`;
  `dotnet test test/AqualLifeStyle.Tests/AqualLifeStyle.Tests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~ClubMemberProgrammeParticipationAppServiceTests|FullyQualifiedName~ProgrammePaymentConfirmationProcessorTests|FullyQualifiedName~AQGreenV2DemoSelectorsTests|FullyQualifiedName~AQGreenV2PilotBoundaryTests'`;
  then `dotnet test test/AqualLifeStyle.Tests/AqualLifeStyle.Tests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~V2Joining_LegacyAQGreenMembership|FullyQualifiedName~V2Joining_FreshMember'`.
  TRX files: `legacy-member-before.trx`, `legacy-member-after.trx`, and
  `legacy-member-corrected-fixtures.trx`, under `TestResults/production-engineering/`.
- Complete backend/frontend suites, PostgreSQL, browser E2E, provider acceptance,
  dependency reinvestigation and production checks not run in this task. No migration,
  locking, provider-delivery, production operation or complete V2 workflow claim.
- Final diff/whitespace, staged state and relevant untracked files inspected.
- Source/test/current-document/worklog changes only; no staging, commit, push, PR,
  merge or deployment. Production gates/selectors retain disabled/V1 behavior.

## Remaining dependencies and next action

B6 production-data reconciliation and D03B/D09 mapping remain unauthorized and need
actual source evidence. B7/D10 activation remains unauthorized. Missing member history
and joining-payment channel rules cannot be invented offline.

Next engineering task: extend existing read-only legacy reconciliation discovery
to include legacy-only membership assignments without modern participation. Adapt
the contract to represent that absence explicitly, preserve Tenant/Area permissions,
and test synthetic fixtures offline. This supplies an inventory entry point for B6;
it must not infer paid/Active state, assign sponsors/scopes, execute against
production, or generate an authoritative manifest before the applicable decisions.
