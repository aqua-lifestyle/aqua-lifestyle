# Exact-manifest customer-11 topology backfill

## Current new-lineage contract (2026-10-03)

The explicit owner **NEW-LINEAGE PLACEMENT IDENTITIES + WRITER REBINDING IMPLEMENTATION** decision governs this successor. Its authority is implementation and local/disposable PostgreSQL validation only: no commit, provider access, recovery-clone execution, production execution or V2 activation. The sections below headed Historical describe earlier artifacts and must not be used as current execution instructions. Interrupted custody of the lost artifacts is not restored by this successor.

Current protected inputs, verified before opening a database connection:

| Artifact | Size | SHA-256 |
| --- | --- | --- |
| New candidate manifest | 20,131 bytes | `74a8e8bedd14d151c119173e44671c7ec8fd8c03ebb75cbd0763974c6dd3e416` |
| New canonical output | 24,489 bytes | `a1bcf510b41fa97e449e0dcb938d2b492ad08f215f6d4e5d75ff117f8b6af11d` |
| New recovery-clone capture | 27,429 bytes | `b84119e66c886ac0086b9f531186d7345a007359df465ac945afe033787e2494` |

The seven capture records are required in order: DatabaseIdentity, DatabaseUser, ServerVersionNumber and ReadOnlyMode guards; identity; bounded_evidence; bounded_supplement. Guard values/results, recovery origin, repeatable-read/read-only observation, population/evidence counts, absence of corrections/conflicts, and supplemental Tenant state are validated. This is recovery-clone evidence, not an original production capture. Complete protected capture bytes remain digest-bound; no two-document projection replaces them.

Scope `06a452ea-9958-5911-8c5b-5d350e516095`, effective time `2026-10-02T05:37:10Z` and topology remain unchanged. Effective time is separate from the fresh observation `2026-10-03T07:17:10.764589Z`. All six V1 structural levels are null/UNKNOWN with Ambiguous impacts. Customer 11 historical administrator approval, acquisition/invitation/confirmation facts retain their explicit UNKNOWN dispositions. Payment completion denotes retained Aqua finality, not bank settlement.

| Customer | Accepted storage ID | Parent / slot |
| --- | --- | --- |
| 11 | `c36485ad-65c2-c44e-54a8-abbba3b91cfa` | root |
| 14 | `f5110269-2d6d-47a7-4e8a-dc03b9120cc6` | 11 / 1 |
| 15 | `1f8f57f6-1b42-ee1e-1ddc-d1d92966a0c6` | 11 / 2 |
| 17 | `08280e99-67e9-4e4d-4359-240f02eba052` | 11 / 3 |
| 19 | `f003f647-9d57-b259-5656-67bf91231662` | 11 / 4 |
| 18 | `1d251e09-d29a-d47f-afb6-950aa3ef6634` | 11 / 5 |

Only an absent, conflict-free baseline permits one scope plus six inserts. Existing OLD/arbitrary/mixed IDs fail closed even when topology matches. Exact NEW ID, participant, Tenant, scope, parent, slot, path, rules and timestamp equality is required for zero-change retry. Prospective primary-key collisions anywhere are included in pre-write conflict checks. No automatic replacement, translation of financial evidence references, deletion, recruiter rewrite or attribution/confirmation creation occurs.

### Target guard and future clone verification

CLI and direct writer use the same pre-connection target guard. Production service `dpg-d9f0jne1a83c73d8m780-a` and database `aqualifestyle` are denied independently of evidence origin. Local mode accepts only explicit loopback, `customer11_test_*` databases and user `fixture`, without clone options. Recovery mode requires service `dpg-davoaabncjis73f56rj0-a`, database `aqualifestyle_1t0a`, user `aqualifestyle`, numeric version `180006`, a separately verified exact endpoint, TLS VerifyFull and no multi-host/alias/connection-options substitution. The writer checks actual database/user/internal address/port, version and trigger execution mode inside the transaction.

Before any separately authorised future clone execution, independently verify the provider endpoint and isolated recovery identity. Store a protected UTF-8 JSON verification receipt. The CLI requires both `--verified-target-receipt FILE` and `--verified-target-reference SHA256` plus `--expected-service-id ID`; a hash string alone is refused. The receipt binds these exact fields:

