# Aqua Lifestyle engineering instructions

These instructions apply repository-wide. A nested `AGENTS.md` may add stricter rules for its directory but must not weaken these requirements. Detailed review methodology is in `docs/review-benchmark.md`; supported commands and scope gates are in `docs/development/validation.md`.

## Working agreement

- Before changing code, inspect the current implementation, tests, relevant business documents, applicable repository instructions, and `git status`.
- Treat the latest confirmed business decisions as authoritative. Do not invent unresolved rules.
- Stop and ask, or explicitly report uncertainty, when it materially affects money, eligibility, programme state, authorization, contractual obligations, or externally visible behaviour.
- For minor technical decisions, follow established repository patterns and disclose assumptions that materially affect the result.
- Verify reported findings against current code. Fix only findings that still apply, using the smallest complete change.
- Preserve unrelated and user-authored work. Do not commit, push, merge, delete branches, or modify external services unless explicitly requested.
- Never expose credentials, API/provider secrets, one-time account links, personal information, or production data in source, URLs, logs, tests, examples, or tool output.
- Update relevant documentation and configuration examples when behaviour or operational requirements change.

## Architecture and implementation

- Follow the existing ABP dependency direction: domain and identity foundations in `AqualLifeStyle.Core`; use-case orchestration in `AqualLifeStyle.Application`; persistence mapping and database integration in `AqualLifeStyle.EntityFrameworkCore`; HTTP/authentication concerns in the existing Web projects; migration execution in `AqualLifeStyle.Migrator`.
- Keep domain invariants and state transitions in the domain layer, orchestration in the application layer, HTTP concerns at API boundaries, and EF Core configuration and repositories in the EF Core project.
- Keep provider-specific implementations behind explicit abstractions and within established integration boundaries. Do not create a generic `Infrastructure` project unless the architecture is intentionally changed.
- Preserve aggregate, Area/tenant, authorization, programme, payment, and ownership boundaries.
- Use descriptive business terminology. Apply KISS, SOLID, DRY, and YAGNI without speculative abstractions or permissive fallback behaviour.
- Treat client input, provider callbacks, webhook payloads, external claims, and API responses as untrusted.
- Keep external calls outside long-running database transactions.
- Use an outbox when committed database state requires reliable asynchronous external delivery. Use an inbox or durable receipt when duplicate external delivery could change business state.
- Design shared-state changes for multiple requests and instances: verify atomicity, database uniqueness, idempotency, ordering, retry ownership, stale-work recovery, rollback, and terminal failure.

## Payments and transactional communication

- Apply a payment only after authoritative provider confirmation verified server-side through a valid provider signature or provider API response.
- Before changing business state, verify the merchant account, provider payment identifier, merchant reference, expected amount, currency, final provider status, payment purpose, and whether the payment was already received or applied.
- Do not activate participation, settle an obligation, release an entitlement, or make commission available before verified payment confirmation.
- Make webhook receipt and processing idempotent. Retain the provider receipt/payload hash needed to reject conflicting replays, without exposing sensitive contents.
- Distinguish mutable current-state projections from append-only history. Preserve append-only records of payment attempts, provider notifications, reconciliation actions, and material status transitions.
- Correct production payment state only through an authorised, auditable reconciliation workflow.
- Do not log or return provider secrets, tokens, sensitive links, customer data, payment details, or raw webhook contents.

## Validation and handoff

- Inspect the complete diff, staged changes, and relevant untracked files; trace affected call sites and contracts across all participating layers.
- Check success and negative paths proportionate to risk, including authorization, Area/tenant isolation, privacy, null/empty input, dependency failure, timeout, cancellation, retry, duplicate, concurrency, rollback, and recovery.
- For persistence changes, verify migration `Up`/`Down` safety, database constraints, model configuration, snapshot alignment, provider compatibility, and deployment ordering.
- Add focused regression tests for fixed defects and material negative paths. Run only commands documented in `docs/development/validation.md` or verified directly from current build configuration.
- Report successful, failed, skipped, stale, partial, and unavailable checks accurately; distinguish introduced warnings from pre-existing ones.
- Re-review the final diff after fixes for inconsistent call sites, weakened boundaries, accidental edits, debug artifacts, generated output, and secret exposure.
- At handoff, report changed files, architectural/security decisions, validation evidence, operational or migration requirements, risks, unresolved business decisions, and validation gaps.

## Engineering Review Standard

This repository values **correctness over completion**.

Passing tests, successful builds, and green CI are evidence—not proof.

Before declaring any task complete, follow this review framework.

