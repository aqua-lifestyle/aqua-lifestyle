# AQGREEN V2 — OWNER WALKTHROUGH EXECUTION-READY PACKAGE

**Preparation verdict: READY TO SCHEDULE.** The qualified candidate, existing
tooling and execution instructions are identified. This is package readiness,
**not a claim that a live session environment or accounts have been provisioned**.
Complete the pre-start checklist before admitting participants. Participants are
not yet designated; no consent or observations exist. All 17 human case results
remain **NOT RUN**. No human-walkthrough ACCEPTED decision is recorded.

Baseline: [existing 17-case independent checklist](aqgreen-v2-independent-operational-mvp-acceptance-checklist.md).
Use that checklist; do not create a second test programme. Cash, retail/inventory,
loans, Onyx, B6 and B7 remain deferred.

**Resumed candidate reconciliation:** later [agent-operated results](aqgreen-v2-agent-executed-operational-mvp-walkthrough-results.md)
report 17/17 isolated software/procedure cases, with failures and retests retained.
The [recorded owner acceptance](aqgreen-v2-isolated-mvp-owner-acceptance-20261012.md)
concerns that **technical** original only; it does not establish human observations.
The [Node 24 qualification](aqgreen-v2-accepted-preservation-node24-qualification.md)
identifies a separate derivative. None of these records changes the 17 human
outcomes from NOT RUN or supplies participant designation/consent.

For preparation, identify the **latest locally qualified derivative**, source
manifest `5e133be584398b2db14a5c542e27860bd6f02adae0c66c1488ef30e7ee2573e0`.
Its parent is the unchanged accepted original manifest
`72219b17d8cd5a55f9c0b542190caa969bb5193131fa2924fdefcdff54d1e26c`.
The derivative changes only layout/local font assets; it is not silently substituted
for the formally accepted original. Before launch, the facilitator verifies the
selected preserved artifact and qualification against these identities; this
preparation did not independently rehash the preserved bundles. Human sign-off
must name the candidate actually shown, not bare HEAD or “latest” without identity.

## 1. Latest implementation readiness

| Item | Current evidence and effect on walkthrough |
| --- | --- |
| Tenant versus Area in weekly earnings | **Closed:** source now passes `scopeLabel="Tenant"`, labels validation/ledger Tenant and still submits `tenantId`. Agent 1 reports 7/7 component checks, TypeScript and focused ESLint. No new browser result is inferred. |
| Administrator sales-review roles | **Closed:** Tenant Admin/SystemAdmin review grants and explicit central scope are mapped; financial grants remain separate. Agent 1 reports 9/9 persisted SQLite mapping checks. Earlier 14/14 JWT/PostgreSQL authorization results are carried forward, not replaced by SQLite. |
| Joining prerequisite wording | **Still present:** `ClubMemberProgrammeParticipationAppService.cs:305–308` says placement must precede checkout. Normal invitation joining creates participation first, so the guided path is usable. Explain attribution/joining versus placement; record any confusion/assistance and mark FAIL if the required understanding or result is not achieved. Agent 1 owns any correction. |
| Sales “verified quantity” terminology | **Still present:** `AdminWeeklySalesReviews.tsx:379,430–432`. It denotes administrative evidence review, not independent commerce. Use the playbook definition; observe understanding rather than claim corrected wording. |
| Financial hold versus unreleased ledger | **Distinction closed in evidence description:** Agent 1 now calls the R400 browser result unreleased and the R150 overdue-obligation Held result separate carried-forward evidence. A runtime positive Held fixture/result is required for C2; source assertions alone do not pass the human case. |
| Later executed financial-hold case | **New reported evidence:** agent-operated C2 records positive R150 Held, original cutoff obligation/reason, browser On hold and null release/payment times. Node 24 qualification carries that case forward rather than claiming a new hold run. Reuse its existing controlled fixture; it is still not a human PASS. |
| Current Node 24 runtime | **Reported locally qualified derivative:** clean offline install/build and standalone browser smoke passed. The original fails its denied-network production build on external Google font fetch; use the identified local-font derivative, not an unqualified rebuild or external-font network exception. No deployment is authorized. |

