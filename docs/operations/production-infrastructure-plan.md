# Production infrastructure integration plan

Assessment date: 2026-10-16. Starting qualified source:
`9581655e4ba0ab22c8578a7536439eda1bbf6fbc`, PR106. **Production readiness: BLOCKED.**
This plan proposes persistent infrastructure; it does not authorize changes. No live
settings, credentials, database contents or deployments were changed. No production
release, financial processing, Bird delivery or worker activation was attempted.

The intended sequence remains merge → main CI PASS → Render exact commit → API
health/revision verification → Vercel same commit → verified production receipt.
There is no recurring activation switch, approval, release JSON, token or dispatch.
An exceptional recovery decision and initial infrastructure custody are distinct
from routine release authorization. Current business/runtime policy is not changed
by this assessment. See the [release runbook](safe-automatic-deployment.md),
[safety contract](cicd-safety-contract.md) and
[independent-verifier limitation](mandatory-security-verification.md).

## Evidence and authoritative targets

Authenticated GETs at 18:05–18:13 UTC are retained as whitelisted
[evidence](../verification/production-infrastructure-20261016/README.md). Local
Render/Vercel CLI sessions and the authenticated GitHub CLI supplied inspection
access. Their existence does not install GitHub Actions credentials or prove a
deploy-only permission. Tokens, secret values, raw environment payloads, export URLs,
database connection strings and customer data were not retained. No database session
was opened. Deployment histories sampled the first 100 entries, not complete history.

| Surface | Authoritative readback |
| --- | --- |
| GitHub | Repository ID1276688422; main `23f176db6eb13af5ee01ff69f1a9f7bbe6b880ff`; active ruleset19085283 protects deletion/non-fast-forward only; classic protection404 |
| Render | `srv-d9f0k8e1a83c73d8n1b0`; exact repository/main; Dockerfile `./docker/api/Dockerfile`, context `.`, root empty; health `/api/health`; native auto-deploy off; not suspended; no start/predeploy override |
| Render current deployment | `dep-db8av3dg1s2s739m6sgg`, live SHA `1251fb82979e18be53d1d0e7f5c6fab2d0c50a58`; API200 reports this SHA and Healthy DB/Redis/key-store; recovery header absent |
| Render database | DATABASE_URL host associates with `dpg-d9f0jne1a83c73d8m780-a`; provider metadata: available, PostgreSQL18, Frankfurt, basic_1gb, HA=false. A second listed instance is not assumed to serve the app |
| Vercel | Project `prj_6qL1e43KhsWrRshY5GxdmGl3HGMz`, team `team_Hc3BWfhTLfwLzwbgbtaasBUY`; root `AqualLifeStyle/9.4.2/aqua-frontend`; Node24.x; no custom build/install/output/ignore override; Git link=null |
| Vercel current production | `dpl_EwGB3apX3P5JnU5KfG3rLzn21Kgf`, READY, alias assigned; gitSource=null. Custom source metadata keys exist but do not authenticate the deployed source |
| Production frontend | `https://www.aqualifestyleclub.co.za`, verified project domain, HTTP200; no served revision header. HTTP200 does not establish commit identity |

## 1. Main governance and independent custody

**Current:** only deletion/non-fast-forward rules; no required PR/check rules. Actions
enabled with allowed_actions=all, SHA pinning not required. Default token is read-only
and cannot approve PRs. All repository/environment/organization-provided deployment
secret inventories are empty. The owner's two administrator accounts represent one
human and do not constitute independent review. No eligible independent custodian or
protected external verifier has been established.

**Required:** retain current protections; require latest-head premerge review,
resolved threads, stale-review dismissal and all17 exact CI job contexts from GitHub
Actions app15368 with strict up-to-date enforcement. The proposed
[ruleset payload](../verification/production-infrastructure-20261016/main-ruleset.proposed.json)
is generated from the current controller inventory. Independently control verifier
code, mandatory-test identities, source pins and credential issuance outside ordinary
application PR authority. Qualify an organization-managed required workflow or
separately administered verifier/broker pinned to an immutable revision. The same
candidate must not redefine both tests and enforcement. Source-app pinning alone
does not prevent another candidate workflow from producing the same job name.

