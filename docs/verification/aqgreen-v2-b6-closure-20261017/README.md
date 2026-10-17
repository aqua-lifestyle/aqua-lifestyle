# B6 acceptance delta and B7/D10 handoff — 2026-10-17

**B6 acceptance: BLOCKED. Commissioning readiness: BLOCKED.** One bounded
discovery-evidence defect is **IMPLEMENTED / TESTED**. This is execution evidence,
not business authority, an independent acceptance receipt or production permission.

Follow-up: [bounded original-evidence recovery and PB1 decision prerequisite](recovery.md).

## Source and scope

- New branch: `feature/b6-closure-qualification-20261017`, based on exact main
  `23f176db6eb13af5ee01ff69f1a9f7bbe6b880ff`. Its implementation/test identities
  are pinned in [evidence.json](evidence.json); the enclosing Git commit binds this record.
- Preserved B6 implementation/source-freeze checkpoint:
  `3148482f2c51878cc8937531340cdf90da51b0f9`. Published branch
  `b6/nonprivileged-attempt-account-foundation` is at
  `438852eca9f91ef973802156ebbd31c3fa3940ed`.
- [PR98](https://github.com/aqua-lifestyle/aqua-lifestyle/pull/98) is OPEN,
  explicitly SUPERSEDED and not accepted for merge. The 100 predecessor B6 paths
  remain preserved there. [PR99](https://github.com/aqua-lifestyle/aqua-lifestyle/pull/99)
  merged the clean MVP at `21a41694163ef09628fd84a9f4e12cb0efe1bb85`, excluding
  those paths. It did not accept the complete B6 publication/runtime package.
- Current main contains the later discovery/frozen-review and offline D10 integration.
  D10 engineering commits `21af7fd10abf73108c415a7da747080c866a601f` and
  `38a5d4f4f53e7d1d1b5dd5dfba6da237aa839d29` are ancestors of this branch.
- The old B6 worktree retains 43 untracked partial successor implementation
  candidates and its separate closure log. They are not published by this change.
  That log's last dependency is PB1's exact invocation-contract rule.
- [PR106's critical-path register](https://github.com/aqua-lifestyle/aqua-lifestyle/pull/106#issuecomment-6231733983)
  was read at head `cd4cd6dac1c694a8f1d582f48b3b8c14c8dfd99a`.
  That branch is separate. Its B1/B2 commissioning blockers remain applicable.

## Acceptance matrix

PASS below is bounded technical evidence unless an existing acceptance is explicitly
identified. UNKNOWN means the claim cannot be established; BLOCKED means a required
input/implementation/witness is unavailable. No current failed check is hidden by a
PASS: the deliberately red regression is separately recorded as FAIL-before-fix.

| Contract requirement | Exact authoritative evidence / witness | Status | Remaining blocker |
| --- | --- | --- | --- |
| B6 source identity | Git objects `3148482…`, published `438852e…`; PR98/99 metadata freshly read | Identified; publication PASS, integration BLOCKED | No qualified full-B6 replacement PR or final source freeze; PR98 remains superseded |
| Publication-root normative contract | Old closure log records parent manifest `2c147a9286ff469aa91eed6191731d8096b34b14a88d916bdbcdb6f8515bc923` and owner-freeze receipt `94f724d19657aa7d6f036f73bc2892540e094c51033d2bb5ef085670833d9747` | Recorded acceptance; current exact-subject verification UNKNOWN | Named parent design/freeze workspaces are absent at recorded paths; custodian must supply retained exact subjects/receipt |
| Publication-root execution qualification | Existing native account replay is pure; latest successor log reports unfinished producer, durable journal/HEAD/ACK/recovery, root publication/reload and helper | BLOCKED | Complete implementations and original runtime/durability/transitive-reference qualification; a manifest/hash bag is insufficient |
| BIND and six live adapters | Recorded AS1 manifest `a49938c8abf5876b984398bf41e3b902e6fe3fbfb08e9d50df40e9ccb6b8c124`, freeze receipt `5e1d1847e5edbe7fee609db422d65eb4309d9ba319ebed9cd2b75060cb37f65f`; recorded contract acceptance only | BLOCKED | Retained normative bytes unavailable at inspected paths; live CREATE/POPULATE/PROTECT/PIN/VERIFY/RELEASE, helper and complete BIND admission remain unfinished/unqualified |
| Runtime supply / authority / technical qualification | Recorded SS1 `090f963433aa0c2874c5403b97e3d57718bbf78b7a9242af9ad3ccabc02ffc58`, AR1 `eabe57fefac023fb9fe43730e2f366edfe27f732684ae6dd014c7244e7f8570b`, TQ1 `50bf5b4bf5d932060a60f46d3ded1fc470a8dd26d7f3b531eef63f726bcccda7` | BLOCKED | Exact bytes/receipts, PB1 governing invocation rule, complete subjects, independently accepted actual authority/supply, original 39-check execution and independent TQ1 examiner/reviewer; local fixtures confer none |
| Customer11 accepted bounded reconciliation | Existing accepted new-lineage input/output/capture, writer and APPLY/idempotency receipts; all seven pins below match; both execution evidence manifests freshly verify in full | **Reused PASS** | None for that closed component. Historical interrupted custody/UNKNOWN facts remain disclosed; no wider population or production authority follows |
| Representative population and anomalies | B6 `representative-snapshot-specification.md` at `3148482…`: one authorized Tenant, complete `ALL_TENANT_AQGREEN_PARTICIPATIONS`, accepted D03B roots, three distinct D09 populations | BLOCKED | No accepted complete sanitized representative snapshot/BASE_SNAPSHOT package supplied; synthetic exports and Customer11 cannot substitute |
| Final reconciliation analysis | Existing B6 `VerifyFinalAsync`/FINAL_ANALYSIS requirements, preserved branch specification and current operations runbook | BLOCKED | Complete population/anomaly/structural/graduation/weekly-sales impact, accepted exceptions and final manifest, operational verification and separately authorized applied comparison |
| Historical authority | B6 branch register section Q records owner D03B/D09 decisions; current main still carries older unresolved labels. Customer11 preserves UNKNOWN approval/acquisition/structural facts | BLOCKED for whole population | Primary decision/custody corroboration and per-component dispositions; do not infer a new confirmation from this record or bulk-copy stale policy labels |
| Discovery-package chronology integrity | Before-fix: eight invalid V1/V2 packages accepted, two missing boundaries null-referenced. After-fix: 18 regressions and three affected PG export/review cases pass | **PASS — local technical qualification** | Independent review/CI of this exact change remains separate; discovery is not operational FINAL_ANALYSIS |
| B6 final independent acceptance | No complete qualifying package or independent final receipt available | BLOCKED | B6 custodian supplies exact governed package; authorized independent technical reviewer and owner/Finance accept their respective complete evidence/scope |
| B7/D10 implementation | Existing completed offline plans, exact ancestor commits above; 22 D10 binding cases freshly pass. The existing 14 D10 PG results in PR106 are carried forward for unchanged D10 code, not re-executed here | **PASS — bounded offline engineering** | Actual B6/authority/scope/provider/recovery prerequisites are BLOCKED; this is not B7 completion |
| Production business commissioning | Existing operations-runbook commissioning boundary and PR106 B2 | BLOCKED | Accepted full B6 chain, actual Tenant/Area/effective/capability inputs, backup/recovery and independently verified release/provider/worker authority |

### Customer11 exact reused identities

These files remain in restricted existing custody; no raw data is published.

| Subject | SHA-256 |
| --- | --- |
| Accepted input | `74a8e8bedd14d151c119173e44671c7ec8fd8c03ebb75cbd0763974c6dd3e416` |
| Accepted output | `a1bcf510b41fa97e449e0dcb938d2b492ad08f215f6d4e5d75ff117f8b6af11d` |
| Accepted capture | `b84119e66c886ac0086b9f531186d7345a007359df465ac945afe033787e2494` |
| Frozen writer archive | `8cf553cb8fde152754aa874cfc17ebb9160f6a9c0467c11b6b33cfa663ddfd4d` |
| First APPLY receipt | `491f520890509c04165ed8a0d02bfd8e72563a3e05e838d619bcfd7f65a1653d` |
| Exact-retry receipt | `142ec9f7cd4f3211bf61b7738ba3dbc413c28ba1314c42dcf582f31fb3e1f6e4` |
| Idempotency evidence manifest | `dda4526861406527f55c0813d06cca68857704a87d13596cc43ccfc5ee32fdfd` |

Historical adapter63/0/0, writer114/0/0, dry-run, one-scope/six-placement APPLY
and zero-durable-delta retry are carried forward accepted bounded evidence. No
artifact was rebuilt, database contacted or acceptance receipt manufactured.

## B6 → B7/D10 contract trace

The current implementation consumes independently verified evidence, not a raw
discovery export or an arbitrary agent PASS:

1. `AQGreenD10OperatorEvidenceSource` reads three separately pinned, bounded
   (32 MiB each), restricted non-Git files: decision, verified authority and
   operational B6 final manifest. Missing/changed bytes, duplicates and invalid
   custody fail closed. The upstream B6 verifier remains the custodian's duty.
2. `AQGreenD10Commissioning.BindVerified` binds whole-Tenant scope, current authorized
   Areas, canonical weekly UTC boundary, three population sets/digests, native
   identity mapping, accepted components and complete applied canonical placements.
   Quarantine, partial population and missing capabilities/authority deny selection.
3. `AQGreenD10Runtime` rechecks database/schema/runtime/configuration and the full
   live same-Tenant Area envelope. Payment verification still stops at awaiting
   approval; existing authorized Area approval owns activation and atomic placement.
   Sponsor credit remains distinct from placement; AQGreen is not Onyx.
4. Existing approval/allocator/ledger transactions, uniqueness and exact-retry paths
   own writes. Pre-effective V1 financial history remains V1; incompatible
   post-boundary ledgers deny commissioning without rewrite. D10 binding creates no
   payment, participation, placement or commission by itself.
5. Missing/expired authority after V2 state exists requires suspension/exception
   review, not silent V1 fallback. Recovery is one-way for the process; restart and
   compatible restore/forward remediation need existing separate authority. No EF
   Down may destroy historical financial evidence. Actual backup/key recovery is
   still an Ops prerequisite, not established by synthetic restores.
6. Worker authority is separate; monthly automation is unsupported. Provider
   acceptance and business commissioning do not follow from ordinary deployment.

This is code inspection plus the bounded fresh tests below and explicitly
carried-forward evidence, not a newly executed production or full D10 workflow.

## Fresh verification and reproduction

- Release solution restore/build: PASS, zero errors; three existing build warnings.
  Existing transitive dependency advisories remain; package/project/lock bytes are
  unchanged. No clean dependency-audit claim is made.
- Initial regression variant: **FAIL expected**, 10 failed / 8 passed / 0 skipped.
  Final focused run: **40 passed / 0 failed / 0 skipped** (18 new + 22 existing D10).
- PostgreSQL16.15: **3 passed / 0 failed / 0 skipped**, model-created disposable
  schema, explicit Development/test-money settings and loopback-only owned
  container; removed after execution. No migration or production equivalence claim.
- Raw TRX/build/audit evidence is retained locally under
  `/tmp/opencode/b6-closure-20261017-{before,after}/`; hashes and exact changed-source
  identities are in evidence.json. Test counts were reconciled to unique TRX test
  identities/outcomes. Full hosted CI and independent acceptance are not inferred.

From `AqualLifeStyle/9.4.2/aspnet-core`, reproduce focused validation with:

```bash
dotnet restore AqualLifeStyle.sln
dotnet build AqualLifeStyle.sln --configuration Release --no-restore
dotnet test test/AqualLifeStyle.Tests/AqualLifeStyle.Tests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~AQGreenReconciliationEvidencePackageTests|FullyQualifiedName~AQGreenD10CommissioningTests'
```

The affected web filter is
`FullyQualifiedName~Export_CoversPaginationAndOverlaps_RepeatsCanonicalContent_WithoutWritesOrSensitiveFields|FullyQualifiedName~Export_WholeTenantInventory_SupportsOfflineCompleteScopeSimulationAndRejectsTampering|FullyQualifiedName~ReviewBinding_ValidAcceptance_ExactReplay_DeterministicReceipt_WithoutBusinessWrites`.
Use only an owned disposable PostgreSQL database with `REPRO_PG=true`,
`REPRO_PG_SCHEMA_FROM_MODEL=true`, `REPRO_TRANSACTIONAL=true`,
`ASPNETCORE_ENVIRONMENT=Development` and both `REPRO_PG_CONNECTION` and
`ConnectionStrings__Default` bound to that loopback test database. The existing
fixture creates its schema; these are synthetic witnesses, not human acceptance.

## Smallest next action and exact authority dependencies

**B6 evidence custodian:** supply the retained exact parent/AS1/SS1/AR1/TQ1
subjects and original freeze receipts, including the PB1 stop record. Verify them
against the already recorded manifest identities before resuming dependent work.
If PB1's invocation rule is still undefined, the **business owner/contract owner**
must accept a compatible exact clarification; this task does not create that rule.

In parallel, the **data owner and authorized snapshot custodian** can deliver the
existing-specification complete sanitized representative package and source-bound
root dispositions. Analysis can then run without renewed production access or
Customer11 repetition. The **independent TQ1 technical examiner/reviewer** must
provide original qualification/witness evidence; an implementation agent cannot
self-accept it. **Owner/Finance/Ops** must separately accept final population,
exceptions, applied reconciliation, D10 scope/boundary, recovery and chosen
provider/worker capability evidence. These are existing commissioning gates.
