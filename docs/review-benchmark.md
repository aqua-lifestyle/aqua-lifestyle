# Aqua Lifestyle review benchmark

**Status:** Active

**Established:** 2026-07-29

**Applies to:** All committed and uncommitted changes in this repository

## Purpose

This benchmark defines what Aqua Lifestyle considers a production-quality engineering review. It combines the strongest validated practices from human review, repository-aware AI review, static analysis, testing, and operational verification without making any external reviewer the source of truth.

The objectives are to:

- detect correctness, security, privacy, concurrency, integration, and operational defects before merge;
- verify implementation against current business rules and customer journeys;
- produce high-signal, evidence-based findings instead of speculative or cosmetic noise;
- review the complete impact of a change rather than only its edited lines;
- learn from missed findings and convert reusable lessons into enforceable protection.

The enforceable summary lives in the repository root `AGENTS.md`. This document explains how to apply and evolve it.

## Source principles

The benchmark incorporates these independently useful approaches:

- **Repository and history context:** understand the complete codebase, current requirements, related decisions, and relevant review history before assessing a diff.
- **Specialised review passes:** examine a change through separate correctness, security, reliability, persistence, UX, maintainability, and testing lenses.
- **Candidate verification:** verify suspected defects against actual code behaviour and protections elsewhere in the stack before reporting them.
- **Code-relationship analysis:** trace call sites, dependencies, contracts, state transitions, and downstream consumers outside the edited files.
- **Tool-assisted evidence:** use compilers, tests, linters, dependency audits, security scanners, migration checks, and builds to establish facts.
- **Governed rules:** maintain explicit repository standards, reconcile conflicts, and revise them as validated requirements change.
- **Continuous learning:** use accepted and rejected findings as evidence while preventing historical precedent from overriding current security or business rules.

## Sources of truth

Review decisions use this precedence:

1. The user's current, explicit business decisions and acceptance criteria.
2. Current approved business documentation and architectural decisions.
3. Repository instructions, security boundaries, and established domain invariants.
4. Current implementation contracts and tests, where they do not conflict with higher authority.
5. Relevant historical decisions and accepted review findings.
6. General engineering best practices and external reviewer suggestions.

External tools provide candidates and evidence; they do not establish business truth. A suggestion that conflicts with a confirmed rule, weakens security, or does not apply to the current code must be rejected with a concise reason.

## Review workflow

### 1. Establish scope and intent

- Read the request, acceptance criteria, current business documents, relevant ADRs, and scoped instruction files.
- Inspect the branch, working tree, staged changes, untracked files, and complete diff.
- Identify user-authored or unrelated work that must be preserved.
- State unresolved assumptions only when they materially affect the outcome.

### 2. Build an impact map

Identify affected journeys and trace their contracts through relevant layers:

```text
Customer or administrator action
            ↓
Frontend state and validation
            ↓
API contract and authorization
            ↓
Application orchestration
            ↓
Domain invariants and state transitions
            ↓
Persistence, external providers, and background work
            ↓
Audit, observability, recovery, and customer feedback
```

Review unchanged code when it participates in the affected journey. A correct local edit can still violate a caller, database constraint, retry worker, deployment setting, or UI assumption.

### 3. Perform specialised passes

Run these eight passes independently:

1. Intent and scope
2. Correctness and contracts
3. Security and privacy
4. Reliability and concurrency
5. Persistence and operations
6. User experience and accessibility
7. Maintainability and performance
8. Tests and evidence

For every behavioural change, trace one complete success path and one complete failure path, including nearby unchanged code involved in those flows. For high-risk authentication, authorization, payment, tenant isolation, migration, or distributed-processing changes, also trace at least:

- one successful journey;
- one validation or authorization rejection;
- one dependency or persistence failure;
- one duplicate or concurrent request;
- one retry, recovery, or rollback path where applicable.

### 4. Perform an adversarial second pass

After the primary review, challenge its assumptions:

- What input, response, claim, callback, or stored state was trusted without proof?
- What happens between validation and persistence when two instances act concurrently?
- Can a partial failure leave state that appears successful?
- Can another tenant, Area, programme, or customer observe or mutate this state?
- Does a retry repeat an external side effect?
- Does production configuration differ from the test path?
- Can a null, empty collection, stale record, or unsupported enum value bypass the intended rule?
- Did the fix protect every call site or only the reported example?

