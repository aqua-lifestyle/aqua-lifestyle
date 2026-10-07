# Isolated prospective AQGreen V2 pilot

This is a local, non-production, test-money candidate commissioned by the human owner. It is not production activation, qualification, deployment, migration, or an authority acceptance. Existing AQGreen V2 product rules remain authoritative.

## Capability boundary

The pilot uses the existing joining, payment-confirmation, Area approval, D02 allocator, cutoff-effective progress, weekly sales review, and versioned commission engine. Host administration may record an explicit D03A prospective root admission. Invitation acceptance records immutable sponsor attribution and member confirmation before payment. Payment confirmation only reaches awaiting approval. Authorized approval atomically activates and places the participation, with the existing decision and outbox evidence.

Sales reviewers record evidence and quantities, including Held, Rejected, and Confirmed states. The system evaluates the frozen 5/5/5 rule; reviewers cannot set commission amounts. Closed-week AQGreen decisions use existing versioned terms and create explicitly marked test-money ledger entries. Test entries cannot be released, marked paid, or converted to real-money entries, including through database updates.

Production migration/backfill, historical reinterpretation, real payouts/entitlements, Onyx/combo, loans/graduation, sponsor corrections/topology repairs, uncommissioned lifecycle actions, live AR1/SS1/BIND/AS1, TQ1 qualification, confinement, Gate-Q/A, operational A/B, B7 and D10 remain closed. Deferral does not mean acceptance or qualification. The API commissioning interceptor denies unlisted application operations; financial processors, selectors and workers independently enforce their respective boundaries. UI hiding is an additional presentation control.

## Explicit local configuration

Use the exact `AQGreenV2Pilot` environment together with `AQGreenV2Pilot:Enabled=true`. The checked-in environment file defaults to disabled. Supply credentials through a protected local secret mechanism; never place them in source, a URL, or a report.

The candidate requires PostgreSQL on loopback using the exact dedicated database name `aqua_aqgreen_v2_pilot`. Client and server root URLs must be explicit loopback addresses. Shared Redis, external Yoco/Bird credentials, demo mode, monthly workers and automatic commission workers are forbidden. Startup refuses inconsistent settings. The same test-money database is refused outside the pilot environment.

Run the API and frontend on loopback only. This restriction is also operational: application root settings are not socket-listener settings. Set the API listen URLs and the frontend hostname explicitly. When overriding the API container port, set its existing `HEALTHCHECK_URL` to the exact loopback health endpoint as well; `ASPNETCORE_URLS` does not change the CLI probe’s default port. The pilot is not a public hosting configuration. Apply the additive pilot migration only to a newly created disposable pilot database, under the local validation authorization. No production connection is required or permitted.

Existing administrator permissions and Tenant/Area assignments are still required. Runtime code does not create an administrator or silently grant permissions. Identity fixtures used in development are not authority acceptance and must not be imported into another environment.

## User flow

1. An authorized host administrator opens participation administration, records the existing AQGreen test commission terms and prospective Tenant observation, and admits an exact prospective customer as a root with an explicit reason.
2. The root member opens joining, creates a test checkout, and confirms the test joining payment. Authorized approval creates the root placement.
3. An invited member joins through the active sponsor's invitation, creates and confirms a test checkout, and awaits their authorized Area administrator's approval.
4. Approval activates and places the member. Sponsor credit remains distinct from the placement parent, including spillover. The existing identity boundary ends the old Guest session after access changes; the member follows the sign-in prompt to obtain current permissions. Members view structural progress at the explicit pilot progress route, based on the existing cutoff-effective evaluator. The normal environment retains its journey redirect.
5. After a real week closes, an authorized reviewer records weekly sales evidence. The system evaluates 5/5/5 and calculates the versioned test ledger. No release/payment action is available.

Tests may control a development clock to exercise a closed-week calculation without waiting a week. The actual pilot host retains the real clock. Development-clock observations are not historical production facts or TQ1 original qualification evidence.

## Validation

The prospective PostgreSQL suite is `AQGreenIsolatedPilotPostgreSqlTests`. It requires `REPRO_PG=true`, `REPRO_TRANSACTIONAL=true`, `REPRO_PILOT=true`, and an explicitly supplied disposable connection through `REPRO_PG_CONNECTION` and `ConnectionStrings__Default`. Disabled tests are not passing PostgreSQL workflow evidence. Run the complete composed pilot suite on a fresh disposable database: an already closed commission period must replay its existing decision and cannot be reused to test newly introduced evidence for that period. Preserve failed development databases before resetting task-owned fixtures.

`playwright.pilot.config.ts` exercises the actual loopback API and frontend using Chrome. Its protected, test-only identity handoff file is supplied by `PILOT_BROWSER_FIXTURE_OUTPUT`; test passwords are supplied separately through protected environment variables. No business transition is seeded for the browser flow. Prepare fresh identity fixtures for each complete browser attempt, preserving the earlier attempt and its persisted test state. The browser suite must not be pointed at production or a preserved clone.

Follow repository validation for Release builds, full backend/frontend tests, TypeScript, lint, dependency review and PostgreSQL model/migration checks. Runtime, PostgreSQL, browser, inspection, and unit evidence remain distinct. No development test establishes TQ1 qualification or production readiness.

## Future gates

PB1's unresolved invocation contract remains a hard prerequisite before governed TQ1 parser reliance. TQ1 subject selection, external foundation, independent hashing/review, original 39-check execution, derivation and acceptance remain future governed work. AR1 bootstrap/population and SS1 selected/qualified supply remain unaccepted/unselected. Production migration, protected B6 runtime, complete BIND, AS1 custody, confinement, Gate-Q/A, signed supply/release, B7 and D10 retain their existing activation gates. They must remain unreachable until their own requirements and owner authority are satisfied.

No commit, push, PR, merge or deployment is authorized by this local pilot implementation.
