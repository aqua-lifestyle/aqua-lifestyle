# Scripture-governed CI/CD safety contract

## Authority and meaning

The owner's instruction makes Scripture the primary Christian ethical authority
for this safety work. This document is an engineering application of that
foundation, not a new theological confession or confirmation of unresolved Aqua
business policy. It does not authorize a merge, deployment, infrastructure change,
financial operation, messaging, migration, service resumption or commissioning.

Keep five kinds of claim distinct:

| Kind | Meaning and limit |
| --- | --- |
| Scriptural principle | The cited biblical teaching, read in its literary context. |
| Theological interpretation | Our stated application to stewardship of software and people; open to correction. |
| Engineering evidence | A reproducible observation, test or inspection at an identified source/environment. |
| Formal specification or guidance | NIST, OWASP and SLSA have different scopes; comparison is not certification. |
| Implementation decision | A human design choice accountable to the owner and evidence; never a divine command. |

A PASS means a named property was demonstrated under stated conditions. It never
means absolute safety, freedom from every attack, or production acceptance. FAIL
means evidence contradicts a required property. UNKNOWN means evidence is absent;
BLOCKED means that uncertainty or an unmet prerequisite prevents acceptance.
IMPLEMENTED, TESTED, INTEGRATED, MERGE READY, MERGED, DEPLOYED, ENABLED and
PRODUCTION VERIFIED remain separate repository states.

## Scripture to engineering

References use the King James Version for source identification; the summaries
and applications below are paraphrase and interpretation, not additional Scripture.

