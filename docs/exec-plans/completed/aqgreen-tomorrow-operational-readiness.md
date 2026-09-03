# AQGreen tomorrow operational readiness

**Status:** COMPLETE — LOCAL/DEMO ONLY

> **NOT BUSINESS AUTHORITY.** This execution record does not authorize the B5.3
> production write gate, D10, a V2 production selector, or B6.

## Goal and fixed boundary

Determine whether a real Club Member and host System Administrator can operate
and observe the accepted AQGreen B5.1-B5.4 flow through the product UI. Preserve
the accepted 5/5/5 rule, commission rates, Level-2 graduation rule, B4/B5
architecture, durable ledger semantics, and payout controls.

The accepted backend baseline is commit
`4d3892f630c5025c0bf2244a7f8d387c5bf2a1d3`; its separate, uncommitted continuous
fresh-data E2E passed in the original worktree.

## Worktree isolation

- Original worktree: `/home/wtc/Downloads/newAqua/aqua-lifestyle`
- Original branch: `main`
- Original `HEAD` and `origin/main`: `4d3892f630c5025c0bf2244a7f8d387c5bf2a1d3`
- Original dirty work: only the accepted continuous-E2E module, gates, test, and
  plan; it remains untouched.
- Readiness worktree:
  `/home/wtc/Downloads/newAqua/aqua-lifestyle-aqgreen-tomorrow-readiness`
- Readiness branch: `feat/aqgreen-tomorrow-operational-readiness`
- Delivery was authorized only after implementation, validation, and independent
  review completed; source remained unstaged throughout the review loop.

## Actual route/API map

| Actor | Route | Page/component | Application API | Permission and scope | Result |
| --- | --- | --- | --- | --- | --- |
| Member | `/member/dashboard` | `MemberDashboard` | `ClubMemberProgrammeParticipation/GetMyParticipations` plus customer/order/membership APIs | self permissions | Participation summary only; earnings are not shown here |
| Member | `/member/programmes` | `MemberProgrammes` -> `ProgrammeJourneyOverview` | `ClubMemberProgrammeProgress/GetMyJourney` and `ClubMemberProgrammeParticipation/GetMyParticipations` | `Aqua.ProgrammeParticipations.ViewSelf`; current Tenant + signed-in user -> active Customer | Primary progress and earnings UI |
| Member | `/member/programme-progress` | server redirect | redirects to `/member/programmes` | same | Compatibility route, not a separate V1 screen |
| Member | `/member/invitations` | `InviteClubMembers` | `ProgrammeInvitation/GetMyInvitations` | `Aqua.ProgrammeParticipations.Invite` | Existing referral/invitation access |
| Admin | `/admin/dashboard` | `AdminDashboard` | dashboard links/summary hooks | granted admin permissions | Links to participation, earnings, and new review page |
| Admin | `/admin/programme-participations` | `AdminProgrammeParticipations` | `AdminProgrammeParticipation/GetAll`, payment/approval/rejection mutations | participation permissions; host or existing Area/Tenant enforcement per action | Existing operational participation workflow; host review link added for active AQGreen |
| Admin | `/admin/weekly-earnings` | `AdminWeeklyEarnings` | `AdminCommission/GetAll`, calculate/release/payment actions | `Aqua.Admin.Commissions.View`; host-wide reads require `Aqua.Admin.AllTenants` | Durable R400 and payout state visible |
| Admin | `/admin/weekly-sales-reviews` | `AdminWeeklySalesReviews` | new reads `AdminAQGreenWeeklySalesEligibility/GetAll`, `Get`, `GetLatestClosedWeek`; existing writes `BeginReview`, `Confirm`, `Reject` | host review permission + host session + `AllTenants` + host scope; writes additionally require enabled gate | UI/read path implemented; live writes production-blocked |

## Member commission visibility result

Classification: **A - the existing member API returns the durable R400 and the
existing frontend renders it.** No member earnings subsystem was rebuilt.

