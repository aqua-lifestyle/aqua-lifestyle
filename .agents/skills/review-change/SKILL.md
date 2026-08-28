---
name: review-change
description: Independently review whether an Aqua branch or diff correctly implements its intended outcome and identify branch-owned semantic, security, and regression defects. Use for correctness review or audit; do not use as the primary workflow for implementation, generic test execution, CI collection, or validation-only readiness checks.
---

# Review a change

Use the [review benchmark](../../../docs/review-benchmark.md) as the canonical
methodology. Apply only the axes relevant to the change, while always applying the
semantic-integrity axis below.

## Review workflow

1. Reconstruct purpose, acceptance criteria, exclusions, governing specification,
   and unresolved decisions before judging the implementation.
2. Inspect branch, worktree, `HEAD`, status, staged/unstaged/untracked state, full
   diff, commit range, and user-owned or unrelated changes.
3. Build an impact map across callers, contracts, state transitions, persistence,
   UI/API consumers, workers, providers, and nearby unchanged code.
4. Separate implementation claims from evidence. Identify assumptions and classify
   findings as branch-introduced, branch-exposed, pre-existing, unrelated, or
   inconclusive.
5. Review authorization and security boundaries, Tenant versus Area, programme
   separation, V1/V2 compatibility, error semantics, current versus historical
   state, and regression behavior where applicable.
6. Trace every material result or DTO value back to authoritative evidence and all
   affected consumers. Check whether an apparently compatible output changes its
   meaning.
7. Inspect the existing evidence for each conclusion and state its limits. Do not
   take ownership of broad test orchestration, CI collection, or generic commit
   readiness; compose with `validate-evidence` when fresh readiness evidence is
   required.
8. Return an independent correctness verdict. Do not attribute an
   unrelated or pre-existing defect to the branch.

When an uncertain external technical claim materially affects correctness, verify it
through a direct experiment or primary official source and classify that evidence;
do not browse mechanically for settled repository behavior.

Use `FACT`, `REPOSITORY EVIDENCE`, `EXTERNAL EVIDENCE`, `EXPERIMENT RESULT`,
`INFERENCE`, `ASSUMPTION`, and `UNKNOWN` only where those distinctions materially
affect a conclusion.

## Mandatory semantic-integrity axis

Explicitly look for:

- unknown becoming `0`, `false`, `null`, an empty collection, or a default enum;
- corrupt or unsupported evidence becoming `Incomplete` or Level 0;
- selected V2 failure silently falling back to V1;
- mutable current state becoming a fabricated historical fact;
- incompatible programme, attribution, topology, structural, or financial models
  being combined;
- a DTO compatibility value fabricated because the authoritative model lacks the
  required fact;
- user-friendly error translation converting failure into success or erasing its
  semantic category.

Distinguish legitimate business incompleteness from an indeterminate result. Absence
of evidence is not evidence of zero.

## Verdict

Report purpose, scope, branch-owned findings with evidence and confidence,
implementation correctness, semantic/security/regression risks, evidence inspected,
and unknowns. Do not issue a generic commit-readiness verdict unless
`validate-evidence` supplies the required current checks.
