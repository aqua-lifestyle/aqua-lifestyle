# AQGREEN V2 — CONTROLLED DEPLOYMENT READINESS AND MINIMUM RELEASE PATH

12 October 2026. **A. ISOLATED HOSTED MVP READY FOR SEPARATE DEPLOYMENT AUTHORIZATION — tunnel-accessible demonstration.**

**PROPOSED:** host the accepted candidate on a separate disposable Linux environment,
with an internal application network and owner-approved SSH access. Each operator
opens `http://127.0.0.1:3000` through a tunnel. This preserves the qualified browser
origin and the existing loopback-only synthetic-money guards. No application
correction is needed for this route. It is not an ordinary public HTTPS website.

Nothing was deployed, migrated, published or connected to production during this
assignment. The accepted source is unchanged. A normal public HTTPS demonstration
has the specific origin prerequisite described below; integration with real
operations has separate production prerequisites.

## 1. Actual starting point and exact subject

**FACT — owner authority:** the current direct instruction accepts the Node 24
candidate with source-manifest SHA-256:
`5e133be584398b2db14a5c542e27860bd6f02adae0c66c1488ef30e7ee2573e0`.
This subsequent acceptance is separate from the original MVP acceptance and the
independent review's historical “ready for owner acceptance” wording.

| Item | Established starting point |
| --- | --- |
| Release source | Preserved bundle `/home/wtc/Downloads/newAqua/aqgreen-v2-node24-candidate-20261012-5e133be58439` |
| Source archive | `derivative-source.tar.gz`, SHA-256 `0af6babebb839c3e65cf4df4797d59822ba84700829b2f0e96e0cdbd87faf11d` |
| Parent | HEAD `e9bd8e96d55d03466492ffd40dd657802ae36d4b` plus exact preserved modifications and five font/layout derivative paths; bare HEAD is not the release |
| New verification | Source manifest hash, archive hash and all **2,105 source files** match; read-only inspection and Compose configuration validation passed |
| Carried-forward release evidence | Node24.21.0/npm11.19.0, Next16.3.6, React19.2.4, .NET runtime8.0.31/SDK8.0.131, PostgreSQL16.15; locked offline build and production standalone browser smoke;23 frontend and13 isolation passes; prior17/17 operational cases and independent review |
| Intended existing hosting | Repository `render.yaml` declares Render API/PostgreSQL/Redis; deployment guide describes Vercel frontend. These are configuration/history evidence |
| Actually deployed revision/schema/flags | **UNKNOWN** — no current deployment attestation inspected; neither HEAD nor old CI identifies the live build |
| Live API running/suspended state | **UNKNOWN** — production contact was prohibited. The client's operating business does not establish API service state. Older P0001 investigation/hosting reports are historical |

Before any later production work, obtain separately authorized, dated, redacted
service metadata: running/suspended status, deployed build/image IDs, schema head,
worker/integration flags and recovery status. Owner-supplied metadata is sufficient
to begin that comparison; no credentials or member records belong in this report.
Current production uncertainty does not prevent an entirely separate hosted demo.

The [qualification report](aqgreen-v2-accepted-preservation-node24-qualification.md)
and [independent review](aqgreen-v2-node24-derivative-independent-acceptance-verdict.md)
retain the executed evidence and limits. No new application suite, container build,
SSH-host test or hosted-browser test was performed for this planning-only assignment.
Read-only checks are retained in
[readiness evidence](../verification/aqgreen-v2-deployment-readiness-20261012/).

## 2. Minimum route comparison

| Path | Minimum changes and prerequisites | Recommendation |
| --- | --- | --- |
| **A — isolated hosted MVP through SSH tunnel** | Separate host/resource, imported runtime bases, accepted application outputs, fresh PostgreSQL volume and synthetic secrets; internal network with only loopback frontend port; existing demo environment; scoped synthetic actors | **Fastest safe route.** No source changes, production dependency, public DNS or provider credentials |
| **A — ordinary public HTTPS demo link** | A separate narrow application correction must support an explicitly approved isolated origin in demo validation, checkout redirects and synthetic verification links, while retaining Production rejection, dedicated local database, no-network gateway and payout denial; qualify HTTPS sessions/origins against that derivative | **Specific correction required for this variant.** Setting a public client URL today fails the guard. Rewriting links or relabeling Production is not an approved workaround |
| **B — integrate real Aqua operations** | Establish actual live baseline; qualify startup/schema effects against authorized sanitized existing history; B6 reconciliation and applicable B7/D10 cutover; non-demo V2 selector/authority wiring; properly authorized existing Yoco/Bird integrations and financial controls | Separate controlled release. The accepted demo cannot activate real V2 merely by setting Production or changing its database |