---

# 1. Reconstruct the purpose
Do not begin by reviewing the implementation.
First determine:

- the original problem;
- the intended business outcome;
- the architectural intent;
- the security invariants;
- the operational requirements;
- what success actually means.
If the branch purpose is unclear, reconstruct it from commits, PR description, issue, documentation and implementation.

State the branch purpose explicitly before reviewing.

---

# 2. Separate implementation from evidence
Never answer only:

> Is the implementation correct?
Instead answer two independent questions:

## Implementation
Does the implementation satisfy its intended purpose?

Status:

- Meets purpose
- Partially meets purpose
- Does not meet purpose

## Evidence
How strong is the evidence supporting that conclusion?

Evidence levels:

- Proven by end-to-end execution
- Proven by integration tests
- Proven by automated tests
- Verified by inspection
- Inferred
- Assumed
- Unknown
Never promote inspection into proof.

---

# 3. Establish causality
When failures occur:

Do not ask:

> How do we make CI green?
Instead ask:

> What system behaviour produced this outcome?
Determine:

- root cause;
- trigger;
- enabling conditions;
- whether the issue pre-existed;
- whether the branch introduced it;
- whether the branch merely exposed it;
- ownership.
Never fix a symptom before proving the cause.

---

# 4. Challenge assumptions
Treat every assumption as a hypothesis.

Explicitly distinguish:

Fact

Observation

Inference

Assumption

Opinion

State confidence for important conclusions.

---

# 5. Security review
Do not trust the UI.

Treat the backend as authoritative.

Attempt to identify:

- bypasses;
- privilege escalation;
- stale endpoints;
- legacy paths;
- direct API access;
- replay attacks;
- race conditions;
- concurrency issues;
- information leakage;
- secret persistence;
- logging leakage;
- transaction failures;
- rollback failures.
For every security claim identify the evidence supporting it.

---

# 6. Regression review
Every change must answer:

What existing behaviour could this have broken?

Review:

- authentication;
- authorization;
- persistence;
- migrations;
- API contracts;
- frontend contracts;
- concurrency;
- background jobs;
- messaging;
- caching;
- deployment;
- monitoring.
Do not stop after reviewing the changed files.

---

# 7. Time and state
Treat time as a domain concept.

Do not hide invalid state.

Prefer:

- fixing timestamp sources;
- explicit invariants;
- deterministic clocks.
Avoid:

- silent conversions;
- global normalizers;
- compatibility switches used as permanent solutions.

---

# 8. Verification
Evidence must be proportional to risk.

Authentication, authorization, money movement, onboarding, permissions and data integrity require stronger evidence than UI changes.
Where possible verify with:

- end-to-end tests;
- integration tests;
- repeated deterministic runs;
- migration validation;
- concurrency tests;
- rollback tests.
One successful run is not sufficient for intermittent failures.

---

# 9. Review conclusions
Every conclusion must contain:

- conclusion;
- supporting evidence;
- evidence type;
- confidence.
If evidence is missing, downgrade the conclusion.
Do not strengthen wording.

---

# 10. Remaining work
Separate findings into:

## Blocking
Must be resolved before merge.

## Accepted debt
Safe to merge with documented follow-up.

## Improvements
Useful but unrelated to the branch purpose.
Do not allow unrelated improvements to delay delivery.

---

# 11. Final verdict
Provide two independent verdicts.

## Implementation

- Meets purpose
- Partially meets purpose
- Does not meet purpose

## Evidence

- Sufficient for merge
- Additional verification required
- Insufficient evidence
A correct implementation with insufficient evidence is **not** merge-ready.
Likewise, green CI alone does not make a branch ready.

---

# Core principles

- Solve root causes, not symptoms.
- Optimise for understanding before fixing.
- Preserve architectural intent.
- Prefer deterministic systems over workarounds.
- Minimise assumptions.
- Keep changes within branch scope.
- Every fix should reduce long-term complexity.
- Every report should make it easier—not harder—for another engineer to independently reach the same conclusion.

# Engineering Verification Standard

## 1. Establish branch purpose
Before reviewing code, state:

- problem being solved;
- confirmed scope;
- business outcome;
- security and data invariants;
- explicit exclusions.
Do not infer unresolved business rules.

## 2. Define acceptance criteria
Create the smallest complete set of observable, testable criteria required for the branch purpose.

Do not add criteria merely because additional testing is possible.

## 3. Evaluate each criterion
For every criterion report:

- Requirement status:

- Met
- Partially met
- Not met
- Out of scope
- Unresolved decision
- Evidence strength:

- End-to-end
- Integration
- Automated component test
- Inspection
- Inference
- Unknown
- Confidence
- Merge impact
- Owner

## 4. Match evidence to risk
High-risk behaviour normally requires integration or end-to-end evidence:

- authentication;
- authorization;
- permissions;
- money movement;
- irreversible data changes;
- external integrations;
- migrations.
Lower-risk presentation behaviour may rely on component tests and inspection.

## 5. Test prohibited behaviour
For every critical invariant, test both:

- intended success;
- realistic bypass or failure.
Green happy-path tests are insufficient for security-sensitive features.

## 6. Establish causality
For every failure determine:

- branch-introduced;
- branch-exposed;
- pre-existing;
- unrelated;
- inconclusive.
Do not fix symptoms before causality is established.

## 7. Classify findings
Use:

### Blocking
Branch purpose or safety cannot be established.

### Required confidence
Evidence needed because failure impact is high.

### Accepted debt
Real issue that does not prevent branch purpose.

### Outside scope
The branch does not own the issue.

### Unresolved decision
A business or operational rule is missing.

## 8. Change policy
Only modify the branch when:

- a verified branch-owned defect exists;
- the smallest complete correction is clear;
- regression evidence can be added.
Do not add speculative hardening or unrelated cleanup.

## 9. Stopping rule
Verification stops when:

1. All predefined acceptance criteria are Met.
2. Evidence strength is appropriate to risk.
3. No verified branch-introduced blocker remains.
4. Remaining findings have owners and follow-up actions.
5. No new evidence invalidates an earlier conclusion.
Do not create new merge criteria after this point unless new evidence reveals a material risk.

## 10. Final verdict
Report separately:

### Implementation

- Meets purpose
- Partially meets purpose
- Does not meet purpose

### Evidence

- Sufficient for merge
- Additional verification required
- Insufficient evidence

### Operational state

- CI green
- CI blocked
- CI flaky
- External dependency blocked
Do not merge automatically unless explicitly authorised.

## Infrastructure verification

Never require a branch to re-prove infrastructure that already has established evidence.

Instead determine:

1. Is the branch introducing new infrastructure?

or

2. Is it consuming existing infrastructure?

If consuming existing infrastructure:

Only verify correct integration.

Do not require the branch to re-validate the infrastructure itself unless its assumptions changed.

When classifying verification work:

Do not ask:

"How important is this?"

Instead ask:

"If this branch disappeared tomorrow,
which team would still own this problem?"

Ownership takes precedence over desirability.

A feature branch should verify:

- behaviour it introduces;
- behaviour it changes;
- behaviour it integrates with.

It should not re-validate infrastructure merely because it depends on it.

Infrastructure verification belongs to the infrastructure owner unless the feature changes its assumptions.


---

# Product Workflow Verification Standard

A branch may satisfy its implementation purpose while the overall product workflow remains incomplete.

Implementation correctness and workflow completeness are independent.

Passing tests, successful builds, and green CI do not prove that users can successfully complete the business process.

Before declaring a branch ready for merge, perform the following workflow verification.

---

## 1. Reconstruct the complete workflow

Do not begin by reviewing the implementation.

First reconstruct the complete business workflow from the perspective of every participating actor.

Identify:

- where the workflow begins;
- where it ends;
- every actor;
- every state transition;
- every approval;
- every notification;
- every background process;
- every external dependency;
- every expected outcome.

State the workflow explicitly before reviewing implementation.

Example:

Customer
↓
Registers
↓
Pays
↓
Payment confirmed
↓
Area Administrator notified
↓
Area Administrator reviews
↓
Area Administrator approves
↓
Member becomes Active
↓
Qualification begins
↓
Commission becomes eligible
↓
Member receives benefits

---

## 2. Verify every actor

Every actor participating in the workflow must be able to complete their responsibility.

Typical actors include:

- Customer
- Member
- Area Administrator
- Platform Administrator
- Background workers
- Payment providers
- Email processors
- Scheduled jobs

For every actor determine:

- what information they receive;
- what action they are expected to perform;
- how they discover that action;
- what happens if they take no action.

If an actor cannot continue the workflow, the workflow is incomplete.

---

## 3. Verify every state transition

For every transition determine:

- trigger;
- owner;
- persistence;
- authorization;
- auditability;
- notification;
- idempotency;
- recovery.

Also determine:

- what happens if it never occurs;
- what happens if it occurs twice;
- what happens if it occurs out of order.

Do not verify only the transition modified by the branch.

Verify the entire lifecycle.

