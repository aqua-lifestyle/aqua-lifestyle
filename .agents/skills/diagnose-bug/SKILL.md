---
name: diagnose-bug
description: Establish reproducible causality for an Aqua defect or unexplained failure before patching. Use for regressions, flaky CI, runtime/provider anomalies, and conflicting behavior; do not use for a mechanical change whose cause and contract are already established.
---

# Diagnose a bug

Establish what behavior produced an unexplained symptom before changing code.
Diagnosis does not authorize a fix unless the task includes implementation. If a
reproducible causal chain is already proven and the user requests the smallest fix,
do not restart broad hypothesis generation merely to use this skill.

## Causal workflow

1. Define the symptom, expected behavior, affected scope, and a minimal reproduction.
2. Record raw `OBSERVATION`s without explanation.
3. Generate competing `HYPOTHESIS`es, including branch-introduced, branch-exposed,
   pre-existing, unrelated, and environment/tooling causes.
4. Choose the smallest discriminating experiment that can falsify more than one
   hypothesis. Prefer repository inspection or direct execution over speculation.
5. Record the result as `EXPERIMENT RESULT`; separate it from `INFERENCE`.
6. Trace the behavior through callers, contracts, state transitions, persistence,
   external boundaries, and relevant unchanged code.
7. Name a `ROOT CAUSE` only when evidence establishes the causal chain. Otherwise
   report `UNKNOWN` or `INCONCLUSIVE` and the next discriminating check.
8. If authorized to fix, implement the smallest complete correction, add regression
   evidence that fails for the proven cause, and remove temporary diagnostics.

For intermittent behavior, repeat the discriminating experiment enough to challenge
the timing or randomness hypothesis; one successful rerun does not establish a fix.

## Required labels

Use `OBSERVATION`, `INFERENCE`, `HYPOTHESIS`, `EXPERIMENT RESULT`, `ROOT CAUSE`, and
`UNKNOWN` precisely. Classify ownership as:

- `BRANCH-INTRODUCED`
- `BRANCH-EXPOSED`
- `PRE-EXISTING`
- `UNRELATED`
- `INCONCLUSIVE`

Repository-wide test-integrity rules remain active even when this skill is not used.
This workflow adds causal discipline; it is not the sole protection against weakened
tests.

When framework, provider, database, or runtime behavior may have changed, consult
primary official documentation or run a controlled direct experiment. Classify that
material as `EXTERNAL EVIDENCE` or `EXPERIMENT RESULT`, not repository fact.
Use the repository evidence vocabulary when the distinction is material, and stop
researching when authoritative evidence has discriminated the plausible causes.

For validation commands and evidence limits, use
[repository validation](../../../docs/development/validation.md).
