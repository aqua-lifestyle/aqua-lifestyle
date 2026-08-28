# Aqua Lifestyle repository instructions

These instructions apply repository-wide. Nested `AGENTS.md` files add scoped
constraints; they must not weaken this contract. Load detailed workflows through
the skills below instead of treating this file as a universal checklist.

## System and authority

Aqua is a multi-tenant membership platform with an ABP/.NET backend and a Next.js
frontend. AQGreen and Onyx are separate programmes with distinct participation,
payment, recruitment, qualification, and ledger semantics. Preserve the existing
ABP layer direction: domain invariants in Core, orchestration in Application,
persistence in EntityFrameworkCore, and HTTP/authentication at Web boundaries.

- The latest explicit business-owner decision is authoritative for business intent.
  `UNRESOLVED` means unresolved; code is implementation evidence, not automatic
  proof of intent.
- Agents may investigate, compare evidence, propose decisions, and draft records.
  They must not mark a material policy `CONFIRMED` without explicit owner
  authorization. Business-policy records use `UNRESOLVED`, `PROPOSED`, `CONFIRMED`,
  or `SUPERSEDED` as defined in
  [document 07](docs/aqua-system/07-verification-decision-and-risk-register.md#p-business-decision-authority-convention).
- A worklog, agent report, PR description, or agent-authored chat summary is not
  business authority. Record workstream state separately from decisions.
- Start authority resolution at the [Aqua system index](docs/aqua-system/README.md).
  Use [document 07](docs/aqua-system/07-verification-decision-and-risk-register.md)
  for unresolved decisions, contradictions, risks, and status evidence. Resolved
  V2 design semantics live in the
  [AQGreen Placement V2 specification](docs/aqua-system/aqgreen-network-placement-specification.md).

## Non-negotiable boundaries

- Tenant is the hard data, authorization, and topology boundary. Area is a
  same-Tenant administrative ownership/approval scope; Area checks never replace
  Tenant isolation, and cross-Area topology grants no cross-Area administration.
- Backend authorization is authoritative; UI visibility is not authorization.
  Tenant, Area, role, permission, and ownership boundaries must not be weakened,
  and direct API or legacy paths must not bypass them.
- Payment confirmation is not participation activation. A verified payment may
  move a participation to awaiting approval; only the authorized Area approval
  transition activates it.
- AQGreen is not Onyx. Legacy `Entry*` names are AQGreen compatibility terminology,
  not a third programme, and Business Premier is not an Onyx alias.
- AQGreen V1 and Placement V2 are separate semantic paths. V2 design or
  implementation evidence does not authorize migration, cutover, financial use,
  or reinterpretation of V1 history. Follow explicit selection and cutover rules.
- Recruitment attribution identifies acquisition/sponsor credit. Placement topology
  identifies structural location. Neither may be substituted for the other.
- Structural completion or topology does not itself prove financial eligibility,
  entitlement, release, or payment.
- Historical facts require effective-dated or immutable evidence. Mutable current
  Customer, User, Area, security, or lifecycle state must not be presented as its
  exact historical value. Preserve history and fail closed where required evidence
  cannot be reconstructed.
- Changes to shared durable state must preserve atomicity, database constraints,
  idempotency, retry safety, and concurrency correctness. Load
  `verify-stateful-change` when those properties are material.
- Migrations and historical operations must not fabricate or destroy authoritative
  data. Validate semantics against the authoritative database provider where
  providers differ; SQLite or mock evidence alone cannot establish migration safety.
- Treat client input, identity claims, provider callbacks, and external responses as
  untrusted. Never expose credentials, provider secrets, one-time links, PII,
  payment-sensitive data, or production data in source, URLs, logs, tests, examples,
  prompts, or reports.

## Semantic integrity

- Never turn an unknown, unsupported, corrupt, policy-unresolved, or
  missing-evidence state into a valid-looking business value such as `0`, `false`,
  `null`, an empty collection, a default enum, Level 0, `Incomplete`, or a legacy
  result merely to keep a workflow or DTO functioning. Absence of evidence is not
  evidence of zero.
- Preserve the distinction between legitimate business incompleteness and a result
  the system cannot determine safely.
- Once a version or path is explicitly selected, failure in that path must not
  silently fall back to another implementation unless an explicit business or
  rollout rule authorizes it.
- Friendly error translation may change presentation, but it must not convert
  failure into success or erase the semantic failure category.
- If a contract requires data the authoritative model cannot provide, do not
  fabricate compatibility values. Extend the authority safely, adapt the contract,
  or report the blocker.
- Never make tests or validation green by weakening assertions, bypassing
  validation, suppressing fail-closed behavior, skipping required execution,
  fabricating data or evidence, or converting a real failure into an allowed
  fallback.

## Work ownership and authorization

- Before editing, inspect applicable instructions, branch, worktree, `HEAD`, status,
  relevant untracked files, implementation, tests, and authority. Preserve unrelated
  and user-authored work. In multi-agent work, unexpected files or commits belong to
  their current owner until proven otherwise; do not reset, stash, clean, overwrite,
  stage, or commit them.
- Keep changes within the requested scope. Fix verified root causes with the
  smallest complete change; do not silently absorb unrelated findings.
- When behavior or operational requirements change, update the relevant current
  documentation and configuration examples or report the unresolved consistency gap.
- Commit, push, PR creation, and other repository mutations require authorization
  for the current task. Merge, deploy/release, production migration or data
  mutation, provider/payment mutation, financial reconciliation, worker/feature
  enablement, uncertain destructive Git/filesystem action, and confirmation of
  unresolved policy remain human-gated.
- Do not create production access or infer production state. Constrained
  observability may be authorized separately; unrestricted production database
  access is not authorized by repository instructions.
- Use a versioned [execution plan/worklog](docs/exec-plans/README.md) for long-running,
  multi-session, or multi-agent work. Do not create one for a trivial task.

## Evidence and completion

- Use exactly: `IMPLEMENTED`, `TESTED`, `INTEGRATED`, `MERGE READY`, `MERGED`,
  `DEPLOYED`, `ENABLED`, and `PRODUCTION VERIFIED`. No status implies the next.
- Tests prove only what they execute. Distinguish unit/component, integration,
  PostgreSQL, provider, E2E, inspection, inference, repository configuration,
  deployment, and production evidence. SQLite/mock/focused results do not prove
  PostgreSQL, provider, full workflow, deployment, or production behavior.
- Report passed, failed, skipped, unavailable, stale, and carried-forward evidence
  truthfully. Never invent command output, state, historical facts, or completion.
- When source distinctions materially affect a decision, use `FACT`,
  `REPOSITORY EVIDENCE`, `EXTERNAL EVIDENCE`, `EXPERIMENT RESULT`, `INFERENCE`,
  `ASSUMPTION`, or `UNKNOWN`; do not force every label into every response.
- Follow [repository validation](docs/development/validation.md) proportionate to
  risk, and inspect the final diff, staged state, and relevant untracked files.
- Use the [review benchmark](docs/review-benchmark.md) as the canonical review
  methodology. Workflow verification is conditional, not a gate for every isolated
  implementation task.

## Skill routing

Load only the skills relevant to the request:

- `resolve-authority` — ambiguous/conflicting business semantics, AQGreen/Onyx or
  V1/V2 interpretation, unresolved decision IDs, or uncertain external behavior.
- `diagnose-bug` — unexplained failure or regression where causality must be
  established before a patch.
- `review-change` — independent correctness review of a branch/diff, including
  required semantic integrity, compatibility, authorization, history, regression,
  and branch-owned defects.
- `verify-stateful-change` — specialist implementation or review of writes,
  payments, webhooks, migrations, transitions, placement, graduation, commissions,
  workers, concurrency, retries, or recovery.
- `validate-evidence` — selection/execution of validation, Git/diff and CI evidence,
  commit/PR readiness evidence, or truthful completion claims.
- `verify-workflow` — workflow-changing features, onboarding, payments, approvals,
  member/admin journeys, multi-actor transitions, epic/release acceptance, or an
  explicit end-to-end review. Do not load it for an isolated pure implementation.

Skills are hypotheses until evaluated. Their structure and current evaluation cases
are documented in [agent evals](docs/agent-evals/README.md).