---

## 4. Verify discoverability

Do not assume backend state is sufficient.

Determine how users discover the next required action.

Examples:

- dashboard status;
- approval queues;
- pending badges;
- actionable messages;
- progress indicators.

Email should normally be treated as an attention mechanism, not the authoritative workflow.

Users must still be able to complete the workflow without relying solely on email delivery.

---

## 5. Verify workflow completion

Do not stop when the branch purpose succeeds.

Instead ask:

Can a first-time user complete the entire business process without developer intervention?

If not:

identify the missing transition.

---

## 6. Perform exploratory testing

For workflows involving:

- authentication;
- authorization;
- onboarding;
- payments;
- approvals;
- notifications;
- permissions;
- background workers;
- long-running state;

perform exploratory testing beyond acceptance criteria.

Examples include:

- browser refresh;
- leaving and returning later;
- multiple tabs;
- duplicate actions;
- retries;
- delayed background workers;
- delayed email;
- expired sessions;
- direct URL navigation;
- interrupted workflows.

Attempt to break the workflow.

---

## 7. Independent verification

The implementation agent must not determine that the workflow is complete.

Workflow verification must assume the implementation is incorrect until evidence demonstrates otherwise.

Review the workflow independently of the implementation report.

---

## 8. Separate implementation from workflow

Provide separate conclusions.

### Implementation

- Meets purpose
- Partially meets purpose
- Does not meet purpose

### Workflow

- Complete
- Partially complete
- Incomplete

### Evidence

- End-to-end verified
- Integration verified
- Automated tests
- Inspection only
- Inference

Implementation success must never be promoted into workflow completion without appropriate evidence.

---

## 9. Final workflow questions

Before declaring a branch ready for merge, answer:

- Can every actor complete their part of the workflow?
- Can every actor discover their next required action?
- Does every state transition have a clear owner?
- Are notifications sufficient but not authoritative?
- Can the workflow recover from interruption?
- Can a first-time customer complete the journey?
- Can a first-time administrator complete the journey?
- Is any step dependent on developer intervention?

If any answer is "No", classify the finding as:

- Blocking
- Accepted debt
- Product decision
- Outside branch scope

and justify the classification with evidence.

---

## 10. Stopping rule

Workflow verification stops only when:

1. Every intended actor can complete the workflow.
2. Every required state transition is reachable.
3. Every transition has appropriate evidence.
4. No verified branch-owned workflow blocker remains.
5. Remaining workflow gaps have documented owners or follow-up actions.
6. No new evidence invalidates an earlier conclusion.

A branch is not ready for merge merely because its implementation meets the branch purpose.

It is ready only when both:

- the implementation satisfies its intended purpose; and
- the complete user workflow has sufficient evidence that it can be completed successfully.

- Treat the complete user workflow as authoritative, not individual implementation tasks. A feature is not complete until every participating actor can successfully complete the end-to-end business process with appropriate evidence. Do not assume that satisfying the branch purpose alone proves workflow completion.

## Evidence and Completion Rules

* Never claim something is fixed, complete, tested, merged, deployed, enabled, or production-ready unless you directly verified that exact claim.
* Distinguish clearly between: `IMPLEMENTED`, `TESTED`, `INTEGRATED`, `MERGE READY`, `MERGED`, `DEPLOYED`, `ENABLED`, and `PRODUCTION VERIFIED`. These are not interchangeable.
* Never invent missing facts, command output, test results, production state, historical data, payments, approvals, timestamps, or session ownership. If unknown, say `UNKNOWN` or `NOT VERIFIED`.
* Previous agent reports are context, not proof. Carry evidence forward as `CARRIED FORWARD — VERIFIED AT <commit>` and challenge prior conclusions when current evidence disagrees.
* Tests only prove what they actually exercise. Do not use focused/unit/SQLite/mock tests as proof of broader PostgreSQL, provider, deployment, or production behaviour.
* Never infer historical facts from current state or production state from repository state.
* In multi-agent work, verify `worktree`, `branch`, `HEAD`, and dirty files before changing anything. Unexpected files/commits may belong to another session; do not modify, stash, reset, clean, or commit them until ownership is known.
* Never make tests green by weakening assertions, bypassing validation, suppressing fail-closed behaviour, or fabricating data.
* Before declaring completion, ask: **What did I verify? What remains unverified? What evidence could make my conclusion false?**
* Final reports must state: `Verified`, `Not Verified`, `Known Blockers`, `Tests Run`, and `Git State`. Prefer an incomplete truthful result over an unsupported success claim.