`GetMyJourney` binds current Tenant and current user to their active Customer and
then reads that Customer's `EntryWeeklyCommission` records and components. Its
`MemberProgrammeCycleEarningDto` preserves:

- period start/end;
- total amount;
- business payout status;
- hold reason and `Not earned` zero reason;
- qualified structural level;
- commissioned level;
- per-level components.

The primary journey UI renders the canonical week, qualified and commissioned
depth separately, Level 1 and Level 2 components, total, hold/zero explanation,
and the payout label. An exact R400 regression now proves Level 2 qualified,
Level 2 commissioned, R150 + R250 = R400, `Earned - awaiting release`, and no
false `Paid` label. Existing and new tests also prove Level 2 qualified with
commissioned Level 0/R0 remains `Not earned` without erasing structural level.

Supported payout labels are `Not earned`, `Earned - awaiting release`, `On hold`,
`Released - awaiting payment`, and `Paid`.

## AQGreen progress and Onyx

- With the explicit V2 test progress gate, the member journey renders qualified
  Level 2 and Level 3 as the next structural target. The Level-1 rail shows `5`
  qualifying placement occupants; it does not expose a separate `Direct recruits:
  5` metric. Personal/direct invitation access is presented separately and does
  not redefine structural completion.
- Production `DisabledAQGreenPlacementV2ProgressGate` remains disabled, so a live
  production-shaped host still projects V1 progress until D10/cutover authority.
- Onyx is already represented as a separate programme journey. A graduated,
  loan-backed participation is shown as active with the joining description
  `AQGreen graduation with an Onyx loan`.
- Admins can see Onyx through existing programme-participation and loan screens.
  No new Onyx subsystem was added.

## Admin B5.3 investigation

Baseline had production mutations only:

- `BeginReview` creates `HeldForEvidence`;
- `Confirm` records verified Spray/1L/5L and evidence references, and the domain
  evaluator computes `Met` or `NotMet`;
- `Reject` records evidence references and a required reason, with a null threshold.

Baseline had no queue/detail reads, no frontend route, no navigation, and no
production caller of `BeginReview`.

The minimal host-only slice adds:

- queue/history reads for held and finalized decisions;
- decision detail;
- latest canonical closed-week context for an explicitly selected active AQGreen
  participant;
- member, Area, week, rules, evidence, quantities, reviewer, time, rejection,
  and final system result;
- a participation-screen link, admin sidebar/dashboard link, review form, confirm,
  and reject actions;
- read-only rendering after a decision becomes Confirmed or Rejected.

The administrator never supplies threshold result or commission amount. R400 is
not editable on this screen. Evidence remains bounded textual/technical references;
no receipt-upload or Verified Commerce architecture was introduced.

## Authorization

- Host System Administrator: permitted when granted the host-only review permission
  and `Aqua.Admin.AllTenants`.
- Tenant Administrator: denied by permission side and the explicit host-session check.
- Area Administrator/Area Leader: denied; no authority was broadened.
- Reads use the same host authorization and scope policy as writes. Cross-Tenant
  filters are disabled only after those checks; participation and Customer are
  joined on both participant identity and Tenant.
- Member earnings remain self-only via current Tenant + current user -> Customer.

## Prior verdict and resolved environmental blocker

The first operational-readiness pass was **PARTIALLY READY**. The single critical
blocker was the absence of a browser-runnable non-production Web Host registration
for the required dormant V2 reads/calculation and B5.3 mutation gate.

That blocker is resolved locally by the dedicated `AQGreenV2Demo` environment. It
is test infrastructure only: **NOT BUSINESS AUTHORITY** and **NOT PRODUCTION
CUTOVER**.

## Non-production safety design

Demo activation requires all four independent conditions:

1. the exact, case-sensitive environment name `AQGreenV2Demo`;
2. explicit `AQGreenV2Demo__Enabled=true` opt-in;
3. an explicit PostgreSQL connection where every Npgsql host endpoint is
   deterministically local/loopback; and