**Evidence:** github-readonly.json and controller validateInfrastructure/
validateCandidateReview. The earlier executable custody probe demonstrates that
candidate-owned policy can be replaced; it is negative evidence, not installed custody.

**Security consequences:** these settings prevent ordinary unchecked merges only
within their actual no-bypass custody. Distinct account IDs do not prove distinct
humans. Agent2 review is technical evidence, not a GitHub human approval. Keep the
independent acceptance claim BLOCKED until an external boundary is exercised.

**Automation/permission:** persistent ruleset creation/update can use an owner-authorized
GitHub repository-administration session. External custody additionally requires an
organization/security administrator with authority over the verifier repository,
required workflow/check source, broker and bypass policy. Current repository admin
inspection does not establish organization administration: organization-ruleset GET
returned404 with an explicit admin:org scope requirement. No changes were applied.

**Verify:** read effective rules, actual check sources and all bypass actors with the
custodian; test an isolated candidate removing/renaming/skipping a mandatory case,
editing inventory, deleting the verification job and altering the credential gate.
Every case must fail independently and obtain no production capability. Bind the
positive result to exact source SHA, CI run/attempt and immutable verifier policy.

**Recover:** keep deployment capability withheld, retain evidence and correct the
configuration through its custodian. Do not restore delivery by bypassing checks or
adding a second owner account as an independent reviewer.

## 2. Unattended environment and persistent credentials

**Current:** production-release exists, can_admins_bypass=true, no protection rules,
no branch policy. Deployment-branch-policy GET404 reflects absent configuration.
RENDER_API_KEY and VERCEL_TOKEN are absent from all visible Actions secret inventories.

**Required:** main-only custom branch policy, branch type=branch/name=main, admin
bypass disabled, no required reviewers, wait timer or manual/custom approval gate.
The [environment payload](../verification/production-infrastructure-20261016/environment.proposed.json)
sets custom branch selection and no reviews/wait; branch-policy creation is a
separate endpoint. Configure admin bypass through GitHub's documented environment
UI; do not invent a can_admins_bypass REST input. Preserve read-only github.token.
Persistent provider capability must be limited to exact targets and required methods.