For A, use a dedicated host with no production mounts, service identities, database
tunnels, copied environment values or payment-provider credentials. A separate
resource name alone is insufficient. If the owner requires a public link rather
than tunnel access, authorize the narrow origin work first; this plan does not
modify that contract or silently transfer acceptance to another source candidate.

## 3. Hosting configuration and critical controls

The concise [operator transition](../operations/aqgreen-v2-isolated-hosting/README.md)
and prepared deployment files are under
[`docs/operations/aqgreen-v2-isolated-hosting`](../operations/aqgreen-v2-isolated-hosting/).
They are **a proposed deployment recipe**, not a deployed or runtime-qualified
hosting configuration. `compose.plan.yaml` validated with Docker Compose
v2.32.4-desktop.1 using placeholder-only environment files; no services started.
It intentionally does not extend the existing production Compose/Render services.

| Control | Configuration / inspected consequence |
| --- | --- |
| Runtime | Frontend `NODE_ENV=production`; backend **`ASPNETCORE_ENVIRONMENT=AQGreenV2Demo`**, `AQGreenV2Demo__Enabled=true`. Production plus this opt-in is already tested to refuse startup |
| Origin and backend connection | Client `http://127.0.0.1:3000/`; API `http://127.0.0.1:8080/`; CORS exact loopback origin; frontend build `NEXT_PUBLIC_ABP_API_URL=http://127.0.0.1:8080`, `NEXT_PUBLIC_DEFAULT_TENANT_NAME=Default`. Database `Host=127.0.0.1`, exactly `aqua_aqgreen_v2_demo` |
| Network | PostgreSQL owns one internal Docker network namespace; API, migrator and frontend share it, satisfying the existing local-database guard. Only host `127.0.0.1:3000` is published; no public API, database or callback port. SSH is the separately authenticated transport; no application egress route |
| Runtime identity | Fixed qualified local base image IDs in `release.env.example`; `pull_policy: never`; accepted application outputs mounted at explicit paths. The bases alone are not application release artifacts. Record output-file hashes and runtime IDs after packaging |
| Versions / environment differences | Hosting defaults use floating Node24/.NET8/PostgreSQL16 image tags; the plan uses the recorded qualified bases. Render defaults include Production, database `aqualifestyle`, Redis, live Yoco, Bird enabled and automatic migration; replace all of those for A. Redis is optional outside Production and blank for this single-instance demo |
| Schema | Existing API startup does not apply schema migrations. Its EF module does seed. Run the dedicated migrator once, explicitly against a newly created empty demo database; it migrates host and distinct Tenant connection databases. Verify no imported Tenant connection strings; do not use a production pre-deploy hook |
| Startup writes | Host/default Tenant roles and permissions, JHB Area/admin assignments, customer-user linking/account provisioning and role assignment can write on startup. Disabling `AQUA_SEED_DEMO_DATA` does **not** disable these. They are acceptable only against disposable records for A; their effect on existing records must be qualified before B |
| Synthetic seed | `AQGreenV2Demo__Fixture__Enabled=true` creates the clearly synthetic qualification tree and guest. `AQUA_SEED_DEMO_DATA=true` reproduces the qualified seed profile, including legacy demo accounts; keep access restricted and use newly generated administrator/member passwords. Never treat these records as existing customers or commerce |
| Week boundary / restart | With the fixture enabled, an existing fixture from a different latest closed week refuses startup. For a multi-week demo, disable **only Fixture.Enabled** after initial seed and before the boundary; retain Demo.Enabled and all isolation controls. Alternatively schedule a deliberate fresh disposable reset. Do not backdate new placements or turn the refusal into a successful fallback |
| Workers | `App__WeeklyCommissions__Enabled=false`, recovery/observability flags false; `App__EntryMonthlyObligations__Enabled=false`; `Bird__Enabled=false`. These existing gates stop their financial/outbox work. Yoco stale-checkout monitor still registers and reads the demo database/logs; it does not contact Yoco |
| Messages and verification | Bird key/address settings blank; no email delivery. Existing outbox may retain synthetic verification intent. Authorized Tenant1 operator retrieves links through the existing protected demo helper for `aqgreen.operational.*@example.test`, then verification uses the existing browser flow. Keep one-time links private, absent from logs/reports |
| Payments / callbacks | Yoco mode, secret and webhook secret blank; startup asserts the no-network demo gateway. Missing webhook signing configuration fails closed503. API is not publicly reachable; no webhook is registered. Existing receipt/reference/amount/transaction controls remain; do not import demo confirmations into real payment history |
| Authorization | Assigned Area admin acts within its Tenant/Area; central review uses explicit existing permission and broader-scope grants, as demonstrated by the accepted fixture. Do not infer global access from role. Preserve direct backend denials and audit identities |
| Sales / commission / holds | Existing manual evidence review and5/5/5 rule version; manual duplicate-evidence register and custodian from Agent2's playbook; system derives amounts. Held evidence and financial Held are separate from an unreleased ledger. Demo payout guard remains active even for otherwise permissioned finance administrators |
| Persistence / secrets | Dedicated PostgreSQL named volume, never shared with Aqua production; database-persisted Data Protection keys and fresh JWT/session secrets retained across normal restart. Private files outside Git, mode0600; private encrypted host storage/backup access for the isolated key ring. No real recipient data |
| Stop | All proposed services use `restart: "no"`; no scheduled migrator, auto-deploy integration or automatic worker enablement. Stop frontend/API first, then database. A stopped application must stay stopped until the operator deliberately restarts it |