4. a PostgreSQL database name exactly
   `aqua_aqgreen_v2_demo`.

The guard runs at the beginning of `Startup`, again during deployment validation,
and before demo service replacement/fixture setup. It refuses startup when:

- the flag is true in `Production`;
- the flag is true in any environment other than the exact demo environment;
- the exact demo environment lacks the flag;
- the connection is absent, malformed, a placeholder, contains any non-loopback
  host endpoint, or names any other database.

The safety model is the environment boundary plus explicit opt-in plus the
local-host database boundary plus the dedicated database name. A demo-looking
database name is not sufficient. Host validation uses the parsed Npgsql `Host`
value, checks every comma-separated endpoint, and does not use DNS resolution.

An actual `Production + flag=true` host process exited with code 1 and the explicit
message: `NON-PRODUCTION AQGreen V2 demo mode was requested in Production. The
application refuses to start.`

Production registrations remain unchanged. `DisabledAQGreenWeeklySalesReviewGate`
still always returns false; production progress remains disabled and commission and
graduation remain `LegacyV1`. D10 remains disabled and B6 was not started.

## Independent review correction

- Independent review: **REVISE BEFORE COMMIT**.
- Finding: the disposable database guard required the demo database name but did
  not restrict the PostgreSQL host, so a remote host with the correct demo database
  name could pass configuration validation.
- Correction: demo activation now requires every Npgsql host endpoint to be an
  explicit local/loopback hostname or IP address, in addition to the exact
  `aqua_aqgreen_v2_demo` database name. Mixed local/remote host lists fail closed.
- Focused validation: remote host + correct demo database rejected; mixed
  localhost + remote host + correct demo database rejected; `localhost`,
  `127.0.0.1:55434`, `::1`, and expanded IPv6 loopback accepted; local host + wrong
  database and malformed connection strings rejected; Production + flag rejected.
- Narrow independent re-review: **PASS — NO MATERIAL FINDINGS**.
- Independent acceptance: **ACCEPTED**. The review loop is **CLOSED**.

## Selectors and gates

Only the seams required by this prepared-fixture browser flow are replaced:

| Component | Production implementation | Demo implementation | Why required / omitted | Safety boundary |
| --- | --- | --- | --- | --- |
| Placement approval | `DisabledAQGreenPlacementV2ApprovalGate` | unchanged | Fixture prepares the accepted Level-2 topology; tomorrow does not test placement approval | normal production/default registration |
| Progress | `DisabledAQGreenPlacementV2ProgressGate` | `AQGreenV2DemoProgressGate` | Member must see the prepared V2 Level 2 topology | exact environment + flag + DB guard |
| Graduation | `LegacyV1AQGreenGraduationStructuralModelSelector` | unchanged | Primary review/R400 flow does not perform graduation; no D10 leak | normal production/default registration |
| B5.3 review | `DisabledAQGreenWeeklySalesReviewGate` | `AQGreenV2DemoSalesReviewGate` | Host must confirm/reject through the real application service | exact environment + flag + DB guard |
| Commission | `LegacyV1AQGreenCommissionStructuralModelSelector` | `AQGreenV2DemoCommissionSelector` | Existing B5.4 action must consume B5.3 and write the V2 R400 ledger | exact environment + flag + DB guard |

The placement and graduation seams were deliberately not enabled merely because
they exist. No `V2EffectiveAt`, automatic selector, legacy migration, dual-write,
or production rollout policy was introduced.

## Disposable demo database and fixture

The environment uses PostgreSQL 16 in the explicitly named local container
`aqua-aqgreen-v2-demo-pg`, bound to loopback port `55434`, with database
`aqua_aqgreen_v2_demo`. Host admin and member browser sessions share this database.
Remote and shared PostgreSQL hosts are refused. No production connection or
Customer data is used.

With `AQGreenV2Demo__Fixture__Enabled=true`, the guarded idempotent fixture creates:

- host `admin` from the existing host seed, retaining System Administrator,
  `Aqua.Admin.AllTenants`, and the B5.3 review permission;
- member `aqgreen.demo.member` / `aqgreen.demo.member@example.test` in Area
  workspace `Default`, with the existing Member role;
- one real active AQGreen root plus the minimum real 5 + 25 placement topology
  required to represent structural Level 2;
- active joining/payment/Area prerequisites and R150/R250/R1250 commission terms;
- one latest closed canonical root week in `HeldForEvidence`, with no root evidence,
  final review, or commission ledger;
- five finalized zero-quantity NotMet supporting decisions required because B5.4
  evaluates all structurally qualified participants; and
- 25 Level-0 supporting participants.

The fixture prepares expensive structure but does not pre-complete the action being
demonstrated: the root 5/5/5 confirmation and root R400 ledger are absent until the
host acts. Onyx graduation is not fabricated by this fixture and is outside the
primary success bar.

If a persisted fixture crosses into a new canonical week, setup fails with an
instruction to recreate the disposable database instead of silently changing demo
history.

## Exact B5.4 trigger

B5.4 is not triggered directly by B5.3 finalization. The legitimate existing
operational mechanism is:

`/admin/weekly-earnings` -> choose Area -> `Prepare weekly earnings` ->
`AdminCommission/CalculateLatestClosedWeek` -> `WeeklyCommissionCalculator.CalculateEntryAsync`.

The demo leaves `App:WeeklyCommissions:Enabled=false`. This is intentional: an
automatic worker could calculate `NotEarned` before the manual B5.3 review, and the
durable idempotency boundary would then prevent the expected R400 replacement.
No new B5.4 button or per-member amount-authoring path was added.

## Changes made

### Backend

- Preserved the earlier host-only B5.3 queue/detail/latest-week read contracts and
  admin participation context.
- Added the fail-closed `AQGreenV2Demo` environment/config/database guard.
- Added environment-scoped progress, B5.3, and commission service replacements.
- Added the guarded deterministic disposable PostgreSQL fixture.
- Added configuration and selector tests. No migration or schema change was added.

### Frontend

- Preserved the earlier `/admin/weekly-sales-reviews` implementation and existing
  member earnings UI.
- Added a separate, opt-in, real-backend Playwright configuration and scenario;
  it does not start or use the mock server.
- Excluded the real Playwright directory from Vitest discovery.

### Explicit non-changes

- No commission rate, 5/5/5 rule, Level-2/graduation rule, or B4/B5 architecture
  changed.
- No production selector/gate, D10, B6, schema, or production data changed.
- No B5.4 calculation button was added.

## Real browser/backend/PostgreSQL proof

A fresh post-correction disposable database rerun passed 1/1 in 22.9 seconds:

1. real Chromium signed in to host Platform administration;
2. `/admin/weekly-sales-reviews` loaded the real held row;
3. the host entered evidence plus 5/5/5 and confirmed;
4. PostgreSQL persisted `Confirmed + Met`, quantities 5/5/5, and one evidence ref;
5. the host used the existing `/admin/weekly-earnings` action;
6. the calculator evaluated 31 participants, recorded one earned result, and wrote
   R400 total;
7. the admin UI showed the member, commissioned Level 2, R400, and
   `Earned - awaiting release`;
8. a distinct real member browser session opened `/member/programmes` and showed
   qualified Level 2, commissioned Level 2, R150, R250, R400, 21-27 Aug 2026,
   and `Earned - awaiting release`, with no false `Paid` claim.

Direct PostgreSQL verification of the same durable graph returned structural model
2, qualified Level 2, total 400.00, Level-1 component 150.00, and Level-2 component
250.00. The restarted Web Host recognized the persisted fixture and `/api/health`
reported `Healthy`, database reachable, environment `AQGreenV2Demo`.

## Authorization and security evidence

