# AQGREEN V2 — ACCEPTED MVP PRESERVATION AND NODE 24 RELEASE QUALIFICATION

12 October 2026. **A. RELEASE CANDIDATE LOCALLY QUALIFIED — separate derivative candidate.**

The original owner-accepted candidate is preserved and unchanged. Its network-denied
production build failed because `next/font/google` fetched Geist fonts externally.
The separate derivative replaces that build dependency with local font assets.
Its clean locked install, production build, production browser smoke and focused
regressions passed. This result authorizes no deployment or production use.

## Accepted source and preservation

Owner authority is the direct 12 October acceptance instruction, recorded in
[the separate acceptance record](aqgreen-v2-isolated-mvp-owner-acceptance-20261012.md).
The accepted subject is HEAD `e9bd8e96d55d03466492ffd40dd657802ae36d4b` on
`feature/admin-ui-remediation` plus the documented local modifications. It is not
bare HEAD. The original 1,739-file accepted source manifest is:

`72219b17d8cd5a55f9c0b542190caa969bb5193131fa2924fdefcdff54d1e26c`.

Preserved bundle:
`/home/wtc/Downloads/newAqua/aqgreen-v2-accepted-20261012-72219b17d8cd`.
Its source directory contains **2,101 files**, including **46 required untracked
files**, tracked modifications, backend/frontend tests, the original 17-case
walkthrough, independent review and acceptance evidence. Git status, binary
tracked diff, file inventory and exclusions are retained separately in metadata.
The new owner-acceptance record is metadata added after preservation; it does not
redefine the original accepted snapshot.

| Accepted preservation artifact | SHA-256 |
| --- | --- |
| `source.sha256` | `a21093f4319ac454a3308ea68a3544b0eddc8d5b51852e84ff1262d3738dd384` |
| `accepted-source.tar.gz` | `784836d801cb459f1026e24805fb4c0b52b79e86ad6b4ed0838b0349d4964aa5` |

GNU tar uses sorted names, epoch timestamps, numeric owner/group zero and
`gzip -n`. SHA-256 checks passed for all **2,101 source files** and all **2,101
independently extracted archive files**. A final comparison against the original
repository also matched all 2,101 files. No original accepted file was edited.
Credentials, ignored environment files, tokens, private keys, personal-data dumps,
Git internals, build outputs and dependency caches are excluded. No database dump
or browser recording was preserved. Source `.env.example` placeholders remain.

## Exact qualification environment

| Component | Executed version |
| --- | --- |
| Application Node.js / npm | **24.21.0 / 11.19.0** |
| Next.js | **16.3.6** |
| React / React DOM | **19.2.4 / 19.2.4** |
| TypeScript / Vitest | **5.9.3 / 3.2.6** |
| ESLint / eslint-config-next | **9.39.4 / 16.2.11** (existing lock versions) |
| .NET SDK / ASP.NET Core runtime | **8.0.131 / 8.0.31** |
| PostgreSQL | **16.15** |
| Browser driver / browser | Playwright **1.62.1**, Chromium **151.0.7922.34** |
| Browser driver Node.js | **26.6.0**; application server and frontend tests use Node 24 |

All direct locked package versions are recorded in
[`dependencies.json`](../verification/aqgreen-v2-node24-qualification-20261012/dependencies.json).
The complete dependency lock is preserved in both source bundles, unchanged:
SHA-256 `1a9e2d3ef91b972c50af82baa9dadf9707d1e978f211b734e157892d8dc64704`.
The Node image digest is
`node@sha256:ebfe2f90462722a7a4de65e91990e97fe0d401c70e0e762c5b53302f905ec1c1`.
PostgreSQL digest and the existing local .NET runtime image ID are in
[`images.txt`](../verification/aqgreen-v2-node24-qualification-20261012/images.txt).

The frontend ran its actual standalone production output using
`NODE_ENV=production`, `node server.js`, copied `.next/static` and `public`.
The backend ran `AQGreenV2Demo`, the exact environment required by its existing
synthetic-money safeguards. ASP.NET Production plus synthetic opt-in was separately
executed and refused startup. No Production backend or production services were used.