| Scripture and context | Principle | Theological application | Chosen engineering obligation |
| --- | --- | --- | --- |
| [Proverbs 22:3](https://www.biblegateway.com/passage/?search=Proverbs+22%3A3&version=KJV), wisdom about seeing danger and taking shelter | Prudence anticipates harm. | Stewardship includes preparing for foreseeable failures rather than ignoring them. | Model threats; deny uncertain admission; bound retries; preserve a containment path. |
| [Luke 14:28–30](https://www.biblegateway.com/passage/?search=Luke+14%3A28-30&version=KJV), an illustration within Jesus' teaching on the cost of discipleship | Consider what responsible completion requires. | By analogy, count recovery, operational and human costs before enabling automation. The passage does not specify a release architecture. | Qualify restoration, compatibility, provider limits, ownership and operational capacity before activation. |
| [1 Corinthians 4:2](https://www.biblegateway.com/passage/?search=1+Corinthians+4%3A2&version=KJV), faithfulness of stewards in Paul's ministry discussion | Stewards must be faithful. | Entrusted credentials, records and authority require accountable custody. | Separate identities, restrict privileges, record exact provenance and assign an accountable custodian. |
| [Proverbs 11:1](https://www.biblegateway.com/passage/?search=Proverbs+11%3A1&version=KJV), honest and dishonest measures | Integrity in dealing with others. | Financial evidence and safety reports should not misrepresent value, entitlement or certainty. | Preserve ledger/history facts; no fabricated defaults, concealed skips, payment replay or CI-as-financial-authority. |
| [1 Thessalonians 5:21–22](https://www.biblegateway.com/passage/?search=1+Thessalonians+5%3A21-22&version=KJV), discernment in concluding community exhortations | Examine claims and retain what is good; reject evil. | Test automation and challenge its claims; technical testing is a prudential application, not an exhaustive definition of spiritual discernment. | Positive and negative tests, independent review, protected tests, honest UNKNOWN/BLOCKED results. |
| [Romans 13:10](https://www.biblegateway.com/passage/?search=Romans+13%3A10&version=KJV), love of neighbour and fulfillment of the law | Love does not harm one's neighbour. | Members, administrators, staff and bystanders bear the consequences of unsafe automation. | Minimize data exposure, privilege, financial error, unwanted communications and outage; restore without erasing evidence. |

No specific flag, provider, language, reviewer identity or pipeline is divinely
mandated. Religious identity is not proof of technical competence or trustworthiness.
Disagreement with a design choice is not automatically moral wrongdoing.

## Scope, workflow and protected assets

Required outcome: **qualified merge → completed CI → automatic deployment →
production verification**, without recurring human release approval or a release
token. Normal independent review of changes remains merge governance. Changes to
the control plane require separately accountable security review before merge,
not a new approval for every deployment.

The existing single release orchestrator retains exact source, CI run/attempt,
release run/attempt, target, policy digest and source-byte checks. Render must
become healthy at the exact revision before Vercel admission. A verified final
pair produces an audit receipt. The latest finalization instruction removes the deployment activation switch and
manual bootstrap. Policy version2 selects the first proven provider baseline
automatically and retains immutable predecessor/retry evidence thereafter. Runtime
recovery/financial/worker holds remain unchanged; there is one release path.

| Asset/person | Trust boundaries and adversaries/failures |
| --- | --- |
| Members and their personal information | Browser → API → Tenant/Area authorization → database; malicious member, compromised staff, faulty query or export. |
| Accounts and administrator authority | Identity provider/session → permission/Area grant → transaction; stolen session, revoked grant, Host privilege escalation. |
| Payments, participation and ledgers | Provider → confirmation → Area approval → immutable ledger; replay, false confirmation, unauthorized activation or calculation. |
| Database, history and key material | Runtime principal → schema/roles → backup custodian → restored service; DDL, corruption, incomplete backup, lost decryption key. |
| Message recipients and outbox | Application → queue/worker → Bird; duplicate or unauthorized delivery and disclosure. |
| Source, build outputs and credentials | Contributor/agent → source platform → CI → deployment environment → providers; poisoned pipeline, changed dependency/action, stolen deployment key. |
| API/frontend availability and evidence | GitHub → Render → health → Vercel → immutable receipt custody; ambiguous POST, partial failure, concurrent administrator, deleted evidence. |
| Owners/operators and agent authority | Human instruction → agent tools/runner → repository/provider capabilities; prompt injection, excessive tooling, unauthorized operational action. |

Application code and agent-produced text are untrusted inputs to admission. A
compromised application and a compromised contributor/CI job are separate threats;
controls must address both. A provider or organization administrator remains a
powerful actor outside normal Actions concurrency.

## Independent authority and identity contract

The following separation is REQUIRED for acceptance, not asserted as deployed:

| Identity/authority | Permitted boundary | Must not inherit |
| --- | --- | --- |
| Contributor/implementation agent | Propose branch changes and run isolated tests within task authorization | Main bypass, production configuration, deployment, database or messaging credentials |
| Security/control custodian | Qualify control changes and immutable verifier policy through protected merge governance | Sole unilateral authority to edit controls, approve own change and bypass enforcement |
| CI build identity | Read source and test fixtures; publish qualified artifacts where explicitly granted | Production deploy keys, application secrets, database administration or money/message operations |
| Release identity | Read qualification; request exact-target deployment and inspect status | Runtime secret/configuration writes, resume, database administration, financial or messaging APIs |
| Runtime application identity | Necessary application DML/authentication/audit/key-store operations under the qualified profile | Schema ownership/DDL, role administration, superuser/BYPASSRLS, migration/backup identity, unrestricted ledger or messaging privilege |
| Migrator/restore custodian | Separately authorized schema/restore work against an exact target | Automatic acquisition by the application or release pipeline |
| Financial and messaging identities | Only separately authorized programme/worker or delivery operations | Deployment authority or availability merely because a flag changes |
| Independent observer | Read revision/readiness/holds, receipt continuity and alerts | Mutation or secret disclosure |

Feature flags, recovery guards and absent keys are useful layers, not independent
least privilege. Sharing an application database role with unrestricted ledger DML
does not protect those tables from arbitrary code running as that role. A single
database may need protected procedures/separate writer identities or other approved
boundaries; choosing that architecture is unresolved infrastructure/security work.
No production role or schema is changed here, and fixture permissions are not an
application authorization redesign.

If native provider IAM cannot limit a deployment credential to the required methods
and service/project, qualify a separately protected machine-enforced credential
broker/API restriction before activation. The orchestration remains the existing
one; an application must not obtain privileged tokens through its environment,
metadata service, secret store, role membership or a confused deputy. Broad provider
keys that can change runtime environment or reveal secrets do not meet this property
merely because the workflow intends to call deploy endpoints only. Actual IAM
scope, network restrictions, secret custody and production DB privileges are UNKNOWN.

### Protecting the controls from ordinary changes

The independent verifier must be outside the candidate's unilateral change authority:
an organization-managed required workflow/check or equivalent protected verifier,
loaded from an immutable qualified revision. Main must require its actual check
identity/source and completion; no skipped/neutral/missing result may qualify.
Restrict workflow/settings/environment/secret custody and verify no bypass. Normal
PR approval alone does not establish independent control-plane custody.

`tools/deployment/safety-boundary.mjs` is an **assessment tool**, not this deployed
boundary. It compares immutable ancestor/head Git revisions without executing head
code, hooks, external diff drivers or text conversion. Renames are compared as
delete/add; missing/truncated/invalid evidence fails. Only regular-file presentation
additions/modifications under the explicit frontend allowlist classify PRESENTATION_PATHS_ONLY.
Tests, workflows, tools, backend code, dependencies/configuration, operational
documents, unknown paths, deletions and symlink/type changes require control review.
This classification says nothing about whether presentation changes are safe.

Invoke the copy in a separately protected verifier checkout, against the candidate
repository, with verified immutable revisions:

```bash
node /trusted-verifier/tools/deployment/safety-boundary.mjs "$VERIFIED_BASE_SHA" "$CANDIDATE_SHA"
```

The path is illustrative, not an existing installed service. Exit0 means only the
narrow presentation classification; exit2 means CONTROL_REVIEW_REQUIRED; exit1 means
unverifiable. The output deliberately states independentEnforcement=UNVERIFIED.
No current workflow consumes this as an independent gate. Wiring an editable PR-head
copy into an editable workflow would not fix the trust problem. A protected control
change must use a separate custodian review route and cannot redefine itself as an
ordinary application change. Never let the candidate supply an approval boolean or
choose its trusted base. Missing custodian identities or external enforcement blocks
activation, not an automatic reviewer allowlist. No provider or GitHub settings were
changed in this task.

## Risk contracts

Each row below specifies all ten required fields. Tests named here are executable
repo evidence, not substitutes for the stated external acceptance tests.

### R1 — Customer data and Tenant/Area isolation

| Field | Contract |
| --- | --- |
| Protected asset/person | Members' records and privacy; administrators' assigned scope. |
| Threat/failure | Cross-Tenant read/export, same-Tenant cross-Area mutation, misleading list/count, import/restore bypass. |
| Safety invariant | Tenant is the hard boundary; Area and explicit permissions constrain administration; count/paging and mutation lookup use the same backend scope. |
| Responsible authority | Business owner sets scope; security custodian qualifies controls; backend enforces each request. |
| Independent technical enforcement | Existing backend guards, transactional Area/grant locks and database constraints; independently protected tests required. ABP filters are not proof against a compromised DB identity. |
| Positive/negative test | Dedicated MemberAreaAuthorizationPostgreSqlTests: assigned access/authorized Host; deny different Area/Tenant, revoked grant, import/restore races and rollback. New shell fixture restores a same-Tenant FK and rejects its violation. |
| Monitoring signal | Redacted authorization-denial/anomalous export/row-count events scoped to protected audit custody; detector delivery must be tested. |
| Containment | Separately authorized session/grant revocation and write fence; do not widen Tenant scope to restore availability. |
| Recovery | Preserve audit/history; restore a qualified backup to an isolated target; reconcile unauthorized reads/writes without fabricating historical state. |
| Remaining uncertainty | Current branch PG execution must be verified; production exposure/exploitation not demonstrated. Direct-SQL compromise protection and live detector acceptance UNKNOWN. |

### R2 — Identity and administrator privilege

| Field | Contract |
| --- | --- |
| Protected asset/person | Account holders, administrator sessions, Area assignments and Host authority. |
| Threat/failure | Forged/stale claims, stolen session, role/permission escalation, revocation race. |
| Safety invariant | Backend checks actual permission and Tenant/Area authority; original authority is stabilized for transaction completion; Host oversight requires explicit permission. |
| Responsible authority | Identity/security custodian owns grants/revocation; application transaction owns stabilization; owner identifies independent reviewers. |
| Independent technical enforcement | Existing persisted grants, auth middleware and ordered database locks; privileged IAM separate from app role. No UI-only authorization. |
| Positive/negative test | Member/import/restore PostgreSQL API suite accepts assigned authority and denies absent/revoked/inactive grants; race cases assert lock wait and rollback. Shell fixture accepts limited LOGIN DML and denies SET ROLE/CREATE ROLE/superuser escalation. |
| Monitoring signal | Privileged grant/session changes, denied escalation and reviewer/settings drift, without tokens or member PII. |
| Containment | Revoke compromised identity and separately fence affected writes; preserve already-committed transaction evidence. |
| Recovery | Re-establish authoritative grants, revoke sessions, reconcile lock/transaction outcomes and review impacted audit records. |
| Remaining uncertainty | Current/historical reviewer custody, production principal memberships, session-revocation propagation and IAM controls remain independently unverified. |

### R3 — Payments, commissions and ledger integrity

| Field | Contract |
| --- | --- |
| Protected asset/person | Members' money, entitlements, participation and financial history. |
| Threat/failure | Deployment enables workers, payment replay, false confirmation, invented zero/default, unapproved commissioning or privileged ledger DML. |
| Safety invariant | Payment is not activation; approval remains separately authorized. Deployment grants no financial/monthly/weekly/D10 authority, does not alter Yoco mode and performs no financial operation. |
| Responsible authority | Owner/financial custodian separately authorizes operations; Area approver owns participation transition; release custodian owns code-only admission. |
| Independent technical enforcement | Existing recovery latch/startup validation, omitted workers, direct-call guards and commissioning evidence; independently restricted financial/provider/DB identities additionally required. A flag alone is insufficient. |
| Positive/negative test | Existing recovery-host PostgreSQL test permits authenticated reconciliation export but denies approval/payment/commission routes and confirms business-data fingerprint unchanged. Release tests accept exact recovery and reject every enabled hold/provider key. |
| Monitoring signal | Independent effective profile/worker/process observation and protected ledger/provider audit; unexpected activity alarms and blocks further rollout. |
| Containment | Stop future admissions; separately authorized writer/provider fence. Cancellation of CI is not cancellation of accepted payment/DB work. |
| Recovery | Preserve immutable records; reconcile provider and ledger identities under separate authority; no replay, EF Down, historical re-label or silent V1 fallback. |
| Remaining uncertainty | Actual normal Render profile is incompatible with recovery-only admission; financial DB/IAM separation UNKNOWN. No financial operation or production commissioning was tested. |

### R4 — Database, backup and recoverability

| Field | Contract |
| --- | --- |
| Protected asset/person | Database consistency, authoritative history, key-store/certificate dependencies and service recovery for members. |
| Threat/failure | Uncontrolled migration/seeding, app DDL, schema drift, incomplete/corrupt backup, non-atomic restore or lost decryption keys. |
| Safety invariant | API startup never migrates/repairs/seeds Production; exact incompatible migration history blocks. Restore uses a separately authorized empty target and fails on errors without declaring partial success. |
| Responsible authority | Database/backup custodian qualifies restoration, schema and recovery objectives; release process only consumes verified compatibility evidence. |
| Independent technical enforcement | Existing startup/migrator preflight plus DB role/ownership restrictions; separate backup/restore principal and authenticated immutable backup custody required. Bundling a migrator binary is not privilege separation. |
| Positive/negative test | Existing AQGreenRecoveryReleasePostgreSqlTests restores actual synthetic application schema/fingerprint and rejects future/missing/gapped history. disposable-recovery.sh adds atomic conflict rollback, truncated archive rejection and real-role DDL denials. |
| Monitoring signal | Restore-drill outcome/age, backup digest/custody, schema drift and denied DDL; application readiness is not proof of backup restoration. |
| Containment | Block admission on incompatible/unknown evidence; isolate failed restore; keep original DB and authoritative history unchanged. |
| Recovery | Independently verify backup identity/digest, roles/extensions, encryption/key material and schema; restore isolated target, verify data/constraints/permissions/readiness, then obtain separate cutover authority. |
| Remaining uncertainty | Production snapshot restoration, point-in-time recovery, key/certificate recovery, scale, RPO/RTO and role restriction UNKNOWN. Fixture success does not qualify a production backup. |

### R5 — Bird, recipients and outbox

| Field | Contract |
| --- | --- |
| Protected asset/person | Recipients' privacy, communication consent, outbox history and provider spend. |
| Threat/failure | Startup delivery, duplicate replay, unintended worker registration or stolen Bird key. |
| Safety invariant | Code deployment does not authorize delivery; recovery has Bird disabled/no key and omits delivery processing. Existing pending records are not replayed. |
| Responsible authority | Messaging custodian separately authorizes delivery; release custodian must preserve the qualified no-delivery profile. |
| Independent technical enforcement | Startup/worker/direct-call guards plus absent or separately held messaging identity, provider/network restrictions and queue ACLs. Outbox flag alone cannot contain arbitrary code with a delivery key. |
| Positive/negative test | Release config tests accept recovery/no-key and reject Bird enabled or key present; existing application recovery tests inspect worker/startup boundaries. Future isolated sink test must prove allowed authorized delivery and denied runtime/direct-worker delivery without contacting Bird. |
| Monitoring signal | Independently observed worker registrations, outbox attempt counts and provider usage; alert delivery verification remains separate. |
| Containment | Separate custodian fences worker/provider credential; leave durable outbox/history for reconciliation. |
| Recovery | Review exact accepted/unknown deliveries and deduplication evidence; separately authorize any retry, never replay the queue just because a deployment failed. |
| Remaining uncertainty | Production key scope/egress/queue permissions and compromised-app containment UNKNOWN; no Bird delivery executed. Current normal configuration is not recovery-qualified. |

### R6 — CI/CD secrets, tests and software supply chain

| Field | Contract |
| --- | --- |
| Protected asset/person | Source, qualified artifacts, deployment credentials and everyone using deployed code. |
| Threat/failure | Fork/untrusted CI, forged identity, skipped/deleted security cases, same-PR pipeline rewrite, mutable dependency/action, credential exfiltration or unverified builder. |
| Safety invariant | Exact trusted completed main-push CI and all required successes bind the frozen attempt/source/target; controls cannot be unilaterally changed by an ordinary application PR; secret access follows independently protected identity. |
| Responsible authority | Organization/security custodian owns independent verifier, workflow/source restrictions and credential custody; reviewer eligibility must be established externally. |
| Independent technical enforcement | Required workflow/check outside candidate authority, source-restricted successful checks, no-bypass path/control governance and least-privilege environment/broker. Existing same-repo tests/controller only provide defense within that trust boundary. |
| Positive/negative test | Existing release suite accepts exact successful CI and rejects fork/stale/missing/skipped/mixed attempts, replay and wrong archive bytes. New classifier accepts narrow presentation and rejects control/test edit/delete/rename, invalid diff/revision and symlink modes. External acceptance must try deleting/skipping a security test while retaining job names and prove the protected gate rejects it. |
| Monitoring signal | Independent verifier identity/revision, required-case inventory/results, credential use, settings drift, action/dependency advisories and artifact/signature failures. |
| Containment | Block automatic admission; revoke compromised key through separate authority; quarantine artifacts and preserve audit evidence. |
| Recovery | Re-establish protected verifier/credential chain, review affected revisions/builds, regenerate qualified artifacts and reconcile any accepted provider operation before retry. |
| Remaining uncertainty | Live independent protection FAIL/not configured; security tests can be edited/deleted in the candidate. Provider build authenticity/SLSA level UNKNOWN. Action tags/base images are mutable; existing dependency advisories require disposition. No ordinary app-change bypass prevention is claimed. |

### R7 — Provider failures, partial deployment and audit

| Field | Contract |
| --- | --- |
| Protected asset/person | Service availability, correct API/frontend pair and trustworthy recovery records. |
| Threat/failure | Render fails but frontend deploys; POST accepted without response; stale/concurrent releases; changed CI after Render; missing or contradictory receipts; false final success. |
| Safety invariant | Render readiness precedes Vercel; every requalification matches frozen CI attempt. Partial IDs/origins survive; ambiguous mutation is never blindly retried or inferred from SHA. Both live revisions must match before VERIFIED receipt. |
| Responsible authority | Release engine sequences requests; independent observer verifies state; operations custodian handles ambiguous reconciliation/rollback. |
| Independent technical enforcement | Actions serialization, exact provider IDs/source/history/readiness and protected immutable receipt custody; provider single-writer/IAM required outside Actions. |
| Positive/negative test | Existing real-Git/synthetic-API rollout positive; Render-failure, CI-rerun-after-Render, three-attempt recovery, cancellation intent, foreign in-flight deployment, contradictory/missing receipt and terminal drift negatives assert no further POST. |
| Monitoring signal | Candidate/release identity, phase, accepted IDs, bounded polling, partial/stalled pair, independent health/recovery observer and missing receipt alarm. |
| Containment | Stop subsequent POSTs/releases; keep known IDs and request intent. A workflow cancellation/timeout does not prove a provider request stopped. |
| Recovery | Inspect exact accepted IDs/status, preserve immutable chain, qualify compatible rollback pair and schema; separately authorize rollback. Never auto-resume service, reset baseline or revert database history. |
| Remaining uncertainty | Actual deployments/rollback/alert delivery and distinct-name receipt retention across GitHub reruns UNVERIFIED. Expiry/deletion/limits block safely. External provider administrator races remain. |

### R8 — Automated agents and delegated authority

| Field | Contract |
| --- | --- |
| Protected asset/person | Owner authority, users, repository controls, production infrastructure and confidential data. |
| Threat/failure | Prompt injection from files/provider output, agent self-approval, excessive tools, production credentials available to an implementation agent, or report mistaken for authorization. |
| Safety invariant | Agent may propose/test/review only within explicit authority; cannot grant itself privilege, merge/deploy, commission operations or turn UNKNOWN into acceptance. |
| Responsible authority | Human owner defines scope; platform/security custodian independently limits execution tools/identity; independent reviewer challenges claims. |
| Independent technical enforcement | Isolated runner with no production secrets, restricted network/tools and branch-only identity; protected external main/release controls and separate human custody. AGENTS.md/prompts/reports alone are instructions, not a sandbox. |
| Positive/negative test | Classifier/release negatives deny control edits or untrusted/replayed identity within their models. Required external test: agent identity may push test branch but cannot update main, obtain production secret, call deploy/configuration/DB/message operations or approve own control change; run against mocks/disposable IAM targets only. |
| Monitoring signal | Agent/tool identity and operation audit, scope change, denied credential/operation request, reviewer independence and provenance of claimed evidence. |
| Containment | Terminate/revoke the agent session/branch credentials and block admission under independent authority; preserve its actions and outputs. |
| Recovery | Review exact changes/tool calls and any accepted operations; re-establish trusted source and scope; never treat agent review as formal eligible GitHub approval. |
| Remaining uncertainty | Current workspace has broad local filesystem/tool access and existing CLI credential custody; no independent production-operation denial was tested. Platform/agent isolation is BLOCKED, not proven by instruction adherence. |

## Comparison with engineering guidance

Comparison baseline checked 16 October 2026:
[NIST SSDF SP800-218 v1.1 final](https://csrc.nist.gov/pubs/sp/800/218/final).
[v1.2 revision1 is an initial public draft](https://csrc.nist.gov/pubs/sp/800/218/r1/ipd),
not silently treated as a final mandate. Practice identifiers below refer to the
[v1.1 text](https://nvlpubs.nist.gov/nistpubs/SpecialPublications/NIST.SP.800-218.pdf).

| SSDF practice | Relevant assessment |
| --- | --- |
| PO.1 / PO.2 | Requirements and accountable identities documented; independent custody unresolved. |
| PO.3 / PO.4 / PO.5 | CI and criteria exist; externally protected tooling and least-privilege environments unverified. |
| PS.1 / PS.2 / PS.3 | Exact source/audit evidence implemented; control tampering prevention, authenticated build provenance and durable retention incomplete. |
| PW.1 / PW.7 / PW.8 / PW.9 | Threat model, review, negative tests and recovery defaults present; production validation remains blocked. |
| RV.1 / RV.2 / RV.3 | Failed evidence/advisories retained; detection ownership, remediation disposition and alert acceptance still required. |

This is a scoped gap assessment, not organization-wide SSDF compliance.

[OWASP CI/CD guidance](https://owasp.org/projects/top-10-cicd-security-risks) is used
as a threat taxonomy, not a certification:

| OWASP risk IDs | Contract coverage and gaps |
| --- | --- |
| CICD-SEC-1 / 2 / 5 | R2/R6/R8: exact qualification, independent custody and identity separation; live enforcement blocked. |
| CICD-SEC-3 / 4 | R6: dependency/advisory review and poisoned pipeline threat; mutable action tags and candidate-editable controls remain. |
| CICD-SEC-6 / 7 / 8 | R3/R5/R6: secret/profile/provider separation; actual IAM/egress/custody unverified. |
| CICD-SEC-9 / 10 | R4/R7: source integrity, restore and audit; provider-built executable provenance, retention and delivered alerts unverified. |

[SLSA v1.2 build requirements](https://slsa.dev/spec/v1.2/build-requirements)
and [source requirements](https://slsa.dev/spec/v1.2/source-requirements) are distinct
technical specifications. Source SHA/archive equality proves a source binding,
not how the running executable was built. The tag-image workflow requests SBOM and
BuildKit provenance, but the production providers rebuild Git source instead of
deploying those images. No verified consumer attestation/signature chain or assessed
builder isolation is demonstrated. Source controls also require evidence of actual
enforcement and history, beyond a proposed ruleset. **No SLSA level is claimed**;
source/build compliance is UNKNOWN until the exact producer/platform/consumer
requirements and authentic evidence are independently assessed.

## Executable evidence and verdict boundaries

Run from repository root; never supply a real target, dump, customer or provider key:

```bash
node --test tools/deployment/*.test.mjs
docker pull postgres:16-alpine
bash tools/deployment/disposable-recovery.sh
```

The shell fixture accepts no arguments or connection string. It uses a local Unix
Docker socket, its own network-disabled/read-only/capability-dropped container and
tmpfs database, only synthetic SQL, a resolved local image ID and private temporary
dump files. Cleanup touches only its created container/temp directory. Cluster roles
are created only there. A real non-superuser LOGIN tests both successful limited
DML and denied DDL/role/privileged-table access; SQLSTATE checks distinguish denial
from a broken connection or syntax error. Custom-format restore compares a prior
snapshot after the source sentinel changes, checks ACL/FK preservation and rejects
conflicting/truncated restores without partial acceptance. The existing application
PostgreSQL recovery test remains the actual-schema restore/readiness evidence.

[PostgreSQL dump guidance](https://www.postgresql.org/docs/16/backup-dump.html)
notes that database dumps do not include global role definitions. This fixture's
roles exist in its own cluster; production restoration must separately qualify
role/extension/key dependencies. [pg_restore](https://www.postgresql.org/docs/16/app-pgrestore.html)
supports transaction/error-stop behavior used here; it is not an automatic production
rollback. No synthetic role policy is asserted to exist on Aqua's live database.

CI runs the new shell fixture in the existing required PostgreSQL job, propagates
failure through pipefail and retains its redacted summary with PG artifacts. The
classifier tests are part of the existing release-safety job. The same candidate
can still edit these jobs: independent enforcement remains a separate blocker.
Final test counts, exact SHA, CI attempts, skipped cases and fresh Agent 2 review
belong in PR #106; earlier 6a5950d results are carried-forward until rerun.

EXPERIMENT RESULT at baseline 6a5950d: in a throwaway Git archive/repository,
changing only the Render-failure regression from `test(...)` to `test.skip(...)`
and running the existing release-safety command returned exit0, with 155 passed,
one skipped and zero failed out of 156. The candidate/worktree was not modified by
that probe and no mutant branch was pushed. This demonstrates that a successful
job alone does not prove every mandatory security case executed. It does not
demonstrate a production attack. Deletion can also shrink same-candidate discovery;
a protected verifier needs its own versioned required-case inventory and must reject
absent, skipped, cancelled or todo security cases, not just compare a candidate to
its own discovery. Updating that inventory is a protected control change.

| Required property | Evidence category and current verdict |
| --- | --- |
| Unauthorized/fork/stale/failed/skipped CI denied; changed attempt cannot substitute | PASS only for executed release-controller isolated tests; actual credential/platform enforcement BLOCKED. |
| Security cases cannot silently be removed/skipped by ordinary app change | FAIL for current independent protection: same-candidate discovery can shrink with deleted tests. Assessment classifier TESTED does not establish enforcement. |
| Compromised runtime cannot obtain privileged DB/financial/message capability | UNKNOWN in production; disposable separate-role mechanisms can PASS their tested cases only. |
| Unsafe migration history blocks without repair | Existing actual-host/PostgreSQL regressions; current-run execution must be checked; comprehensive schema drift/production posture UNKNOWN. |
| Missing/contradictory evidence denied; Render failure blocks Vercel; partial/retry identities preserved | Isolated automated PASS at recorded tested source; actual provider/retry/retention acceptance BLOCKED. |
| Restricted operations stay inactive | Recovery-host/fixture evidence is scoped; effective live normal profile incompatible and independent privilege containment UNKNOWN. |
| Backup restoration works | Existing actual-schema synthetic PG restore plus new failed-restore fixture; production backup/key/scale/RPO/RTO acceptance UNKNOWN. |
| Controls cannot be trivially bypassed by ordinary application-code change | FAIL/not independently configured; source-only validator cannot prove this. |
| Agent cannot exceed authority through available tools | Instructions exist; independent credential/tool containment UNKNOWN/BLOCKED. |
| End-to-end unattended production acceptance | BLOCKED; no merge/deploy/production test or operational enablement performed. |

## Monitoring, containment, recovery and unresolved authority

An independent observer must correlate source/CI/release/target identities, service
revision/readiness/recovery, worker/credential/DB grants and receipt continuity.
Collect only redacted operational facts; never tokens, raw provider payloads or
member/payment identifiers. Missing heartbeat/evidence or conflicting signals must
block new admission. A green CI dashboard or provider READY alone is insufficient.
Alert ownership, delivery/escalation testing, evidence retention/access and acceptable
observation delay require operations/security owner decisions; no arbitrary numeric
SLO, RPO/RTO or freshness window is invented here. The current engine does not
authenticate all these external observations; this remains an activation blocker.

Containment first stops new admissions; separately authorized operators fence actual
writers/credentials/providers. Preserve request intent and accepted IDs: cancellation
does not undo an external side effect. Verify compatible schema, immutable prior
pair, backup and key material before separately authorized rollback/restore. A manual
rollback changes the live pair and requires explicit baseline reconciliation; no
automatic database Down, service resume, payment replay or queue draining is allowed.

The following independent-security and operational properties remain unestablished.
The latest owner task requests unattended code preparation with these limits
disclosed, rather than a claim of independent enforcement or production acceptance.
Separate custodians must qualify:

1. Protected verifier location/revision, actual required check source and no-bypass
   path/change governance; eligible security reviewer/settings custody without
   per-release approval. Current fresh GitHub GETs still show only deletion and
   non-fast-forward main rules, and an unprotected bypass-enabled environment.
2. Read-back principal/grant/network evidence proving distinct deploy/runtime/
   migration/restore/financial/messaging/agent capabilities; actual negative IAM
   tests on disposable identities. Provider API key permission scope is not inferred.
3. A separately qualified safe runtime and Vercel source/build configuration. Fresh finalization GETs on2026-10-16 confirm
   Render recovery=false/Bird=true and disconnected Vercel; see the updated release
   runbook. No live settings were changed.
4. Authenticated restore evidence binding backup digest/snapshot, schema/build,
   roles/extensions, keys/certificate recovery, observed outcome, verifier identity,
   approved retention/freshness and owner-selected recovery objectives. An opaque
   bootstrap reference or self-authored assertion is insufficient evidence.
5. Durable authenticated receipt custody, distinct-attempt retention and bounded
   partial reconciliation, independent monitoring/alert acceptance and rollback drill.

These are one-time infrastructure/operational qualification and continuing automated
policy obligations, not recurring release approvals. Missing authority/evidence
leaves independent-security/production acceptance BLOCKED. The release code itself
uses automatic technical checks, not a manual activation token. Scripture supplies the ethical foundation; human authority
and independent technical evidence determine whether these chosen obligations have
been met. No absolute safety, compliance certification or production acceptance is
claimed.

## Mandatory security-case hardening follow-up

[Mandatory verification and custody](mandatory-security-verification.md) records
PR 106's exact 170-pass/one-skip reproduction, versioned case inventory, strict
classifier, source/run/attempt evidence, signature-consumer tests and executable
candidate tampering probes. The CI job now runs the classifier and retains case
results. This is candidate-controlled hardening: independent verifier custody,
required GitHub enforcement and the pre-credential boundary remain BLOCKED. No
same-repository inventory, signer fixture or artifact name establishes that boundary.