- Real host read request: HTTP 200.
- Real tenant Administrator request to the same B5.3 read: HTTP 403.
- Real member request to the same B5.3 read: HTTP 403.
- Area Leader role tests prove it lacks both `AllTenants` and the B5.3 review
  permission; no role grant was changed.
- Member journey remains a self-bound current Tenant/current user query and accepts
  no other member/customer identifier.
- The host UI and APIs never accept `ThresholdResult`, commissioned level, or money.
  Met/NotMet and R400 remain system-computed.
- Final states remain immutable; real PostgreSQL Confirmed/NotMet and Rejected
  application tests pass.

## Validation evidence

- Post-correction real browser -> frontend -> ASP.NET -> PostgreSQL primary flow:
  1/1 pass.
- Focused demo configuration/startup tests: 37/37 pass in Release, including
  remote correct-name, mixed-host, loopback, wrong-database, malformed, Production,
  and unchanged non-demo cases.
- Earlier focused read/authorization and PostgreSQL application evidence remains
  accepted and was not rerun because those sources are unchanged.
- Real PostgreSQL NotMet and Rejected application tests: 2/2 pass.
- Actual Production startup rejection: pass (exit 1 with explicit demo guard).
- Actual demo startup with remote host + correct demo database: rejected before
  normal operation (exit 1, no credential or connection-string exposure).
- Actual corrected Release demo startup with `127.0.0.1:55434`, fixture setup, and
  health/database check: pass.
- Frontend unit/component suite: 121 files, 463 tests pass.
- Frontend TypeScript: pass.
- Frontend lint: pass.
- Frontend production build: pass; 65 routes generated, including both critical
  routes.
- Backend Release build: pass, 0 errors and 3 pre-existing warnings (one
  AngleSharp advisory and two xUnit analyzer warnings).
- Mock-backed readiness Playwright suite from the earlier pass: 16/16 pass; not used
  as primary proof.
- `git diff --check`: pass.

One preliminary post-correction browser rerun attempt failed before authentication
because the generated ephemeral validation passwords exceeded the application's
32-character input limit. No product assertion failed. The disposable database was
removed, the stack was recreated with valid-length generated passwords, and the
required fresh rerun then passed 1/1 as recorded above.

## Critical-flow inventory

| Action | State | Reason |
| --- | --- | --- |
| Member login | READY | Real distinct member browser login passed |
| Member AQGreen progress | READY IN DEMO | Real V2 progress read showed qualified Level 2 |
| Member weekly R400 | READY IN DEMO | Same durable PostgreSQL ledger rendered R150 + R250 = R400 |
| Member 5/5/4 R0 | READY BY CONTRACT | Existing exact UI coverage plus real PostgreSQL NotMet application path; not a second demo fixture |
| Admin participation/payment approval | READY | Existing workflow unchanged; fixture starts after this prerequisite |
| Admin B5.3 queue/detail/mutation | READY IN DEMO | Real host browser Confirm passed; Rejected PostgreSQL path and UI regression pass |
| Admin durable R400 view | READY IN DEMO | Real admin browser showed the generated ledger |
| Onyx/graduation visibility | NOT REQUIRED FOR PRIMARY DEMO | Existing UI remains; fixture does not fabricate graduation |
| Production V2/B5.3 writes | DISABLED | Deliberately unchanged and fail-closed |

## Tomorrow startup runbook

All commands run from the readiness worktree. The first block intentionally creates
and later destroys only the exact disposable demo container.

### Terminal 1: database, migration, and backend