Both frontend installations/builds ran in Docker `--network none`. Backend restore,
publish and focused tests ran under `bwrap --unshare-net` with existing NuGet cache;
NuGet audit was disabled for offline restore, so this is not a vulnerability-audit
claim. The initial offline npm cache miss was retained; 18 exact locked tarballs
were subsequently provisioned from `registry.npmjs.org`, with no application scripts
or provider access during provisioning. A public upstream OFL license was retrieved
before the isolated builds. Fresh `npm ci --offline` then succeeded for both clean
frontend copies, installing 566 packages. No lock, package list or framework changed.

Runtime used an internal Docker network with no default external route and no
published TCP ports. API, frontend and PostgreSQL shared the disposable namespace.
The browser ran under `bwrap --unshare-net`, connecting through fixed local Unix
bridges to the frontend/API ports only. Browser routes also denied other origins.
No Yoco or production request was executed. This isolation was not a configuration
bypass around the application's fail-closed demo guard.

## Executed results

These are newly executed Node 24 qualification checks. The prior **17/17 acceptance
cases are preserved, carried-forward evidence**, not newly rerun as seventeen cases.

| Check | Result and observed evidence |
| --- | --- |
| Original accepted clean install | PASS — Node 24, locked offline npm ci |
| Original accepted production build | **FAIL** — Geist/Geist Mono Google font fetch on denied network; original log retained |
| Derivative fresh install / production build | PASS — offline npm ci, Next compilation/type checking/static generation and standalone output |
| Production startup / session | PASS — production server ready; browser login, refresh, Secure/HttpOnly/SameSite=Lax session chunks, anonymous redirect/API401, logout204 and cleared cookies |
| Signup / verification / invitation | PASS — two new synthetic accounts created through browser forms, isolated verification-link helper, browser email verification and invitation return |
| Full joining payment | PASS — browser R1,200 synthetic confirmation; R1,199 rejected400 with unchanged participation; exact replay idempotent; other member denied404 |
| Two instalments | PASS — two distinct browser R600 checkouts; first leaves R600 due; early approval denied without placement; second permits pending approval |
| Approval / attribution / progress | PASS — member and out-of-Area admin denied403; assigned Area admin approves in browser; one approval/placement/attribution per member; approval retry unchanged; sponsor-change/self-sponsor rejected; active programme/progress routes load after fresh login |
| Administrator permissions | PASS — Area review, explicitly granted central review, compatible host financial UI; out-of-Area Confirm/Reject/BeginReview403; cross-Tenant list403 and ordinary member review403; central calculation403 |
| Weekly sales review | PASS — initial Held decision blocks calculation without ledger writes; Area confirms5/5/5; duplicate confirmation preserves reviewer/time; central confirms5/5/4 shortfall; rejected and Held decisions persist through browser actions |
| Versioned commission / display | PASS — root test ledger R400 (R150 Level1 + R250 Level2); PlacementV2; `AQGreenWeeklyCommissionDecisionV1`; rate version `aqgreen-v2-demo-commission`; repeat creates0 records; newly placed members excluded from earlier closed period; browser says test calculation/no payout |
| Financial-integrity denial | PASS — financially permissioned host Release and RecordPayment direct calls denied500 with explicit simulated-money details; unchanged ledger; ReleasedAt/PaidAt remain null across all ledgers |
| Production routes / fonts | PASS — dashboard, programmes, progress, invitations and profile200; admin review/approval/earnings, signup/login/verification/checkout exercised above; local font bodies200, hashes equal source; font loading complete; no browser JS errors, server500s or external asset requests in route smoke |
| Focused frontend regressions | **23/23 PASS**, four authentication/session files under Node24; changed layout lint PASS |
| Focused backend isolation regressions | **13/13 PASS**, demo classification/registration and real gateway guards, under denied network; includes accidental real gateway registration and fail-before-provider tests |
| Production synthetic opt-in startup | PASS negative — exit1, explicit demo-in-Production refusal before services start |

Browser/API actions and PostgreSQL readbacks are in
[`events.jsonl`](../verification/aqgreen-v2-node24-qualification-20261012/events.jsonl).
Build logs, version records, operator scripts, fixture source, lock checks and
commands are in the same [evidence directory](../verification/aqgreen-v2-node24-qualification-20261012/).