`verification` = `OWNER_AUTHORISED_INDEPENDENT_TARGET_VERIFICATION`; `targetKind` = `verified-recovery-clone`; `serviceId`, `database`, `user`, `endpoint`, `externalPort`, `serverAddress`, `serverPort`, `serverVersionNumber`, `inputSha256`, `outputSha256`, `captureSha256`, `scopeId`, `pitrSourceServiceId` = original production service, `pitrCutoffUtc` = `2026-10-02T05:37:10Z`, `productionServiceId`, `productionDatabase`, `isolatedFromProduction` = true.

The future owner must accept the independently verified receipt bytes/hash and execution context. The tool checks integrity and consistency of that receipt; it cannot prove provider provenance or human authority merely because an operator writes a matching JSON file. No exact endpoint has been invented or authorised by this implementation. No real Render target is contacted in local tests; structurally shaped synthetic hosts are used only in non-connecting guard tests.

### Current build/validation

From the backend directory, restore/build the dedicated writer test project, then run its complete suite with the three protected NEW input paths supplied through `AQGREEN_CUSTOMER11_ACCEPTED_INPUT`, `AQGREEN_CUSTOMER11_ACCEPTED_OUTPUT` and `AQGREEN_CUSTOMER11_ACCEPTED_CAPTURE`. The fixture pins the existing PostgreSQL 18.6 image, binds loopback and owns/removes only its disposable databases/container. It verifies the observed numeric version and seeds synthetic contact/provider details. No schema/domain algorithm changes are required. Historical 59-test evidence does not establish this successor's result; the current evidence receipt records exact executed totals, commands, dependency state, source/build hashes and successor artifact SHA.

Current successor validation: 114 passed, 0 failed, 0 skipped on disposable PostgreSQL 18.6 (180006), including all preceding 59 cases and 55 new cases. Release build passed with three existing Core warnings. Exact first-apply/retry, protected-table equality, conflict/key collision refusal, rollback and concurrency passed. Inherited dependency advisories are recorded separately; no production qualification or 18.4 compatibility is claimed. The source remains uncommitted pending owner source-freeze review.

For a future separately authorised disposable command, use the existing flags with NEW input paths and `--source-snapshot-sha256 b84119e66c886ac0086b9f531186d7345a007359df465ac945afe033787e2494`. Recovery-mode invocation additionally needs the exact service and receipt flags above. Neither command is authority to execute against a preserved recovery clone. A successor archive has a new identity; it is not historical writer-2 and is not continuity of the lost custody chain.

## Historical writer lineage — retained record, superseded bindings

The following records describe the earlier writer and its original acceptance. Their hashes/commands are historical, not the governing bindings of the rebound successor.

## Authority and input files

Keep all input files protected and outside source control:

- accepted input: 16,351 bytes, SHA-256 `57529d67d4320595c90e2dedaf492d97c42fede1febd22caea014d0fc16f257c`;
- accepted canonical output: SHA-256 `46a7ae8a01b28b4c8896f1f778f704e6673256164b3825c1a052f1d113430978`;
- retained bounded source capture: SHA-256 `ac89f43cbe89959b7da690c836c33f6365593476cb687db1f29c059f04b25c8e`, bound by the accepted input manifest.

The fixed effective time and scope identity are loaded from those exact bytes. No editable Tenant, programme, population, sponsor, slot, parent or rules override exists. Changed input/output/capture bytes fail before opening a database connection.

`LegacyCanonicalisation` provenance is supplied by the accepted manifest and operator result. The existing schema has no PlacementSource/manifest-digest column; this tool does not invent one or generate a schema migration. Archive the accepted input/output, owner acceptance and execution result with every separately authorised execution. Existing placement rows retain their accepted effective timestamp and existing rules version. No historical topology, historical approval, acquisition source, invitation/confirmation, bank settlement or financial-cutover claim is added.

## Target verification and authority

This continuation authorises implementation and disposable local PostgreSQL validation only. Do not use the tool against production or create/access a Render clone under this authority. Recovery clone creation, provider identity verification and actual clone execution require separate owner authority.

Before any future clone command, independently establish its provider service/endpoint identity, recovery-point/source lineage, exact database/user/server address/internal port, accepted source snapshot relationship and owner execution authority. `--verified-target-reference` records the SHA-256 of that separate verification/authority receipt; a supplied digest does not itself prove a clone or grant permission. The tool proves PostgreSQL identity against the operator's expected values and compares the accepted retained source facts; it cannot prove provider lineage from database contents alone. Production endpoint strings containing the accepted source service identifier are refused. This is an additional guard, not a substitute for independent target verification.

