# AQGreen V2 — D10 Area envelope correction

> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It cannot confirm or supersede a business-policy decision.

Owner authorizes the two narrow corrections and a local commit on `21af7fd10abf73108c415a7da747080c866a601f`. Preserve B6, MVP, recovery controls and legitimate same-Tenant Onyx Area movement. No production operations or activation.

Repository evidence: D10 validates only frozen native members' Areas; `StartEntryAsync` selects before invitation resolution and moves the invitee afterward. The resolver already acquires ordered sponsor/invitee transaction locks and reads the authoritative destination after acquiring them.

Implementation scope: validate the complete relevant live AQGreen population against the authorized current Tenant/Area assignment; validate joining's resolved destination through the existing joining gate before any participation/checkout obligation writes; stabilize checkout current-Area checks with the existing Area-transition lock. Reuse existing fixtures and PostgreSQL model-created schemas.

State: **IMPLEMENTED**, **TESTED** offline. Ready for Agent 2's narrow re-verification, not a claim of production readiness or cutover authorization.

Exact changes (backend paths relative to `AqualLifeStyle/9.4.2/aspnet-core/`):

- `src/AqualLifeStyle.Application/ProgrammeParticipations/AQGreenD10ProductionSelectors.cs`: verify all live same-Tenant AQGreen participations, including prospective members and deleted active records. Missing/deleted customer, unauthorized/missing/inactive Area, conflicting/missing effective current assignment, or uncommissioned prospective identity denies the complete V2 selection. Financial selection retains the existing ordered customer Area-transition locks in its owning transaction. Final joining revalidates D10 authority and the resolved destination. Invitee checking reads committed current Area/history in a fresh read transaction, so checkout cannot rely on a snapshot predating its Area-lock wait.
- `src/AqualLifeStyle.Application/ProgrammeParticipations/ClubMemberProgrammeParticipationAppService.cs`: validate the invitation destination after the resolver's sponsor/invitee locks, before clearing legacy membership or creating participation/attribution records, including replay. Checkout acquires the existing invitee Area-transition lock before scope checking/preparation. Its serializable transaction is preserved.
- `src/AqualLifeStyle.Core/Domain/AQGreen/IAQGreenPlacementV2JoiningGate.cs`, Application `DisabledAQGreenPlacementV2JoiningGate.cs`, Host `AQGreenV2Demo/AQGreenV2DemoSelectors.cs`, and `test/AqualLifeStyle.Web.Tests/AQGreenV2TestJoiningGate.cs`: extend the existing joining gate with destination validation; preserve uncommissioned V1 and explicitly isolated demo/test behavior.
- `src/AqualLifeStyle.Core/Domain/Payments/IHostedPaymentCheckoutLock.cs` and EF `EntityFrameworkCore/HostedPaymentCheckoutLock.cs`: read actual PostgreSQL `transaction_isolation` through the existing lock service. Final D10 joining and financial selection reject repeatable snapshots or missing transactions instead of trusting nullable ABP options. Existing lock acquisition behavior is unchanged.
- `test/AqualLifeStyle.Web.Tests/Integration/AQGreenD10AreaEnvelopePostgreSqlTests.cs`: reuse existing approval/identity/frozen-evidence fixtures; nine focused PostgreSQL vectors. Test-only source authority and gateways remain synthetic, with no network provider requests.
- `test/AqualLifeStyle.Tests/Application/ClubMemberProgrammeParticipationAppServiceTests.cs`: explicitly configure destination validation on the existing test-only V2 gate.
- `docs/aqua-system/06-operations-and-enablement-runbook.md`: describe complete live scope, transaction visibility and locked final destination/current checkout checks.

Final evidence, 2026-10-13:

| Check | Result | What executed |
| --- | --- | --- |
| Release Web.Tests build and referenced backend/host/test projects | Passed, zero errors; compiler/package warnings remain | Current code; no dependency changes |
| Synthetic PostgreSQL 16 application tests | 14 passed, zero failed/skipped | Nine new Area vectors plus five directly affected D10 cutover cases |
| Focused unit/component regressions | 32 passed, zero failed/skipped | D10 commissioning binding, affected V1/V2 joining, checkout, replay and legacy repeat-joining guards |
| Diff/staged whitespace check | Passed | Owned changes only |

PostgreSQL proof: a member joins Area A through the real application service, replays/concurrently retries without duplicates, prepares a synthetic checkout, receives synthetic payment confirmation and authorized approval, then moves to unauthorized B through the existing Onyx checkout/confirmation workflow. Tenant-wide V2 selection and the actual weekly calculator reject the population; no commission period/row/component is created, and the existing obligation/checkout/payment evidence remains unchanged. Pre-effective V1 selection still succeeds.

The sponsor-move test holds the existing Area-transition advisory lock, allows the first joining check, commits a domain Area/history move to B while invitation resolution waits, then observes `D10_JOINING_DESTINATION_OUTSIDE_AUTHORIZED_SCOPE`. No invitee participation/checkout/financial side effects or gateway calls occur; retry remains denied. A separate checkout race waits for the same member-Area lock and reads committed B after release, rejecting without preparing checkout or calling a provider. Both repeatable-read and serializable snapshots are denied for final joining/financial selection. Missing Area, missing current assignment, conflicting authorized Area/history and deleted prospective customer are also denied without business changes.

Raw ignored evidence: `AqualLifeStyle/9.4.2/aspnet-core/TestResults/d10-area/build.log`, `d10-area-verified-postgresql.trx`, `d10-area-verified-regression.trx`, and their logs. Synthetic schemas used `REPRO_PG_SCHEMA_FROM_MODEL=true`; no migration executed. An owned disposable loopback PostgreSQL container was used and removed after validation. No production access, providers, deployment, activation or Git publication occurred.

Earlier runs: two new fixture failures (missing due-policy foreign-key evidence and a new Area assignment not explicitly marked for insertion) were corrected without weakening assertions; eleven cases then passed. The added transaction check initially denied six cases because ABP's optional isolation metadata did not establish provider state; querying actual PostgreSQL visibility corrected that defect. The final fourteen-case execution passed. An earlier 63-case joining/commissioning regression run also passed before the final transaction refinements; the final 32 are overlapping focused cases, not additional unique coverage.

Remaining material Area defect identified in this scope: none. Agent 2 still needs to independently verify the correction. Existing production-dependent B6/schema/backup/owner-authority requirements remain unchanged. D10 defaults stay disabled and production API remains suspended.

Git context: `feature/aqgreen-v2-main-integration`, parent `21af7fd10abf73108c415a7da747080c866a601f`; earlier accepted commits/artifacts preserved. Owner authorized only a local correction commit. Next action: Agent 2 narrow re-verification of the live population and final joining/checkout destination checks.
