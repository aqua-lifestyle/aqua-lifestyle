---
name: verify-workflow
description: Verify an Aqua multi-actor product workflow from entry to terminal business outcome. Use for workflow-changing features, onboarding, payments, approvals, member/admin journeys, state transitions, epic or release acceptance, and explicit end-to-end review; do not use for an isolated pure calculator, refactor, or static implementation task.
---

# Verify a workflow

This is a conditional product-level review. Do not require an isolated branch to
re-prove the entire product when it neither changes nor claims workflow behavior.

## Workflow review

1. Define the beginning, terminal business outcome, participating actors, required
   decisions, and explicit exclusions.
2. Reconstruct the workflow independently; treat an implementation report as input,
   not proof of completion.
3. Map each actor's entry point, information, discoverable next action, authorization,
   and outcome when they do nothing.
4. For every relevant transition, determine whether it has an owner, reachable state,
   authorization, persistence, recoverability, and evidence. When transaction,
   concurrency, idempotency, retry, migration, or receipt mechanics are material,
   compose with `verify-stateful-change` instead of duplicating its procedure.
5. Check unavailable, duplicated, delayed, interrupted, retried, out-of-order, and
   resumed paths where relevant. Verify that email or another notification is an
   attention mechanism rather than the sole authoritative workflow.
   Include browser refresh, return-later, direct URL, and multiple-tab cases when the
   affected journey makes them plausible.
6. Trace frontend, API, application, domain, persistence, worker, provider, and
   operator/reconciliation boundaries involved in the journey.
7. Determine whether first-time members and administrators can discover and complete
   their responsibilities without developer intervention.
8. Validate with proportionate browser/API E2E or integration evidence. Inspection
   alone cannot prove workflow completion.

Use [repository validation](../../../docs/development/validation.md) for commands and
[the review benchmark](../../../docs/review-benchmark.md) for finding quality.

An isolated rollback, lock, or retry question belongs to `verify-stateful-change`,
not this product-level workflow. Compose both only when actor-level completion and
durable transition mechanics are simultaneously in scope.

## Independent verdicts

Report separately:

- `IMPLEMENTATION`: meets, partially meets, or does not meet purpose;
- `WORKFLOW`: complete, partially complete, or incomplete;
- `EVIDENCE`: E2E, integration, automated component, inspection, inference, or
  unknown.

Identify every branch-owned blocker, accepted debt, product decision, and outside-
scope gap. Implementation success must not be promoted into workflow completion.