`render.yaml` currently runs `dotnet migrator/AqualLifeStyle.Migrator.dll -q`
automatically, selects `Yoco__Mode=live`, `Bird__Enabled=true`, and permits automatic
Git deployment. Existing Compose also requires Yoco secrets, chooses Production,
uses the non-demo database and publishes service ports broadly. **Do not apply either
unchanged.** Do not create a version tag: the existing publishing workflow would
publish images. The isolated files neither change nor activate those configurations.

## 4. Ordered executable transition — after separate owner authorization

**Prerequisites:** owner-selected separate Linux/Docker host with authenticated SSH;
no relationship to production resources; qualified runtime bases privately imported;
local build cache/toolchain matching the qualification record; fresh synthetic
secrets; named operator and owner-review accounts; agreed demo duration and reset/
retention choice. No Bird account, real Yoco access, retail SKU or production-service
resume is needed. The candidate is not yet a published application-image release.

1. **Prepare from the frozen artifact.** Verify archive digest above; extract into a
   new build directory; from its source root run `sha256sum -c /path/to/source.sha256`.
   All2,105 must match. Build from that directory, never the mutable working tree or
   a checkout of bare HEAD. Keep font notices with the bundle.
2. **Package using existing qualified processes.** Release-publish Host and Migrator
   from accepted source using the offline .NET cache/SDK. In a clean frontend copy,
   run locked `npm ci --offline`, then `npm run build` under Node24.21.0 with denied
   network and the loopback public values above. Assemble `bundle/api`,
   `bundle/migrator`, `bundle/web` (standalone plus `.next/static` and `public`).
   Preserve compiler/build logs and output SHA-256 manifest. Private transfer/import
   of the runtime bases and bundle needs the deployment authorization; no Git push
   or registry publication is necessary. Use readable API outputs and Node-user
   access to the dedicated web output/cache directory. Validate the actual packaged
   runtime before admitting viewers; source acceptance does not attest new binaries.
3. **Configure only the separate host.** Copy the proposed recipe; create private
   files from examples with fresh values, not reused production values. Fill the
   release file with imported image IDs and dedicated absolute directories. Verify
   each image locally exists. No pulls occur at runtime. Inspect effective config
   privately; use `config --quiet` for recorded evidence so secrets are not printed.
4. **Create/bootstrap the empty demo database once.** On the authorized isolated
   host, from the recipe directory, execute:

   ```bash
   docker compose --env-file release.env -f compose.plan.yaml config --quiet
   docker compose --env-file release.env -f compose.plan.yaml up -d postgres
   docker compose --env-file release.env -f compose.plan.yaml run --rm migrate
   ```

   Verify successful migrator exit, exact demo database/schema and only approved
   synthetic Tenant configuration. Stop on migration failure; do not continue,
   point at another database or invent a compatibility fallback.