```bash
cd /home/wtc/Downloads/newAqua/aqua-lifestyle-aqgreen-tomorrow-readiness/AqualLifeStyle/9.4.2

read -rsp 'Local demo host admin password (16+ chars; upper/lower/number/special): ' AQGREEN_DEMO_ADMIN_PASSWORD
printf '\n'
read -rsp 'Local demo member password (16+ chars): ' AQGREEN_DEMO_MEMBER_PASSWORD
printf '\n'
export AQGREEN_DEMO_ADMIN_PASSWORD AQGREEN_DEMO_MEMBER_PASSWORD
export AQGREEN_DEMO_DB_PASSWORD="$(openssl rand -hex 24)"
export AQGREEN_DEMO_JWT_KEY="$(openssl rand -hex 48)"
export AQGREEN_DEMO_YOCO_KEY="sk_test_$(openssl rand -hex 24)"

if docker container inspect aqua-aqgreen-v2-demo-pg >/dev/null 2>&1; then
  docker rm -f aqua-aqgreen-v2-demo-pg
fi

docker run -d --name aqua-aqgreen-v2-demo-pg \
  -e POSTGRES_DB=aqua_aqgreen_v2_demo \
  -e POSTGRES_USER=aqua_demo \
  -e POSTGRES_PASSWORD="$AQGREEN_DEMO_DB_PASSWORD" \
  -p 127.0.0.1:55434:5432 postgres:16-alpine

until docker exec aqua-aqgreen-v2-demo-pg \
  pg_isready -U aqua_demo -d aqua_aqgreen_v2_demo >/dev/null 2>&1; do sleep 1; done

export AQGREEN_DEMO_CONNECTION="Host=127.0.0.1;Port=55434;Database=aqua_aqgreen_v2_demo;Username=aqua_demo;Password=$AQGREEN_DEMO_DB_PASSWORD"

cd aspnet-core
ASPNETCORE_ENVIRONMENT=AQGreenV2Demo \
AQUA_INITIAL_ADMIN_PASSWORD="$AQGREEN_DEMO_ADMIN_PASSWORD" \
ConnectionStrings__Default="$AQGREEN_DEMO_CONNECTION" \
dotnet run --project src/AqualLifeStyle.Migrator/AqualLifeStyle.Migrator.csproj -- -q

ASPNETCORE_ENVIRONMENT=AQGreenV2Demo \
AQGreenV2Demo__Enabled=true \
AQGreenV2Demo__Fixture__Enabled=true \
AQGreenV2Demo__Fixture__MemberPassword="$AQGREEN_DEMO_MEMBER_PASSWORD" \
AQUA_INITIAL_ADMIN_PASSWORD="$AQGREEN_DEMO_ADMIN_PASSWORD" \
ConnectionStrings__Default="$AQGREEN_DEMO_CONNECTION" \
Authentication__JwtBearer__SecurityKey="$AQGREEN_DEMO_JWT_KEY" \
Yoco__Mode=test \
Yoco__SecretKey="$AQGREEN_DEMO_YOCO_KEY" \
ASPNETCORE_URLS=http://127.0.0.1:5000 \
dotnet run --no-launch-profile --project src/AqualLifeStyle.Web.Host/AqualLifeStyle.Web.Host.csproj
```

### Terminal 2: frontend

```bash
cd /home/wtc/Downloads/newAqua/aqua-lifestyle-aqgreen-tomorrow-readiness/AqualLifeStyle/9.4.2/aqua-frontend
export NEXT_PUBLIC_ABP_API_URL=http://127.0.0.1:5000
export NEXT_PUBLIC_DEFAULT_TENANT_NAME=Default
export NEXTAUTH_SECRET="$(openssl rand -hex 48)"
npm run dev -- --hostname 127.0.0.1 --port 3000
```

Open `http://127.0.0.1:3000/login`. Use the selected password with host username
`admin` and workspace `Platform administration`. Use the selected member password
with username `aqgreen.demo.member` and workspace `Area workspace`.

To reset after a completed run, stop both applications, remove only the named demo
container, and repeat Terminal 1:

```bash
docker rm -f aqua-aqgreen-v2-demo-pg
```

## Tomorrow manual test script

1. Start the disposable stack above and wait for the backend log confirming the
   root week is held and its commission is uncompleted.
2. Open a private browser window at `/login`, choose `Platform administration`, and
   sign in as `admin`.
