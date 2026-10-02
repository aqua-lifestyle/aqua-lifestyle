# Customer-11 bounded topology backfill writer

Owner-authorised implementation/validation workstream; not production apply, clone creation, V2 activation, D10, deployment or publication authority.

## Contract and evidence boundary

The owner accepted the exact external input hash `57529d67d4320595c90e2dedaf492d97c42fede1febd22caea014d0fc16f257c` (16,351 bytes) and output hash `46a7ae8a01b28b4c8896f1f778f704e6673256164b3825c1a052f1d113430978`. Authoritative input files contain production identities and stay outside source control. The writer loads these exact bytes; no editable topology/configuration override. Named root/component, effective timestamp and source snapshot are bound by those accepted bytes. The embedded candidate-status strings remain historical artifact text; the separate explicit owner acceptance governs their current authority.

Topology-only first delta: one scope, six placements; exact prior state: zero semantic changes. Mixed/partial/conflicting state fails closed. No attribution/confirmation, participant/recruiter, historical approval, history deletion or financial writes.

## Plan

1. Verify accepted artifacts and existing schema/lock/transaction architecture.
2. Implement immutable accepted plan in Core, transaction-owned PostgreSQL writer in EntityFrameworkCore, explicit operator CLI under tools.
3. Verify exact source facts, target identity, database invariants and conflicts under transaction/placement-tree protection.
4. Disposable PostgreSQL: exact first apply; exact retry; dry-run; conflicts; partial-failure rollback; mismatched hashes/identity/source and missing invariant rejection; concurrent retries; prohibited-write absence.
5. Record source/binary identities and bounded evidence. Stop without Render/clone/production execution.

## Worklog

2026-10-02: accepted artifact hashes and input size verified. Clean starting branch `b6/nonprivileged-attempt-account-foundation`, HEAD `8ca0dc3d78496c0a5673b5ce8df7643fbf19196a`. Existing PostgreSQL placement schema is append-only and uses a deferred exactly-one-root constraint plus Tenant-qualified foreign keys and unique slots. Source status has no persisted PlacementSource column; accepted manifest/receipt supplies LegacyCanonicalisation provenance without inventing a schema field or migration.

2026-10-02: IMPLEMENTED the immutable accepted plan in Core, transaction-owned writer/schema guard in EntityFrameworkCore and standalone operator CLI. The original protected source capture is required and digest-bound; original payment/checkout/receipt/customer-user/approval-record/actor identities and retained source facts are rechecked. Normal allocator advisory protection and source/topology relation locks surround invariant/conflict checks and writes. Exact absent baseline inserts one scope/six placements; exact retry returns zero semantic change; partial/conflicting state rejects without repair. Dry-run rolls back without DML. No HTTP/DI startup registration or feature selection change.

2026-10-02: TESTED final Release source: 35 dedicated tests passed, 0 failed, 0 skipped on disposable PostgreSQL 18.6, pinned image digest `77f585114c32fbca283dc835b0596f4e52b51b4c6662d7810b2f4084f60a1873`. Executed proofs include first apply, exact retry, dry-run, concurrent applies, actual scope/root partial-write rollback, foreign/conflicting/partial/extra topology, changed hashes/target/source/payment/approval/actor facts, missing/weakened schema invariants (including SQL-literal preservation), all non-topology-table equality, CLI dry-run/apply/retry and unchanged EF model. Original acceptance files remain byte-identical. No closed B6 foundation suites rerun: no causal implementation change to those files.

PostgreSQL 18.4 matching the earlier production observation was attempted but unavailable: Docker Hub timed out before fixture startup. Its TRX reports fixture initialization failures, not executed business assertions. No 18.4 PASS is claimed. Earlier implementation iterations and corrected compile failures are retained in evidence; the final 35-case result is the current credited result, not a sum of repeated executions.

Local framework-dependent writer artifact and validation receipt are recorded outside Git. All ten new repository paths are expected implementation/test/documentation; existing tracked files and HEAD remain unchanged; source is uncommitted. No Render access/control change, recovery clone creation/execution, production connection/mutation, V2 enablement, D10, deployment, commit or push. Clone execution and provider/source-lineage verification remain separately gated.

Strongest bounded status: CUSTOMER-11 BOUNDED BACKFILL WRITER: READY FOR CLONE EXECUTION. This is implementation/local PostgreSQL evidence, not clone, production or spillover execution evidence. The operator runbook is `docs/operations/aqgreen-v2-b6/customer11-backfill/README.md`.