No demonstrated critical payment/authorization defect was established by this
preparation review. The remaining wording issues are tracked, not described as
resolved. If the owner requires correction before a meaningful session, wait for
Agent 1's corrected source and relevant verification; do not edit its code.

Sources: [Agent 1 authorization qualification, final alignment](aqgreen-v2-admin-sales-authorization.md#final-administrator-review-alignment--ready-for-owner-walkthrough),
[operational acceptance](aqgreen-v2-operational-mvp-acceptance.md), and corresponding
source inspection, plus the explicitly attributed newer records above. The prior
[independent engineering review](aqgreen-v2-agent-executed-walkthrough-independent-review.md)
is a repository record of earlier evidence review, not a runtime execution by this
preparation session. No application tests were rerun by Agent 2.

## 2. Executable environment preparation checklist

**Facilitator completes and initials each box. Any unmet box prevents starting the
human session, regardless of the scheduling verdict.** Use existing .NET 8,
Docker PostgreSQL 16, repository Node 24 container and guarded demo conventions.
Do not use generic `docker compose up`: that profile includes a migrator and
provider configuration outside this walkthrough setup.

- [ ] Identify the exact local candidate/build and task-owned resources; verify
  the selected derivative identity and Node 24/label/role qualification match them.
  Keep mounts/artifacts separate
  from Agent 1's editable worktree. Do not alter branch, source, deployed settings
  or production databases. Do not run B6 migration or any historical conversion.
- [ ] Use a disposable PostgreSQL instance containing only the existing synthetic
  fixture/schema. **Database name must be `aqua_aqgreen_v2_demo`**, with connection
  host `localhost` or loopback; demo validation rejects other names/hosts. No
  production backups, credential files, customer exports or real certificates.
- [ ] Reuse the qualified **Docker internal network / denied-outbound namespace**.
  Application and database must have no default/external route; frontend points
  only to the local API. No host-network fallback or production address. Share the
  PostgreSQL network namespace where needed so the API's database endpoint remains
  loopback. Bind human access to loopback only; remote access is not assumed.
  The dedicated browser must use the restricted demo access arrangement, not a
  personal profile with unrelated external tabs. Verify its permitted route scope
  before tasks; if the local browser cannot reach the isolated stack safely, stop.
- [ ] Before startup, explicitly set the demo profile below with local synthetic
  secrets supplied privately. Do not inherit production `.env`, provider keys or
  real user settings. Keep optional workers/delivery disabled. No outbound probe
  to Yoco or a production host is needed or permitted.
- [ ] Start the **existing qualified API and frontend launch commands** inside
  that isolated setup. Use the existing Node 24 container, not this machine's
  Node 26 host runtime. Reuse the qualified derivative's standalone output and
  operator setup retained in `docs/verification/aqgreen-v2-node24-qualification-20261012/`;
  application server Node was 24.21.0. Do not rebuild the original with an external
  font-fetch exception. This package does not invent a new launch script.
- [ ] Verify local health advertises demo capability, startup selects
  `AQGreenV2DemoCheckoutGateway`, and all relevant pages show the TEST banner.
  Inspect route/configuration evidence without screenshots or trace recording.
- [ ] Create/reset only synthetic accounts through existing fixture and admin
  tooling; map the proposed roles below. Inspect current grants and effective
  active Area assignments. Do not assign blanket central scope to Area admins.
- [ ] Prepare separate cases for full payment and two instalments; no joining
  payment yet on those test accounts. Use synthetic signup addresses accepted by
  the guarded verification helper, not real email delivery.
- [ ] Prepare sponsor S with **zero immediate children**, retain its recorded path
  and scope, and prevent competing placements in S's subtree while testing six
  new members. Default 31-member fixture is already full; it is not this branch.
- [ ] Read the application's actual reporting week. Use the existing pre-cutoff
  synthetic tree for financial cases and new accounts for joining/placement cases.
  Do not backdate new members or change the clock to force closed-week qualification.
- [ ] Provide independent fresh scenarios for confirmed-met, confirmed-shortfall,
  rejected and held evidence, and two aliases of one sale line for duplicate
  allocation. Prepare the manual case/reason/allocation register from the playbook.
- [ ] Prepare an unreleased positive ledger **and a distinct positive financial
  Held ledger** with its recorded cutoff reason and supporting overdue fixture
  fact. Reuse Agent 1's existing controlled fixture/evidence; do not invent policy,
  amounts or a new UI to create it. Existing prepared-fixture source/operator
  evidence is retained in `docs/verification/aqgreen-v2-agent-walkthrough-20261012/`;
  the previous runtime/database was cleaned up. If C2 is not prepared for actual
  human inspection, mark NOT RUN; a reported agent PASS does not fill that field.
- [ ] Confirm backend demo prohibition on Release and RecordPayment, and hidden
  payout UI. For C3, use the existing otherwise-authorized synthetic financial
  operator so denial proves the demo guard, not only absence of permission.
- [ ] Disable **audio, video, screen capture, screenshots, browser traces/HAR,
  session replay and recording extensions**. Do not run Playwright recording
  defaults. Use a fresh synthetic browser profile; no personal accounts/history.
- [ ] Owner access/retention fields are completed and roles designated. Obtain
  informed consent to participation before any human task, and separate agreement
  to written notes before collecting observations. No outreach until designation.

### Required profile (values are setup requirements, not current runtime claims)

| Setting | Required value / handling |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `AQGreenV2Demo` |
| `AQGreenV2Demo__Enabled` | `true` only inside isolated demo |
| `AQGreenV2Demo__Fixture__Enabled` | `true` for existing fixture setup; recognize its pre-filled tree |
| `AQGreenV2Demo__Fixture__MemberPassword` | Privately generated synthetic secret, at least 16 characters; never put it in this record |
| `ConnectionStrings__Default` | Task-owned PostgreSQL, loopback host, exact demo database; credential value stays private |
| `App__ServerRootAddress`, `App__ClientRootAddress`, `App__CorsOrigins` | Matching local loopback URLs; client must be plain HTTP, no query/token |
| `ASPNETCORE_URLS` / exposed ports | Match the chosen local API URL/port inside the isolated namespace; Docker host publication is loopback-only, never unrestricted |
| `NEXT_PUBLIC_ABP_API_URL`, `NEXT_PUBLIC_DEFAULT_TENANT_NAME` | Local API and the synthetic `Default` workspace; not a business Area selector |
| `Yoco__SecretKey`, `Yoco__WebhookSecret` | Empty; no test/live provider credentials |
| `Bird__Enabled`, `App__WeeklyCommissions__Enabled`, `App__EntryMonthlyObligations__Enabled` | `false`; use the guarded local verification helper, not external delivery or automatic period/obligation creation |
| Auth/data-protection secrets | Local synthetic values only; no production credentials or key directories |

Independent prerequisite checks performed: local Docker Unix-socket endpoint and
server 29.2.1 available; existing `postgres:16-alpine` and `node:24-alpine` images
present; a fresh `bwrap --unshare-net` route inspection showed only loopback
routes, no default/external route; .NET 8.0.131 present.
Host Node is 26.6.0, whereas `package.json` declares 24.x; the existing
`docker/web/Dockerfile` uses Node 24. These checks **do not prove an application
stack is running, accounts exist or route isolation has been checked on that stack**.

## 3. Proposed actors and exact permission aliases

**These are proposed synthetic profiles, not appointed participants.** One person
may use several profiles in separate sessions. Do not collect a name/contact map.

| Alias / proposed profile | Required grants and record boundary |
| --- | --- |
| **O — Owner observer** | Synthetic read-only/self-view profile; no automatic administrator rights because a human is the owner. Observe other actors live; no recording. |
| **M — Member / signup** | `Aqua.ProgrammeParticipations.Join` and `.ViewSelf` for own records; existing signup/verification. Guest becomes Member only through approved participation. |
| **I — Sponsor** | `Aqua.ProgrammeParticipations.Invite` and `.ViewSelf`; Active AQGreen participation in the synthetic Tenant/scope. |
| **A — Area approver** | `Aqua.Admin.ProgrammeParticipations.View`, `.ViewPaymentCheckouts`, `.Approve`; active/effective/unrevoked assignment to the target's active Area inside Tenant. |
| **R — Area sales reviewer** | `Aqua.Admin.Commissions.ReviewAQGreenWeeklySalesEligibility`; same-Tenant active Area assignment. Seeded Admin/SystemAdmin mapping verified; no `AllTenants` or financial grants. |
| **CR — Central sales reviewer** | Review grant plus explicit `Aqua.Admin.AllTenants`; only the approved synthetic central fixture scope. No automatic calculation/release grant. |
| **F — Financial operator / facilitator** | `Aqua.Admin.Commissions.View`, `.Calculate`, `Aqua.Admin.AllTenants`, and `Aqua.Admin.Tenants.View` for existing Tenant selector. For C3 only, use the existing synthetic Host financial test profile with `.Release`/`.RecordPayment` entitlement; backend demo prohibition must still deny. |
| **X — Negative actors** | Ordinary Member/anonymous; admin without target Area assignment; revoked/inactive assignment; central actor lacking broader grant. Each denial has an unchanged-state check. |
| **W — Register writer** | On-duty task performed by an already-authorized R/CR actor; one active writer, no new host-only reviewer class or record access grant. |

Human case permission references use these aliases. F's read-only technical
inspection concerns only the task-owned synthetic database/evidence, not production.

## 4. Plain-language consent — proposed, read before collecting notes

**Complete owner-confirmed fields first:** allowed readers `[role labels]`;
custodian `[role label]`; restricted note location `[owner-approved location]`;
retention `[period or deletion date]`; withdrawal/deletion handling `[agreed procedure]`.
Do not promise a period, reader group or deletion policy that the owner has not agreed.

> We are checking whether Aqua's administrators can use the membership MVP
> accurately. We will use only synthetic accounts and simulated money. No real
> payments or customer records will be used. There will be no audio, video,
> screenshots, screen recordings or other recordings.
>
> With your consent, we will write brief notes using role labels rather than names
> or contact details: which task was attempted, what happened, any error or
> misunderstanding, and whether help was needed. Only **[allowed readers]** may
> access these notes at **[restricted location]**; **[custodian]** will retain them
> for **[agreed retention]**, with **[withdrawal/deletion handling]**.
>
> Participation is voluntary. You may decline a task, decline written notes or stop
> at any time. Do you understand this and agree to participate in the walkthrough?
> Separately, do you consent to the de-identified written observations described?
> We will not begin tasks without participation consent or collect notes without
> written-observation consent.

Ask separately for each designated role participant. Record only a minimal
role-labelled consent entry in the agreed location **after agreement**. No signature,
name or contact information is required by this proposed protocol. If consent is
absent/withdrawn, stop notes; the human case is NOT RUN unless an agreed observation
was actually collected. Never manufacture consent or treat owner authorization
as another participant's consent.

## 5. Use the same 17 cases

The [baseline](aqgreen-v2-independent-operational-mvp-acceptance-checklist.md#2-administrator-walkthrough-and-expected-outcomes)
now retains each actor/action/expected evidence/prohibited outcome and adds
permission aliases, **NOT RUN** and a blank written-observation field. J1–J6,
P1–P2, S1–S6 and C1–C3 are unchanged case identities. S1 includes both Area-scoped
and explicitly authorized central review. No screenshots or traces are evidence.

A human PASS requires all required substeps observed after consent. If only a
facilitator/API action or previous test is available, record that category and
keep unobserved human substeps NOT RUN. Do not fill a human PASS from an automated
result. S6's duplicate allocation is a manual control, not software enforcement.

## 6. De-identified written-observation and acceptance template

```text
Session / candidate / synthetic fixture / reporting week:
Owner-designated role labels (not names):
Written-note readers / custodian / location / retention confirmed:
Participation consent per role: NOT OBTAINED / agreed after explanation / withdrawn
Written-observation consent per role: NOT OBTAINED / agreed after explanation / withdrawn
No-recording controls checked:

Case ID / actor alias / required scope:
Human result: NOT RUN / PASS / FAIL
Action actually attempted / expected outcome / actual state:
Error or misunderstanding (brief paraphrase; no identifying quotes):
Assistance: none / instruction repeated / guided / task completed by facilitator
Human evidence reference (consented written entry only):
Engineering evidence reference (Agent 1 test, date/build/limits):
Independent review reference (Agent 2 source/check, date/limits):
Prohibited outcome observed? / unchanged-state check:
Defect reproduction: synthetic preconditions → action → actual → expected
Next action / accountable role:

Owner acceptance: NOT GIVEN / ACCEPTED / CORRECTION REQUIRED / INCOMPLETE
Owner decision reference and stated scope (only after explicit decision):
Unobserved cases / outstanding defects / exception policy boundaries:
```

Maintain **three separate evidence categories** throughout:

1. **Engineering:** Agent 1's executed checks, attributed as reported or carried
   forward, including later 17/17 agent-operated execution and separate Node 24
   qualification; no new human learning inferred.
2. **Independent review:** Agent 2's source inspections and local prerequisite
   checks performed personally; no application suite rerun or live stack claim.
3. **Human operational:** actual role-labelled observations after designation and
   consent. **Currently none.** All 17 cases remain NOT RUN.

Do not place raw observation notes in source control. The owner-confirmed restricted
location governs access/retention; this repository template contains no human data.

## 7. Exact outstanding owner / facilitator decisions

- Designate participants **by role**, choose local participation method/date, and
  state who can give the final owner acceptance decision. No outreach yet.
- Confirm written-note readers, custodian, restricted location, retention and
  withdrawal/deletion handling; then obtain individual informed consent.
- Designate the manual register writer and central coordinator with existing
  approved access. No broader authority is inferred.
- Decide whether remaining wording must be corrected before the guided session;
  otherwise use the explicit explanations and record any resulting confusion.
- Facilitator supplies the qualified launch/setup handoff and prepared synthetic
  accounts/Area assignments, empty branch and positive-held fixture/result.
  Complete all pre-start boxes before a live session. No production access,
  retail development or historical migration is needed.
- Keep the recorded original technical acceptance separate from any new human
  acceptance. State the selected derivative identity and obtain an explicit owner
  decision before recording the human walkthrough as ACCEPTED.

## 8. Final preparation verdict and governing responsibilities

**READY TO SCHEDULE — package and qualified technical candidate identified.**
Participant designation, note governance and consent remain required. The live
stack/accounts/fixtures were **not created or verified by this preparation**;
pre-start completion is a separate session condition, not a concealed PASS.
If isolation, a necessary prepared case or relevant corrected-build evidence is
unavailable at preflight, mark that case NOT RUN and preparation **BLOCKED** rather
than improvise. The later walkthrough result remains **INCOMPLETE** while required
human evidence or owner decision is absent; use **CORRECTION REQUIRED** for specific
demonstrated defects and **ACCEPTED** only on explicit owner approval of the stated
scope. No walkthrough occurred and no ACCEPTED verdict is authorized here.

Scripture is applied from its context: honest measures (Proverbs 11:1), examining
accounts (18:17), diligent plans (21:5), faithful stewardship of entrusted matters
(1 Corinthians 4:1–5), accountable collection administration (2 Corinthians
8:16–24), and others' interests (Philippians 2:1–8). Observable responsibilities:
truthful amounts/evidence labels; examine contrary cases; prepare before clicks;
personal role sessions and preserved history; no financial bypass; informed
consent, voluntary withdrawal and no recordings. Technical and human claims still
need their own evidence; Scripture does not supply a test result or acceptance.
