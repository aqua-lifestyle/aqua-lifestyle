# Exact-manifest customer-11 topology backfill

This is an operator tool, not an application endpoint, EF migration or V2 selector. It can insert only the topology approved by the owner's **OWNER ACCEPTANCE — CUSTOMER-11 B6 CANONICALISATION MANIFEST** instruction. That later owner acceptance governs the unchanged artifacts even though their embedded status strings still describe the original candidate stage.

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

## Recovery

On failure, PostgreSQL transaction rollback is the recovery action; do not delete or update append-only topology. On successful future execution, preserve the exact result and verify rows against the accepted output. Any later mismatch requires owner reconciliation/verified snapshot restoration under separate authority, not an automatic Down migration, topology repair or reflow.