5. **Start the candidate privately.** Execute:

   ```bash
   docker compose --env-file release.env -f compose.plan.yaml up -d api web
   ```

   Confirm health and demo startup, then create separately authorized synthetic
   central/member/Area/owner-review actors with the established fixture procedure.
   Confirm Area assignments and explicit central review scope. Never pre-seed
   successful new-member payment/approval outcomes for the walkthrough.
6. **Qualify the hosted package before opening demonstration access.** Perform the
   post-deployment checks below; record source/output/image IDs, time, environment
   and actual results. Verify there is no default external route, only loopback
   published access, no provider settings, no public callback and no worker arming.
   No real-provider probe is needed or permitted.
7. **Provide owner-approved tunnel access.** On each authorized operator's machine,
   after checking host identity through the approved channel:

   ```bash
   ssh -N -o ExitOnForwardFailure=yes -L 127.0.0.1:3000:127.0.0.1:3000 DEMO_SSH_ALIAS
   ```

   Use the verified host alias/key and an available local port3000. Browse
   `http://127.0.0.1:3000` in the browser used for the acceptance smoke. This keeps
   invitation, session, synthetic checkout and verification URLs aligned. Transfer
   passwords privately. No outreach, public announcement or production member
   access is implied. Confirm the week-boundary procedure before leaving it running.

The template is a single-instance version of the existing qualified topology;
no new qualification architecture or backend feature implementation is proposed.

## 5. Pre-deployment and post-deployment acceptance

**Before start:** accepted source/output hashes; imported image identities and
version records; private resource/storage ownership; database absence of imported
records or Tenant connection strings; exact environment/gateway settings; disabled
workers/Bird; blank provider secrets; correct bundle paths and file permissions;
loopback-only ingress/internal network; manual bootstrap only; named reviewers and
stop operator. Source/guard tests already establish their properties; repeat only
where packaging or environment changes require it.

**After start, before demonstration access:** repeat the accepted production smoke
on the hosted package, retaining actor/action/readback outcomes. Use the existing
[17-case checklist](aqgreen-v2-independent-operational-mvp-acceptance-checklist.md)
for case references. Its passed allocator/concurrency, detailed business cases and
unchanged guard tests remain carried forward; a full17-case rerun is not an added
release gate. Check the hosting-sensitive paths:

- browser signup and synthetic email verification; invitation and immutable
  recruiter attribution; R1,200 and two distinct R600, incorrect amount/replay;
- pending versus authorized activation; one persisted approval/placement; correct
  sponsor and progress. Inspect the seeded five-wide tree and resulting new
  spillover; repeat the empty-branch sixth-member case only if allocation inputs
  or the PostgreSQL version differ from the accepted qualification;
- Area/explicit central review success and cross-Area/Tenant/member API denial;
  confirmed, shortfall, rejected and Held evidence, reviewer/time/history,
  manual register versus application-enforced protections;
- versioned5/5/5 calculation with exact rate/component inputs; Held review blocks
  calculation; current placements excluded from older closed periods; duplicate
  calculation produces no new financial effect. Carry the accepted positive
  financial-Held case unless its underlying configuration/rules changed;
- direct Release/RecordPayment denial even with finance grants; no released/paid
  records; demo provider records only, no Bird delivery or real Yoco requests;
- actual production frontend routes/assets, login refresh/logout and cookie
  protections through the tunnel; healthy API/database/key ring; one controlled
  stop/restart preserves application records and does not rerun schema migration.

If a changed hold input requires retesting, reuse the existing positive
financial-Held fixture, clearly identified as synthetic. A fail-closed result counts as its expected negative case only; do not
call an entire journey passed because a banner is present. Post-host checks are
**NOT RUN** in this planning assignment. Existing local passes are carried forward,
not hosted proof or observations of actual administrators.

## 6. Stop, restart and rollback

On error, unexpected worker/provider configuration, missing isolation, wrong
Tenant scope or financial mutation: disconnect viewer tunnels, stop `web api`,
then stop `postgres` using the same project/recipe. Retain sanitized logs and
source/output IDs; privately preserve the synthetic volume and keys if investigation
needs them. No automatic restart or migration recovery is configured.

```bash
docker compose --env-file release.env -f compose.plan.yaml stop web api
docker compose --env-file release.env -f compose.plan.yaml stop postgres
```

