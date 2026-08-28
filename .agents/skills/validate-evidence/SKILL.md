---
name: validate-evidence
description: Select proportionate Aqua validation and assess test, commit, PR, CI, release, or completion claims without overclaiming. Use when asked to test, verify, determine readiness, or report implementation evidence; do not use merely to explain existing code.
---

# Validate evidence

Use [repository validation](../../../docs/development/validation.md) as the canonical
command source. Inspect current build configuration if a needed command is absent;
do not copy the command catalog into this skill.

This skill owns validation execution and readiness evidence, not an independent
semantic/specification review. If correctness has already been independently
reviewed, run the required checks without replaying `review-change`. If an unreviewed
branch is asked to be commit-ready and both correctness and evidence are unknown,
compose `review-change` with this skill.

## Process

1. State the exact claim and acceptance criteria being validated.
2. Map failure impact to proportionate evidence. Reuse established infrastructure
   evidence only when the change does not alter its assumptions.
3. Select the smallest checks that establish the claim, including negative,
   duplicate, concurrent, rollback, migration, or workflow paths only when relevant.
4. Run against current artifacts; identify stale builds, carried-forward results,
   unavailable infrastructure, skips, warnings, and failures.
5. Inspect the final diff, staged state, and relevant untracked files. Confirm the
   evidence corresponds to the exact commit/worktree under review, and check for
   debug artifacts, generated output, accidental edits, and exposed secrets.
6. Report only the strongest status directly supported.

## Evidence categories

Distinguish:

- unit/component tests;
- integration tests;
- PostgreSQL execution;
- provider acceptance or delivery;
- browser/API end-to-end execution;
- code/configuration inspection;
- inference;
- repository configuration;
- deployment;
- production verification.

Focused, SQLite, mock, inspection, or repository tests cannot be promoted into
PostgreSQL, provider, deployment, workflow, or production claims.

For intermittent failures, require repeated evidence proportionate to the suspected
failure mode rather than treating one passing rerun as resolution.

Use exactly `IMPLEMENTED`, `TESTED`, `INTEGRATED`, `MERGE READY`, `MERGED`,
`DEPLOYED`, `ENABLED`, and `PRODUCTION VERIFIED`; no status implies the next. Report
each check as passed, failed, skipped, not run, unavailable, or carried forward at an
exact commit. Tests prove only what they execute.

Conclude with the claim, evidence strength, confidence, blockers, unavailable checks,
and Git state. Do not call a change `MERGE READY` when required evidence is missing.
Use the repository evidence vocabulary when source distinctions materially affect
the readiness claim.