Connection credentials must be supplied through `AQGREEN_CUSTOMER11_BACKFILL_DSN`, not command arguments or logs. Example commands use placeholders only. `--expected-server-port` is the PostgreSQL server's internal port, not a Docker host mapping. The tool emits identifiers/digests/delta only and suppresses PostgreSQL server detail.

## Build and operator commands

From `AqualLifeStyle/9.4.2/aspnet-core`:

```bash
dotnet build tools/AqualLifeStyle.Customer11Backfill/AqualLifeStyle.Customer11Backfill.csproj --configuration Release
dotnet tools/AqualLifeStyle.Customer11Backfill/bin/Release/net8.0/AqualLifeStyle.Customer11Backfill.dll --help
```

Populate the connection environment securely outside command history. For an authorised **local disposable fixture**, inventory first:

```bash
dotnet tools/AqualLifeStyle.Customer11Backfill/bin/Release/net8.0/AqualLifeStyle.Customer11Backfill.dll \
  --mode dry-run \
  --manifest '<protected accepted input>' \
  --output '<protected accepted canonical output>' \
  --source-capture '<protected original bounded capture>' \
  --target-kind local-fixture \
  --expected-database '<verified disposable database>' \
  --expected-user '<verified fixture user>' \
  --expected-server-address '<verified server IP>' \
  --expected-server-port '<verified internal port>' \
  --source-snapshot-sha256 ac89f43cbe89959b7da690c836c33f6365593476cb687db1f29c059f04b25c8e
```

For a separately authorised, verified recovery clone, use `--target-kind verified-recovery-clone` and additionally `--verified-target-reference '<separate verification/authority receipt SHA-256>'`. These flags do not enable V2, create a clone or repoint an application. Apply uses the same fully verified arguments with `--mode apply`; **do not execute it under implementation-only authority against production or a production-derived clone**.

Expected empty-baseline dry-run: ScopeInserts=1, PlacementInserts=6, Conflicts=0, UnexpectedExistingRows=0, Updates=0, Deletes=0, AttributionInserts=0, ConfirmationInserts=0; DryRun=true. Dry-run acquires protection, reads the relevant source/state and rolls back without inserting rows.

Expected first local apply: one scope/six exact placements, AlreadyApplied=false. Exact retry: zero inserts/semantic changes, AlreadyApplied=true. A valid retry neither allocates a new vacancy nor changes storage keys. Partial rows, other-scope placements, extra scope participants or any conflicting fields fail rather than being repaired. After a new recruit has legitimately extended the scope, this exact six-row baseline tool rejects additional rows; it is not an ongoing maintenance/reflow tool.

## Transaction and source protection

A fresh context owns one PostgreSQL READ COMMITTED transaction. Expected database/user/server identity and original snapshot binding are checked. The existing placement allocator's transaction-scoped advisory key is acquired. Short-lived SHARE locks freeze source/correction/approval/payment/customer/actor facts; SHARE ROW EXCLUSIVE locks serialize topology DML. All conflicts and required schema invariants are re-read inside the protected transaction. Local statement/lock timeouts bound waiting.

Checks include physical public-table visibility without RLS, valid Tenant-qualified restrictive foreign keys and keys, valid unique root/participant/slot indexes, original check expressions (including logical grouping), enabled original topology triggers, original trigger function bodies and deferred one-root enforcement. No schema repair occurs.

The target must retain the accepted six identities, Active state, source timestamps, recruiter/no-correction facts and original payment/checkout/receipt/customer-user/approval-record/approving-actor identities. Payment purpose/amount/currency/finality and retained checkout/receipt bindings are checked. The root remains without a retained approval row; its historical administrator approval is UNKNOWN. Five descendants require their original positive decision and actor reference. Unknown historical permission/acquisition facts are not manufactured.

Only domain scope/placement entities are added. Scope identity is accepted; deterministic placement row storage IDs derive from SHA-256 of `AQGreenCustomer11Backfill:<accepted input SHA>:<scope UUID>:<participant UUID>`, first 16 bytes as a big-endian Guid. These storage keys are engineering identity, not new placement authority. Root and children use existing domain factories with accepted parent/slot/path/effective time. Any exception, cancellation, constraint or deferred-commit failure rolls back the whole transaction. No participant, recruiter, attribution, confirmation, approval or financial history is written.