## Finding model

Every material finding should include:

- **Classification:** introduced regression, pre-existing risk, requirement gap, or improvement.
- **Severity:** critical, high, medium, or low, based on impact and likelihood.
- **Invariant:** the requirement, security boundary, or expected behaviour being violated.
- **Scenario:** a concrete path that demonstrates the failure.
- **Evidence:** relevant code location, test, command output, provider contract, or approved documentation.
- **Scope:** affected users, tenants, programmes, data, integrations, or deployments.
- **Correction:** the smallest safe change that addresses the root cause.
- **Validation:** the test or check that proves the correction and guards against regression.

Do not create multiple findings for symptoms with one root cause. Do not report a possibility as a fact when the failure path has not been established. Do not attribute a pre-existing problem to the current change.

After fixes, repeat the affected review passes and inspect the complete resulting diff. Confirm the correction covers every affected call site, does not weaken another boundary, introduces no unrelated change, and has current validation evidence.

### Severity guidance

| Severity | Meaning |
| --- | --- |
| Critical | Likely financial loss, unauthorized access, severe privacy exposure, or irreversible production data corruption. |
| High | Probable incorrect business state, payment inconsistency, tenant boundary violation, deployment failure, or another material production defect. |
| Medium | Bounded functional failure with a practical workaround or limited scope; should normally be corrected before completion. |
| Low | Limited-impact maintainability, usability, accessibility, or operational improvement supported by evidence. |

Formatting preferences and speculative refactors are not material findings unless they violate an explicit repository rule or create a demonstrated risk.

## Validation evidence

Validation must be proportional to risk and reported exactly. The normal sequence is:

1. Focused regression tests for the changed behaviour.
2. Tests for negative, duplicate, concurrent, and rollback paths where relevant.
3. Type checking, compilation, linting, formatting, and dependency/security analysis.
4. Relevant integration suites and production/Release builds.
5. Migration application and model/snapshot verification when persistence changes.
6. Final diff, untracked-file, generated-file, configuration, and secret review.

A command that was skipped, failed, used stale build output, or required unavailable infrastructure must be reported as such. Warnings must be distinguished from errors and classified as introduced or pre-existing.

## Continuous-learning loop

When an internal or external reviewer, CI, production telemetry, or a user journey reveals a missed issue:

1. Verify the finding against the current code and reproduce or prove the failure path.
2. Identify why the review missed it: incomplete context, weak contract tracing, absent negative case, concurrency blind spot, unclear rule, missing tool, or incorrect assumption.
3. Generalise the defect class without creating an overly broad or vendor-specific rule.
4. Choose the strongest practical prevention, preferring this order:
   - domain or database invariant;
   - automated regression test;
   - compiler, analyzer, linter, scanner, or CI check;
   - reusable implementation pattern;
   - repository review instruction.
5. Validate the prevention against the discovered example and check that it does not create false positives or conflict with current architecture.
6. Update `AGENTS.md` when reviewer behaviour must change and update this document when the benchmark's rationale or process changes.
7. Consolidate duplicate guidance and remove obsolete rules.

Rejected external findings are also useful learning. Record a new rule only when the lesson is reusable and supported by evidence; do not train the benchmark to repeat an unsafe or irrelevant historical decision.

## Benchmark health

The benchmark is improving when:

- fewer material issues are first discovered after the internal review;
- review findings have reproducible evidence and a high acceptance rate;
- the same defect class does not recur after a prevention is added;
- critical journeys have focused negative and concurrency coverage;
- review time is spent on business and production risk rather than duplicated tool output;
- instructions remain consistent, scoped, and short enough to be followed.

The goal is not to maximise the number of comments or promise that no future reviewer can find anything. The goal is a continuously improving review system that finds material issues earlier, explains them precisely, and turns validated learning into durable protection.

## Completion record

At handoff, report:

- reviewed scope and affected journeys;
- material findings fixed, rejected, or still open;
- architectural and security decisions;
- tests and validation commands with results;
- migration or deployment actions;
- known limitations, warnings, and unresolved business decisions;
- whether changes were staged, committed, pushed, or left only in the working tree.
