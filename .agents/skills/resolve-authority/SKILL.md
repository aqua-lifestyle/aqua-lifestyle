---
name: resolve-authority
description: Resolve current Aqua business or behavioral authority when semantics conflict or are uncertain. Use for AQGreen/Onyx, V1/V2, payment/activation, eligibility, unresolved decision IDs, or externally visible policy; do not use for mechanical edits with settled semantics.
---

# Resolve authority

Determine the rule that may safely govern the requested change without copying
changing business rules into this skill.

Do not load this skill merely because a mechanical change names a business entity.
Use it when the meaning, precedence, or policy is actually ambiguous or conflicting.

## Process

1. State the exact decision or interpretation needed and the consequence of getting
   it wrong.
2. Start at the [Aqua system index](../../../docs/aqua-system/README.md), then inspect
   the relevant current business document, decision/risk register, V2 specification,
   ADR, implementation, tests, history, or primary external source.
3. Apply this precedence:
   - explicit current owner authorization;
   - current confirmed business records and accepted architecture decisions;
   - repository-wide safety and domain invariants;
   - current code and tests as implementation evidence, not automatic business intent;
   - historical sources and external guidance as contextual evidence.
4. Identify stale or conflicting sources instead of silently reconciling them.
5. Classify every material claim as `FACT`, `REPOSITORY EVIDENCE`,
   `EXTERNAL EVIDENCE`, `EXPERIMENT RESULT`, `INFERENCE`, `ASSUMPTION`, or
   `UNKNOWN`.
6. Determine whether the rule is `PROPOSED`, `CONFIRMED`, `SUPERSEDED`, or still
   unresolved under the
   [authority convention](../../../docs/aqua-system/07-verification-decision-and-risk-register.md#p-business-decision-authority-convention).
7. Fail closed when unresolved policy materially affects money, authorization,
   programme state, contractual obligation, eligibility, or externally visible
   behavior. Preserve unaffected paths when the authority explicitly permits it.
8. Cite the exact authority used and record any unresolved boundary or safe
   consequence.

## Guardrails

- An agent inference, worklog, report, PR description, or agent-authored summary
  cannot establish a `CONFIRMED` decision.
- Do not use code behavior alone to overwrite confirmed intent or to settle an open
  decision.
- Do not copy current formulas or decision contents into this skill; follow the
  maintained authority documents.
- Use primary official documentation or a direct experiment when framework,
  provider, or runtime behavior is material and may have changed. Do not research
  mechanically when current repository evidence settles a routine question.
- Compose with `review-change` when a change's correctness depends on first resolving
  conflicting authority; authority resolution does not replace the correctness
  review.

## Result

Report the decision being resolved, governing authority, conflicts, evidence
classifications, remaining unknowns, and the implementation consequence. Label an
agent recommendation `PROPOSED`, never `CONFIRMED`.
