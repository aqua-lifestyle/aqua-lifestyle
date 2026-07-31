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