3. Open `/admin/weekly-sales-reviews`; locate `AQGreen V2 Demo Member` for the latest
   closed Friday-Thursday Johannesburg week.
4. Open the review. Verify member, Area, week, `Held for evidence`, and no finalized
   quantities.
5. Enter evidence reference `demo-manual:5-5-5`, Spray `5`, 1L `5`, and 5L `5`.
6. Click `Confirm sales`; verify `Confirmed - Met` and that fields/actions become
   read-only. The admin must not choose Met or an amount.
7. Open `/admin/weekly-earnings`, choose `Default (Default)`, and click the existing
   `Prepare weekly earnings` button once.
8. Verify the success summary says one member earned R400. Search for
   `aqgreen.demo.member@example.test`; verify qualified Level 2, commissioned Level
   2, R400, and `Earned - awaiting release`.
9. In a distinct private browser, choose `Area workspace` and sign in as
   `aqgreen.demo.member`.
10. Open `/member/programmes`; verify qualified Level 2, commissioned Level 2,
    Level-1 R150, Level-2 R250, total R400, the same canonical week, and
    `Earned - awaiting release`. Verify the UI does not say Paid.
11. Attempt `/admin/weekly-sales-reviews` as the member; verify access is denied or
    redirected.
12. Do not click release/payment actions unless tomorrow's separate test scope
    explicitly includes those financial operations.

For rejection rehearsal, recreate the disposable database, repeat through step 4,
enter an evidence reference and rejection reason, click `Reject evidence`, and
verify `Rejected`, threshold `Not applicable`, the reason, and read-only controls.
For 5/5/4, recreate again and confirm 5/5/4; prepare earnings and verify qualified
Level 2, commissioned Level 0, R0, and `Not earned`.

## Bounded self-review

- Member earnings remain self-only; no other-member selector or raw audit graph was
  exposed.
- Tenant joins and host `AllTenants` scope are preserved; tenant admin, Area roles,
  and member remain denied B5.3.
- `Met`/`NotMet`, commissioned depth, and R400 remain system-computed and immutable.
- Structural level, commissioned level, amount earned, and payout state remain
  distinct. Earned is not rendered as paid.
- Canonical week is server-resolved and shown in Johannesburg time.
- `NotEarned` and `Rejected` remain distinct; rejected threshold is null.
- Demo mode cannot activate in Production, cannot target a non-loopback PostgreSQL
  host (including a mixed host list), and cannot target a differently named DB.
- Production LegacyV1/default-disabled behavior, D10, and B6 are untouched.

## Delivery state

- Implementation, validation, broad review, the required safety correction, and
  narrow independent re-review are complete.
- The broad review returned `REVISE BEFORE COMMIT` for the name-only demo database
  guard. The loopback/per-host correction was implemented and independently
  accepted with `PASS — NO MATERIAL FINDINGS`; the review loop is closed.
- **LOCAL/DEMO ONLY.** This completed record covers the isolated disposable
  `AQGreenV2Demo` environment and tomorrow's local operational demonstration.
- **PRODUCTION V2 NOT ENABLED.** Production selection remains LegacyV1/default
  disabled, and the production B5.3 mutation gate remains disabled.
- **D10 DISABLED.** D10 remains unresolved and no `V2EffectiveAt` was introduced.
- **B6 NOT STARTED.** Verified Commerce remains future work.
- No production customer data was used. Completion of this execution plan does not
  authorize production enablement, cutover, migration, or data mutation.
- The accepted plan is complete and moved to `docs/exec-plans/completed/` for the
  authorized commit, push, PR, and required-CI delivery sequence.

## Remaining non-tomorrow scope

- Verified Commerce and long-term evidence capture/retention/correction authority.
- A managed staging deployment, if desired beyond this local disposable host.
- A richer reusable control/rejection fixture set.
- Production gate and D10 cutover decision, including any future `V2EffectiveAt`.
- B6.
