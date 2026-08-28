---
name: verify-stateful-change
description: Implement or inspect the durable-state mechanics of Aqua changes, including transactions, locks, uniqueness, retries, idempotency, migrations, history, receipts, and reconciliation. Use directly for already-specified stateful work or compose with correctness/workflow review; do not use for static presentation changes.
---

# Verify a stateful change

Select checks by actual risk; do not mechanically require every item. Establish the
authoritative state transition and evidence model before validating mechanics.

This is a specialist procedure, not an automatic independent review. An
already-specified transactional implementation may use it directly. Compose with
`review-change` when the request independently reviews the change's semantics or
overall correctness, and with `verify-workflow` when product actors and terminal
journey outcomes are also in scope.

## State and transaction review

- Identify invariants, owners, Tenant/Area/programme boundaries, allowed source and
  target states, durable evidence, and authoritative timestamp semantics.
- Verify database constraints and uniqueness independently of application checks.
- Establish transaction ownership, isolation, lock scope and order, external-call
  placement, rollback behavior, and what another request or instance can observe.
- Exercise concurrent duplicate and distinct actions where they can race. Verify
  idempotency keys, replay conflict detection, retry ownership, stale-work recovery,
  and terminal failure.
- Check partial failure before and after each durable boundary. Use outbox, inbox,
  durable receipt, or reconciliation only where the change's contract requires it.
- Check timeout and cancellation ownership where work can be interrupted; neither
  may leave a durable state that appears successfully completed.
- Keep current projections separate from immutable/effective-dated history. Do not
  infer an earlier fact from current state or rewrite settled evidence silently.

## Domain-sensitive checks

- For payments and webhooks, follow
  [payment and approval authority](../../../docs/aqua-system/04-payments-approval-and-yoco.md).
  Verify provider confirmation, identity/reference/purpose/amount/currency/finality,
  duplicate delivery, and the separation between payment and activation.
- For migrations and historical records, follow
  [data and migration authority](../../../docs/aqua-system/05-data-history-migrations-and-legacy-members.md).
  Review `Up`, `Down`, model/snapshot alignment, existing-data compatibility,
  deployment ordering, rollback limits, and recovery.
- For financial time and cutoff evidence, inspect the maintained temporal and
  decision documents rather than reconstructing rules from this skill.
- For provider, PostgreSQL, isolation, or runtime uncertainty, prefer a direct
  controlled experiment and primary official documentation. State what remains
  unavailable.

Use the repository evidence vocabulary when source distinctions materially affect a
decision. Stop researching once authoritative evidence or a discriminating
experiment settles the relevant state behavior.

## Evidence

Use [repository validation](../../../docs/development/validation.md). SQLite, mock,
unit, or inspection evidence cannot prove PostgreSQL locking, provider delivery,
deployment order, or production recovery. Report invariants checked, experiments,
passed/failed/unavailable paths, remaining reconciliation needs, and the exact
evidence strength.