A normal restart uses the same bundle, synthetic volume and secrets. It does not
run `migrate`. Handle the fixture's week boundary as stated above. If preserving
multi-week history, disable only fixture seeding before restart and manually review
new periods; never rewrite dates to retain an old displayed commission example.

For an initial disposable failure, rebuild a **new empty isolated volume** from the
accepted artifact, preserving failed evidence and the old volume until authorized
reset. `down` without `-v` removes containers/network while retaining data. Do not
use `down -v` unless the owner authorizes discarding that named synthetic dataset.
For a later update, stop writers, retain the previous output bundle and take a
private isolated backup including key ring; rollback by verified restore into a
separate empty isolated database plus matching prior outputs/configuration.
Never use EF `Down` to erase consumed financial/topology history. Aqua production
is never a rollback target for A. Existing production recovery requires its own
accepted backup/restore evidence before B.

## 7. Exact authorizations and production-only blockers

Separate execution approval must name: **route A/tunnel access**, accepted manifest,
selected host/resource and spending limit, private artifact/image transfer,
creation of only the disposable database, its one-time schema migration and
synthetic seed, service startup/network configuration, synthetic operator accounts
and permissions, approved viewers, stop operator and retention/reset authorization.
Do not infer resource creation or human access from source acceptance. No approval
is being requested to re-confirm accepted business rules or completed local tests.

Before **B / any live V2 member or financial operation**, the responsible owners
must resolve the applicable existing prerequisites:

1. Establish current deployed code/schema/service state and a restorable recovery
   point; examine the historical P0001 migration issue against current evidence.
   Qualify the actual migrations and startup account/role/Area writes on an
   authorized sanitized production-like copy before affecting existing records.
2. **B6 / D03B and D09:** accepted legacy roots/populations/sponsor provenance,
   historical exceptions, deterministic canonicalisation/reconciliation and
   financial-impact comparison. Preserve V1 payments/decisions and authoritative
   timestamps. The parked B6 programme remains parked here. If its privileged
   runner is selected for actual migration, its previously recorded Finding6
   terminal-finalization correction/evidence must be addressed for that operation;
   this does not reopen settled firewall gates or require them for A.
3. **B7 / D10:** explicit Tenant/population and financial effective boundary,
   cycle alignment, live-write fencing, mixed-version compatibility, authorized
   non-demo V2 selectors and dependent readers, incident pause/recovery, retention
   and exception authority. The current default joining/review selectors remain
   disabled and financial structural selection defaults to V1 outside demo.
   Production cannot use the demo replacement bindings as its cutover mechanism.
4. Existing **Yoco** checkout/webhook/amount/currency/purpose/reference/idempotency
   controls plus separately authorized provider acceptance/delivery/finality
   evidence, including both schedules and occurrence-time semantics. Keep joining
   confirmation separate from Area activation; refund/dispute/chargeback exceptions
   need the existing unresolved authority. No synthetic ledger becomes real history.
5. Bird credential/sender and actual delivery/outbox-recovery qualification if
   messaging is enabled; preserve Production certificates, DB key ring, Redis and
   provider configuration validation. Do not disable required Production checks.
   Weekly/monthly worker enablement requires their existing preflight, policy,
   recovery and observability conditions; they need not be enabled for a manual
   administrative MVP. Real releases/payouts remain separately unauthorized.
6. Follow Agent2's existing commercial/legal findings before real commercial
   activation, including unresolved applicable South African consumer-law review.
   No retail platform, SKU prerequisite, cash implementation, loans or Onyx expansion
   is added to this deployment path.

**Verdict: A. ISOLATED HOSTED MVP READY FOR SEPARATE DEPLOYMENT AUTHORIZATION**, for
restricted tunnel access with the listed concrete hosting prerequisites. A public
HTTPS link requires the narrow origin correction; B remains separately blocked by
its real-data/cutover/integration dependencies. No production-readiness claim.

Proverbs21:5 grounds deliberate planning, Proverbs22:3 prevention of foreseeable
harm, Proverbs18:17 examination and1Corinthians4:2 stewardship. Derived engineering
requirements are preserved identity, separate resources, controlled writes and
truthful financial evidence. Technical proof remains the named checks and recorded
local executions; the scriptural principles do not prove hosting behaviour.