Only account/permission/Area prerequisites were added by the fixture helper.
The existing demo seed supplies the separately identified pre-cutoff synthetic
qualification tree. New members were signed up, paid and approved through the
production browser; their placement dates were not backdated. The calculation
period was 1 October22:00 UTC through 8 October21:59:59.999999 UTC. Newly created
placements contributed no closed-period commission evidence.

All34 payment records had synthetic provider labels: 31 seed records
`AQGREENV2DEMO`, three newly confirmed payments `AQGREEN_V2_DEMO`.
No real payment or payable income is asserted. A Held sales decision and an
unreleased commission ledger remain distinct. The earlier monthly-obligation
financial-Held case is carried forward, not newly executed here. Synthetic/manual
sales confirmation establishes administrative software behavior, not independently
verified retail commerce or market demand.

## Narrow correction and separate identity

Reproduction: copy accepted frontend; clean offline npm ci with the recorded Node
image; run `npm run build` with `--network none`. Next fails on the two Google Fonts
fetches from `app/layout.tsx`. This is a pre-existing external build dependency;
no evidence identifies a Node24 runtime defect.

Derivative bundle:
`/home/wtc/Downloads/newAqua/aqgreen-v2-node24-candidate-20261012-5e133be58439`.
Exactly five paths differ from the accepted snapshot:

- `AqualLifeStyle/9.4.2/aqua-frontend/app/layout.tsx`
- `AqualLifeStyle/9.4.2/aqua-frontend/app/fonts/geist-latin-variable.woff2`
- `AqualLifeStyle/9.4.2/aqua-frontend/app/fonts/geist-mono-latin-variable.woff2`
- `AqualLifeStyle/9.4.2/aqua-frontend/app/fonts/OFL.txt`
- `AqualLifeStyle/9.4.2/aqua-frontend/app/fonts/README.md`

`next/font/local` retains the existing CSS variables, normal Latin variable-weight
Geist families,100–900 weights and swap display. Actual existing font output was
promoted into explicit source assets; font metadata and generated CSS established
provenance, and the upstream SIL OFL1.1 is included. No font-fetch mock, dependency
addition, business-rule change or production-safeguard change was introduced.

| Derivative preservation artifact | SHA-256 |
| --- | --- |
| `source.sha256` — **candidate identity** | `5e133be584398b2db14a5c542e27860bd6f02adae0c66c1488ef30e7ee2573e0` |
| `derivative-source.tar.gz` | `0af6babebb839c3e65cf4df4797d59822ba84700829b2f0e96e0cdbd87faf11d` |

All **2,105 source checks** and **2,105 independently extracted archive checks**
passed. Parent identity and exact changed-path/hash inventory are preserved in
`metadata/identity.json`; `metadata/layout.patch` contains the minimal code diff.
The derivative has no new Git commit and has not been silently substituted for the
formally accepted original.

Seven stopped operator attempts remain in the event log. They involved the wrong
cookie name, the API client's treatment of Secure cookies on HTTP loopback,
expected token invalidation after activation, checking error message instead of
error details, resolving font URLs relative to the page rather than stylesheet,
incorrect synthetic-provider labels and expecting logout200 rather than204.
Each affected check was resumed/repeated against its actual existing contract;
application authorization or assertions of durable financial effects were not
relaxed. The sole application correction is the separate font change above.

## Limits and remaining blockers

**No remaining blocker to this derivative's local qualification.** The original
accepted candidate retains its documented offline production-build failure.
Owner acceptance of that original is preserved separately from this subsequent
technical correction and evidence.

No broad test campaign, new17-case acceptance rerun, live-provider verification,
production environment, security audit, human usability study or commercial
validation was performed. Existing compiler/deprecation/install-script warnings
remain in logs; dependency upgrades were outside scope. No production credentials,
real customers, releases, payouts, loans, B6/B7 operations, deployment or Git
publication occurred. Task-owned disposable services and temporary secrets were
removed after evidence capture.

Proverbs18:17 supports examination, Proverbs11:1 accurate representations and
1Corinthians4:2 faithful stewardship. Derived engineering requirements were source
preservation, independent hash checks, scoped authority and truthful simulated
amounts. Technical conclusions rest on the executed evidence described above.

Status: **TESTED**, locally in isolated infrastructure. The qualified derivative
remains subject to separate authorization for any deployment or production use.