**Evidence:** authenticated environment/secret inventories and
[GitHub environment documentation](https://docs.github.com/en/actions/how-tos/deploy/configure-and-manage-deployments/manage-environments).

**Security consequences:** a main-only environment alone does not exclude
pull_request_target, a changed main workflow or a workflow deliberately requesting
that environment. The current candidate-owned production job receives secrets before
its own JavaScript checks execute. Do not provision broad production tokens there
and claim untrusted-PR protection. Native deploy-only IAM or an externally controlled
broker must withhold provider authority unless protected verification succeeds; deny
PR, pull_request_target, workflow_dispatch, fork, stale source and wrong attempts.
The broker must constrain deploy requests, not merely hand a broad token to arbitrary
candidate code. OIDC policy needs verified event/ref/repository/workflow identity,
immutable trusted verifier identity and independently fetched CI evidence; environment
subject alone is insufficient. See [GitHub OIDC claims](https://docs.github.com/en/actions/reference/security/oidc).

**Automation/permission:** environment/branch setup is a one-time owner-authorized
administration action; admin-bypass UI needs an authenticated repository-admin
session. Secret provisioning needs a separate credential custodian. Read access to
the existing provider sessions is available; deploy-only scopes and broker are not
established. Broker integration will require a reviewed repository workflow/client
change if native IAM cannot supply the required boundary; it is not already installed.

**Verify:** fresh settings readback; no post-merge approval prompt; independent
negative workflow probes obtain no credential/provider mutation. Verify only the
qualified exact-target request can pass. Qualify persistent deploy permission on an
owner-authorized isolated target before a production attempt.

**Recover:** custodian revokes release capability/pauses workflow and investigates;
retain audits. Never fall back to repository-wide secrets or per-release tokens.

## 3. Render runtime profile — unresolved operational choice

**Current:** Production, recovery=false, Bird=true, Yoco=test with Bird/Yoco API and
webhook keys present. FinancialOperations, WeeklyCommissions, EntryMonthlyObligations,
BackgroundJobs, AQGreenV2Demo, AQGreenV2Pilot, AQGreenV2 commissioning and SEED_DEMO_DATA
are all false. No environment groups or secret files were returned. Database/Redis/
key-store health is good; this does not prove independent runtime privilege isolation.

**Required:** the existing controller implements recovery-only: recovery=true,
Bird=false, all listed flags false, and Yoco mode/secret/webhook and Bird key absent.
Its API verification requires X-Aqua-Recovery-Suspended=true. This is a genuine
profile contract, not a routine deployment authorization. Changing Production to
recovery would suspend ordinary business routes and messaging; it needs an explicit
owner operational decision. **PROPOSED alternatives:** authorize that suspension,
or separately design/test a normal-operation deployment profile with independently
enforced financial/messaging/worker restrictions. Neither option is selected here.

**Evidence:** DeploymentConfigurationValidator, AqualLifeStyleWebHostModule,
ApplicationRecoveryMiddleware, TransactionalEmailOutboxWorker and the live flags.
Normal startup requires Yoco/Bird configuration. BackgroundJobs=false disables ABP
job execution but normal startup separately registers periodic workers. Bird outbox
execution checks Bird:Enabled; it does not check that ABP flag. No live deliveries
were observed or inferred. The Bird delivery cutoff is a messaging boundary, not
a CI/CD release authorization. The prior production-readiness assessment already
records this normal/recovery incompatibility.

**Security consequences:** removing recovery checks solely to match live flags would
erase unproven restrictions. Removing provider keys or enabling recovery changes
product availability. Do not enable commissions/payments/Bird/workers to pass CI/CD.

**Automation/permission:** once the owner chooses a qualified profile, an authorized
Render configuration custodian can apply persistent settings. This may restart the
service and is outside this task. An alternative profile requires implementation,
regression tests and independent review before settings changes; no hidden toggle
can safely substitute for it.

**Verify:** re-read actual service/env metadata; verify revision, dependency health
and effective profile; isolated direct API/worker tests must prove Tenant/Area and
financial/delivery boundaries. Runtime identity checks must support claimed holds.

**Recover:** authorized operator restores last qualified configuration/revision using
preserved private configuration custody; never reactivate financial/delivery capability
as an automatic rollback side effect. Escalate ambiguous state without replaying work.

## 4. Render integration and runtime database authority

**Current:** repository/main/Docker settings and health route match; native deploy is
off. No migration/start hook is configured. Docker starts Web.Host directly; production
startup preflights compatibility rather than migrating/repairing/seeding. A migrator
binary in the image is not privilege separation. Database role grants/ownership,
network boundaries and provider-token mutation scope are UNKNOWN. Health checks
prove connectivity, not restricted role authority.

**Required:** retain one orchestrator, exact commitId deployment and no auto migration.
Release identity must not alter runtime secrets/settings, resume services or administer
DBs. Runtime must not own schema or inherit migration/restore, superuser, CREATEROLE,
BYPASSRLS or unrestricted financial/messaging authority. Preserve required application
DML/key-store behavior with an explicitly qualified grants/procedure design.

**Evidence:** live service readback, Dockerfile, startup validators and
[Render exact-commit API](https://api-docs.render.com/reference/create-deploy).

**Security consequences:** flags do not stop malicious code using a powerful DB role
or provider credential. A deployment token permitting environment reads/writes can
expose runtime secrets. The existing inspection session was not tested by mutating.

**Automation/permission:** obtain catalog-only evidence from an authorized DB/security
custodian in a read-only transaction: current role flags, memberships, schema/table/
sequence ownership, effective DDL/DML and function EXECUTE grants, RLS/BYPASSRLS and
role escalation paths. No membership/payment rows or credentials are needed. Separately
authorize any grants/role/schema/network changes; they are not ordinary release work.

**Verify:** positive application/auth/key-store tests plus forbidden DDL, role change,
ledger/delivery bypass and privileged-credential acquisition tests against a qualified
isolated clone under the actual role design. Read live metadata back without data
access. Deploy permission tests need an isolated service, not this production target.

**Recover:** authorized custodian restores narrowly versioned grants/configuration;
contain release capability and preserve incident evidence. No automatic DB rollback.

## 5. Vercel exact-source transport and single orchestrator

**Current:** expected project/team/root/Node24 and verified production domains; link=null.
No custom build/install/output/ignore override. Current source Git SHA is unavailable;
existing custom metadata cannot authenticate source. With no Git link, there is no
observed project-native Git push trigger to duplicate Actions today.

**Required:** one automatic deployment authority after Render verification. The
implemented path requires the exact GitHub repo link, production branch main, native
Git deployment disabled and explicit gitSource ref/sha=qualified SHA. Preserve the
existing project/domain; do not create a parallel production project. A reconnect
must not briefly enable a competing production push deployment.

**Evidence and alternatives:** [Vercel create-deployment API](https://vercel.com/docs/rest-api/deployments/create-a-new-deployment)
supports Git source or non-Git file uploads. Its official OpenAPI GitHub source schema
requires type/ref/repository identity and allows sha; this does not prove this unlinked
project's GitHub application has repository access. **INFERENCE:** an explicit-source
API request may work without a persistent project link if GitHub access is installed;
the current controller's link requirement is conservative, not proven necessary for
every Vercel transport. Establish that capability on an authorized isolated project
before removing the assertion. The API also documents a Git-independent file route:
upload exact immutable Git archive bytes with a verified manifest/digest, build at the
correct root and attest the deployment output/source/served revision. That route needs
new controller/tests/provenance handling; caller-supplied gitMetadata is insufficient.
Do not use a manual fresh Git token per release; the documented gitAccessToken
option is limited to Vercel platform accounts and is not an assumed workaround.

**Security consequences:** accepting arbitrary metadata or silently switching transport
loses provenance. Reconnecting Git without disabling native deployment violates the
Render-first ordering. No production POST can be used as an inspection probe: create
starts building immediately.

**Automation/permission:** an authenticated Vercel team/project administrator and GitHub
app installation administrator must authorize persistent Git access/connection changes.
Alternatively authorize an isolated explicit-source capability test and then review
the corresponding link-free implementation. No connection/settings change was made.

**Verify:** isolated exact-SHA deployment must return authoritative repository/source
identity and READY at the intended root; native pushes must not independently publish.
Then read back settings. Production capability is verified only during separately
authorized rollout; no such evidence exists now.

**Recover:** stop release capability, reconcile exact accepted deployment IDs and
preserve existing domains. Do not reconnect a second trigger or redeploy blindly.

## 6. Public build inputs and trustworthy initial baseline

**Current:** NEXT_PUBLIC_ABP_API_URL and NEXT_PUBLIC_DEFAULT_TENANT_NAME are production+
preview sensitive variables, no branch override; decrypt=true returns value="" and
decrypted=false. Actual values are UNKNOWN, not known wrong. The current Vercel READY
deployment has no gitSource; Render has an exact source but the frontend baseline
cannot be qualified. Existing custom source labels are not independent build evidence.

**Required:** production public API=`https://aqualifestyle-api.onrender.com` and tenant=
`Default`, readable/verifiable by the protected deployment identity without revealing
secrets. These NEXT_PUBLIC values are public build configuration. A real initial
provider baseline must establish repository/source identity for both providers;
subsequent verified receipts are authoritative. Preserve automatic discovery with no
handwritten bootstrap/release JSON or invented historical SHA.

**Evidence:** full current target GET, safe public-input readback and controller
validateVercelVariables/baseline checks.

**Security consequences:** wrong build API/Tenant can route users incorrectly. A
fictional baseline could authorize an incompatible source difference. Reconnecting
Git alone does not repair the already deployed target's missing provenance.

**Automation/permission:** owner-authorized project admin can persist the two non-secret
values in a readable type. Recover authentic historical build evidence if possible;
otherwise a separately authorized initial attested deployment/transport implementation
is needed. Initial configuration is not permission for this task to deploy, nor a
per-release prerequisite. Baseline is currently BLOCKED; no proof was fabricated.

**Verify:** protected identity reads exactly one unscoped production value per key;
deployment source/output proof matches the actual baseline. Automatic baseline
selection succeeds without manual records, then later releases use the verified audit.

**Recover:** restore prior qualified public configuration through its custodian;
retain the original baseline/request evidence. Never reset baseline after partial
rollout to conceal an advanced backend revision.

## 7. Durable receipts, monitoring and recovery readiness

**Current:** source/candidate and JSONL release audits use immutable GitHub artifact
IDs and SHA/run/attempt binding with 90-day retention. This is not immutable external
custody: artifacts can expire or be deleted. Global concurrency serializes this
workflow only. Retry/intent/provider-ID checks reject ambiguous operations. The
production frontend returns200 but does not expose a served build revision. No
authenticated alert configuration or restore drill was inspected. Both listed DB
export endpoints returned empty arrays; DB metadata returned no backup retention/PITR
fields. A separate authenticated GET of `/postgres/{id}/recovery` reports AVAILABLE
for the application database, startsAt=2026-10-13T02:44:00Z (also AVAILABLE for the
other instance). This establishes provider-reported PITR availability at observation,
not a tested restore, RPO/RTO, decryption-key recovery or financial-history integrity.

**Required:** externally controlled append-only receipt retention before GitHub expiry,
continuity monitoring and protected producer identity; alert on failed/stalled/partial
release and revision/profile drift. Qualify backup retention, RPO/RTO, encryption/key
custody and actual isolated restore of authoritative schema/history. Verify compatible
rollback artifacts for both providers; a partial rollout must preserve API/frontend
compatibility. Add authenticated served-revision evidence if running-output attestation
is required; current checks establish provider source and API build identity only.

**Evidence:** release receipt/retry code, public GETs, provider metadata, empty export
lists, [Render export API](https://api-docs.render.com/reference/list-postgres-export)
and [PITR availability API](https://api-docs.render.com/reference/retrieve-postgres-recovery-info).
Exports can contain secret download URLs; none were accessed or retained.

**Security consequences:** expired/missing receipts deliberately block deployment;
availability cannot justify discarding history. Healthy/HA flags do not demonstrate
recoverability. A READY frontend and matching provider Git source do not cryptographically
attest the bytes actually served to users. No automatic financial replay or DB restore.

**Automation/permission:** receipt storage/alerts can be persistent automated services
under a separate custodian; provider backup/monitoring read access or authenticated
dashboards are needed to establish actual settings. Storage, grants, backup policies,
drills and restore/rollback actions require explicit owner authorization. No recurring
approval is required for routine code-only releases once these controls are qualified.

**Verify:** complete source/run/attempt/artifact digest chain, protected append-only
write/read/retention negatives, alert delivery on synthetic faults, actual isolated
restore and tested revision-compatible rollback. Never use production data or backup
downloads in ordinary CI. A live end-to-end result requires an observed authorized
Render and Vercel rollout, which has not happened in this task.

**Recover:** contain release capability, keep partial IDs/intents and reconcile provider
state before any retry. An authorized operator selects a compatible known-good code
pair; schema/data restore is separately authorized. Do not blind-retry POSTs, fabricate
history, automatically run EF Down or activate held operations.

## Application and expected behavior

Recommended dependency order: establish external custody/credential boundary; qualify
main/environment governance; choose and qualify runtime profile and privileges;
qualify Vercel transport/public inputs/real baseline; qualify durable receipts/recovery;
provision persistent restricted capability; finally permit an authorized merge/rollout.
This is initial infrastructure work, not an activation switch or per-release approval.

No verified implementation defect was identified that can resolve these live
conditions through an authorized repository-only patch. This assessment changes
documentation/proposed configuration only; existing guards are preserved. Brokering,
an alternative runtime/transport or stronger runtime attestation would be separate
implemented changes with executable negatives and fresh review, not claims made here.

Today a qualified main-push CI automatically starts release qualification, then stops
at missing main PR/CI governance before provider mutation. Fixing that alone exposes
the remaining environment, credential, runtime and baseline blockers in turn. After
the qualified persistent setup and any explicitly selected implementation are complete,
the same automatic chain proceeds without additional human approval. Independent
enforcement, runtime isolation and actual production deployment remain separate
unproven claims. This branch is not claimed MERGE READY or PRODUCTION VERIFIED.