## Receipt predicate correction and diagnostics

The owner-authorised successor corrects the lowercase-only payload-hash restriction in writer commit `952f4835949e791586307845e36aa06672d3cc5e`. A retained hash must still be non-null, exactly 64 characters and entirely ASCII `0-9`, `a-f` or `A-F`. No trimming or stored-value mutation occurs. Receipt identities, Tenant/programme, exact processing time, checkout/payment reference equality and event presence retain their existing comparison semantics.

Receipt rejection reports the first failed category in evaluation order: `ReceiptCount`, `ReceiptId`, `ProcessedAt`, `Tenant`, `Programme`, `ProviderCheckoutBinding`, `PaymentBinding`, `EventPresence`, `PayloadHashNull`, `PayloadHashLength`, or `PayloadHashCharacters`. The operator CLI retains `REJECTED`, `CONTRACT_CONFLICT` and a null delta, with only the category in the reason. No raw provider references, event identifiers, payload hashes, payload contents or credentials are emitted.

The original recovery-clone dry-run rejected with an aggregate receipt-binding message; its first failed term remains UNKNOWN. This correction addresses a separately proven source defect and does not retroactively establish the original runtime cause or a successful clone execution. Accepted manifest/output/capture bytes remain unchanged. A successor artifact and a new source commit are required; the old artifact remains historical rejected evidence. This correction continuation authorises only disposable local validation and a bounded commit, with no push or Render/clone/production execution.

## Dedicated PostgreSQL validation

This dedicated operator test project is intentionally not part of ordinary API startup and requires protected accepted artifacts. It seeds a disposable fixture with accepted identifiers but synthetic names/contact/provider values; it does not copy production PII or real provider secrets. No input files are committed.

```bash
AQGREEN_CUSTOMER11_ACCEPTED_INPUT='<protected accepted input>' \
AQGREEN_CUSTOMER11_ACCEPTED_OUTPUT='<protected accepted output>' \
AQGREEN_CUSTOMER11_ACCEPTED_CAPTURE='<protected original bounded capture>' \
dotnet test tools/AqualLifeStyle.Customer11Backfill.Tests/AqualLifeStyle.Customer11Backfill.Tests.csproj \
  --configuration Release \
  --logger 'trx;LogFileName=customer11-writer.trx' \
  --results-directory '<protected evidence directory>'
```

Docker is required. The fixture pins PostgreSQL 18.6 at `postgres@sha256:77f585114c32fbca283dc835b0596f4e52b51b4c6662d7810b2f4084f60a1873`. PostgreSQL 18.4 matching the earlier production observation could not be fetched during validation because Docker Hub timed out; no 18.4 execution is claimed. Only uniquely named `aqgreen-customer11-writer-*` fixtures are created; ports bind loopback; fixture teardown removes those owned containers/databases. Tests cover first apply/retry/dry-run, actual PostgreSQL rollback after partial writes, concurrent duplicate applies, conflicting/foreign/partial/extra state, wrong hashes/target/snapshot/source/payment/approval/actor identities, missing/weakened constraints/indexes/triggers/RLS visibility, no changes to all non-topology tables, CLI execution and current EF model agreement. This evidence does not establish production apply, provider recovery lineage or live new-recruit spillover.

The successor fixture obtains baseline hashes through `YocoWebhookReceipt.Record`, exercising its uppercase storage representation throughout the suite. Dedicated cases cover uppercase/lowercase/mixed-case success, malformed ASCII and non-ASCII rejection, each receipt-binding category and CLI redaction. Malformed-length inputs use a disposable fixture text column to avoid PostgreSQL `char(64)` padding; topology constraints remain intact. Because the real receipt column forbids NULL, the defensive null-hash category is tested through an EF materialization interceptor without relaxing that constraint or changing stored rows. These fixture mechanisms are not production schema changes or evidence repairs.

## Recovery

On failure, PostgreSQL transaction rollback is the recovery action; do not delete or update append-only topology. On successful future execution, preserve the exact result and verify rows against the accepted output. Any later mismatch requires owner reconciliation/verified snapshot restoration under separate authority, not an automatic Down migration, topology repair or reflow.
