# Aqua Lifestyle engineering instructions

These instructions apply to the entire repository. More specific `AGENTS.md` files may add rules for their own directories.

## Working agreement

- Inspect the current implementation, relevant tests, business documentation, and `git status` before changing code.
- Treat the latest confirmed business rules as authoritative. Do not infer missing rules; ask when an unresolved decision would materially change behaviour.
- Verify reported findings against the current code and fix only issues that still apply.
- Make the smallest complete change. Preserve unrelated and user-authored work.
- Do not commit, push, merge, delete branches, or modify external services unless the user explicitly requests that action.
- Never place credentials, provider/API secrets, personal information, or production data in source control, logs, URLs, tests, or examples. Treat one-time account links as sensitive and never log them.
- Update configuration examples and documentation whenever deployment requirements or operational behaviour change.

## Architecture and implementation

- Follow the existing ABP layered architecture and dependency direction. Keep domain rules in the domain layer, orchestration in the application layer, persistence in Entity Framework Core, and provider details at infrastructure boundaries.
- Preserve aggregate, Area/tenant, authorization, and programme boundaries.
- Prefer explicit, descriptive business terminology and names that state what a service or class actually does.
- Apply KISS, SOLID, DRY, and YAGNI. Reuse proven abstractions without introducing speculative frameworks or generic fallback behaviour.
- Treat all client input, provider callbacks, webhook payloads, and external API responses as untrusted.
- Keep external calls outside long-running database transactions. Use durable outbox/inbox patterns where database state and external delivery must remain consistent.
- Design shared-state changes for concurrent requests and multiple application instances. Check atomicity, uniqueness constraints, idempotency, retry ownership, stale-work recovery, and terminal failure behaviour.

## Payments and transactional communication

- Resolve payments from server-stored provider identifiers and verify provider authenticity before changing business state.
- Do not activate programme participation until payment confirmation is verified.
- Preserve payment and webhook idempotency, immutable history, and reconciliation paths.
- Never manually alter production payment state without an auditable reconciliation process.
- Do not log or return provider secrets, tokens, message bodies containing account links, or sensitive customer data.

## Review standard

Before declaring a change ready:

- Review the complete diff and its call sites, not only the edited lines.
- Establish intent from the request, current business documents, acceptance criteria, relevant issue or pull-request history, and existing tests before judging the implementation.
- Trace contracts across frontend, API DTOs, application services, domain entities, persistence configuration, migrations, background workers, and deployment configuration.
- Check success, validation, authorization, privacy, null/empty, failure, timeout, retry, duplicate, concurrency, and rollback paths.
- Confirm migrations have a safe `Up`, a valid `Down`, matching model configuration/snapshot state, and appropriate database constraints.
- Add regression coverage for each fixed defect and for important negative paths.
- Run focused tests first, then the relevant frontend and backend suites, type checking, linting, security audit, and Release builds in proportion to risk.
- Report commands and results accurately. Do not claim a check passed if it was skipped, failed, or only partially executed.
- Summarise changed files, architectural decisions, operational requirements, risks, and unresolved business decisions.

## Living review benchmark

Use this benchmark for reviews of committed and uncommitted work. It combines context-aware, multi-pass, rule-driven, and tool-assisted review practices into one repository standard; no external reviewer is the authority over the current code and confirmed business rules. The rationale, workflow, evidence model, and improvement process are documented in `docs/review-benchmark.md`.

### Review passes

Perform distinct passes so one concern does not hide another:

1. **Intent and scope:** compare the implementation with the request, acceptance criteria, current business rules, related history, and the complete diff. Identify missing work and unrelated changes.
2. **Correctness and contracts:** trace inputs, outputs, state transitions, invariants, nullability, serialization, call sites, and compatibility across every affected layer.
3. **Security and privacy:** review authentication, authorization, tenant/Area isolation, ownership, validation, injection, secrets, personal data, sensitive URLs, logging, and safe failure behaviour.
4. **Reliability and concurrency:** review transaction boundaries, atomicity, idempotency, uniqueness, retries, duplicate delivery, ordering, race conditions, rollback, stale-work recovery, and terminal failure.
5. **Persistence and operations:** review schema constraints, migrations, backfills, provider/database compatibility, configuration, observability, deployment sequencing, and recovery or reconciliation paths.
6. **User experience and accessibility:** review business language, loading/empty/success/error states, actionable messages, navigation continuity, keyboard and screen-reader behaviour, and responsive presentation.
7. **Maintainability and performance:** review dependency direction, naming, cohesion, duplication, unnecessary abstraction, query and allocation cost, bounded work, cancellation, and resource disposal.
8. **Tests and evidence:** map each requirement and material risk to a focused test or other evidence, then run the broader validation required to detect integration regressions.

After these passes, perform an adversarial second look: try to disprove the implementation's assumptions, follow at least one complete success journey and one failure journey, and inspect nearby unchanged code that participates in those journeys.

### Finding quality

- Verify every finding against the current code before reporting or fixing it.
- Prioritise findings by customer/business impact and likelihood. Distinguish blocking defects from improvements and avoid low-value cosmetic noise.
- Distinguish defects introduced by the change from pre-existing problems, and do not attribute an existing defect to the current work. Report a severe pre-existing risk separately when it materially affects readiness.
- For each material finding, identify the violated requirement or invariant, the concrete failure scenario, affected scope, supporting evidence, and the smallest safe correction.
- Trace a suspected defect far enough to rule out protection elsewhere in the stack. Do not report speculative issues as facts.
- Deduplicate findings that share one root cause. Prefer one well-supported explanation that covers every affected call site over repeated line-level comments.
- Do not restate formatter, compiler, analyzer, or test output as an inferred review finding; run the relevant tool and report its actual result.
- Re-review the resulting diff after fixes. A fix is incomplete if it creates a regression, weakens a boundary, leaves a call site inconsistent, or lacks validation.

### Rules and continuous learning

- Treat repository instructions and confirmed business rules as a maintained rule system. More specific scoped instructions may strengthen, but must not silently contradict, repository-wide safety requirements.
- When rules overlap, duplicate one another, become obsolete, or conflict with current business decisions, reconcile them instead of accumulating contradictory guidance.
- When an internal or external review discovers a missed issue, determine the general failure pattern rather than copying a one-off finding. Add or refine the smallest reusable instruction, automated check, test, or constraint that would prevent the class of defect.
- Record a new benchmark rule only after validating it against the current architecture and at least one real example. Remove or revise rules when later evidence disproves them.
- Prefer executable enforcement—tests, analyzers, linters, schema constraints, and CI checks—over prose where practical, while retaining the business reason in documentation.
- Use prior accepted and rejected review findings as context, not unquestionable precedent. Current requirements, security, and verified behaviour take priority.
- Keep review instructions concise enough to remain usable. Periodically consolidate repeated rules and remove stale tool- or vendor-specific wording.

### Completion gate

A review is complete only when:

- every changed file and relevant untracked file has been inspected;
- affected call sites and cross-layer contracts have been traced;
- material findings are fixed or explicitly reported with evidence;
- focused and integration validation results are known;
- migrations, dependencies, generated files, configuration, and secret exposure have been checked where relevant;
- the final diff contains no accidental edits, debug artifacts, unsupported claims, or undisclosed validation gaps.
