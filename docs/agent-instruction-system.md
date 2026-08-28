# Repository agent-instruction architecture

**Status:** Behaviorally accepted for initial repository use; full multi-trial A-D
effectiveness evaluation deferred

**Scope:** Repository instructions, skills, governance, workstream memory, and evals

**Product behavior:** Unchanged

## Documented compatibility

The following are documentation-only findings from primary sources reviewed on
2026-08-28. They establish documented compatibility, not runtime behavior:

- [Codex `AGENTS.md`](https://developers.openai.com/codex/guides/agents-md)
  builds a root-to-working-directory instruction chain; closer files appear later
  and can override earlier guidance. The default combined project instruction limit
  is 32 KiB.
- [Codex skills](https://developers.openai.com/codex/skills) discover repository
  `.agents/skills` directories from the current directory through the repository
  root. Discovery initially exposes name/description, supports explicit and implicit
  activation, and loads the full `SKILL.md` on use.
- [OpenCode rules](https://opencode.ai/docs/rules) recognize project `AGENTS.md` and
  allow custom instruction configuration. Its current V1 documentation selects the
  first local rule while walking upward. Current
  [OpenCode V2 instructions](https://opencode.ai/v2/docs/instructions) describe a
  combined ambient chain plus dynamically discovered nested instructions. The
  frontend nested file therefore explicitly requires the root contract if absent.
- [OpenCode skills](https://opencode.ai/docs/skills) discover
  `.agents/skills/<name>/SKILL.md` while walking to the Git worktree and load skills
  on demand through name/description advertisement.
- The [portable Agent Skills specification](https://agentskills.io/specification)
  requires `SKILL.md` YAML frontmatter with `name` and `description`, lowercase
  hyphenated names, matching directories, and relative shallow references. The six
  repository skills use only the universally safe required fields.

The candidate is **format portable** because it uses the common Agent Skills
directory and required frontmatter fields. It is **documented compatible** with the
referenced Codex and OpenCode conventions. Focused runtime acceptance on candidate
`abc9dc56c1a65dfe3ff3583159cc5c56a59a1006` passed in both harnesses without an
`opencode.json` or `.codex/config.toml` adapter. This does not establish behavior for
all future harness versions or models.

## Local smoke evidence

Codex CLI 0.147.0 was run before this correction against the then-dirty candidate
worktree, in ephemeral read-only subprocesses with low reasoning:

- Root and frontend subprocesses reported the expected instruction and skill names
  after inspecting repository files with commands such as `rg` and `sed`.
- One prompt explicitly named all six skills, and its response described their
  intended boundaries.

This was an **explicit-name smoke**, not isolated proof of native discovery or full
skill-body loading. The subprocesses could inspect the files directly, and no
per-case implicit routing assertions were evaluated in that early smoke. The CLI
emitted a local model-cache warning about `supports_parallel_tool_calls`, but all
three subprocesses exited successfully.

## Focused cross-harness acceptance

The immutable candidate
`abc9dc56c1a65dfe3ff3583159cc5c56a59a1006` subsequently passed the same focused
behavioral acceptance in OpenCode and Codex:

- root safety remained effective with no explicit skill invocation;
- all six repository skills were discovered natively;
- routing cases A-F matched the intended boundaries;
- frontend sessions retained root plus nested instructions and all six skills; and
- unsupported business policy was not promoted to `CONFIRMED`.

The Codex discovery probes used native initial-context metadata and prohibited shell
or file inspection, separating native discovery from manual reads. These results
support **behavioral acceptance for initial repository use**. They do not constitute
a repeated, statistically meaningful A-D comparison of skill effectiveness,
Nemotron evidence, or verification of future harness/model versions.

At task start the original main worktree was clean, contrary to the older discovery
audit embedded in the task brief: the owner had explicitly discarded the previous
`AGENTS.md` edit and the untracked audit had already been recoverably removed. The
semantic-integrity text remained directly owner-supplied in the current task and was
incorporated into the new root contract and `review-change` axis without modifying
the original worktree.

## Architecture

- Root `AGENTS.md` is the always-loaded authority and safety contract.
- The frontend nested `AGENTS.md` adds only frontend-specific rules and bootstraps
  root guidance across harness differences.
- Six task-oriented skills contain reusable review, diagnosis, state, evidence, and
  workflow methods. Business formulas remain in maintained Aqua documents.
- `docs/exec-plans` holds resumable workstream state and is explicitly non-authority.
- Document 07 owns the `UNRESOLVED` / `PROPOSED` / `CONFIRMED` / `SUPERSEDED`
  decision-state convention and the durable confirmation fields.
- `docs/agent-evals` and `tools/agent-evals` define deterministic structure and
  routing cases. Focused Codex/OpenCode acceptance passed; full comparative
  effectiveness evaluation remains deferred.

No backend nested `AGENTS.md` is added. It remains a deferred hypothesis until a
scope experiment proves useful context reduction while preserving root invariants.

## Rule disposition matrix

This maps reviewed rules to candidate locations. It records intended treatment and
structural inspection, not behavioral acceptance.

| Old rule or method | Candidate location | Treatment | Reason |
| --- | --- | --- | --- |
| Latest confirmed decision wins; unresolved stays unresolved | Root authority; `resolve-authority`; document 07 | Restated and clarified | Defines who may confirm without copying decisions into skills. |
| Backend authorization; Tenant, Area, role, permission, and ownership boundaries | Root no-skill safeguards | Restated concisely | UI, direct API, and legacy paths cannot weaken authority. |
| Payment confirmation versus activation | Root boundaries; stateful skill points to document 04 | Restated concisely | Prevents a known authorization defect class without duplicating provider fields. |
| AQGreen/Onyx and legacy naming | Root boundaries | Restated concisely | Prevents programme conflation. |
| V1/V2, attribution/topology, structural/financial separation | Root; `resolve-authority`; `review-change` | Restated and routed | These distinctions have material semantic risk. |
| Historical facts versus current state | Root; `review-change`; `verify-stateful-change` | Restated and routed | Prevents fabricated cutoff history. |
| Semantic integrity supplied by the owner | Root semantic integrity; `review-change` review axis | Condensed into invariant and review method | Keeps fail-closed meaning available even when no skill loads. |
| Shared-state atomicity, constraints, idempotency, retries, and concurrency | Root invariant; `verify-stateful-change` | Restored to root and routed to specialist | High-cost safety survives non-activation without copying a checklist. |
| Migration/history integrity and authoritative-provider evidence | Root invariant; `verify-stateful-change`; validation guide | Restored to root and routed | SQLite/mock evidence cannot prove provider-specific safety. |
| Universal test and validation integrity | Root invariant | Restored to root | It must not depend on diagnosis or another skill loading. |
| Documentation/config consistency | Root completion contract | Kept as concise root invariant | Behavior and operations frequently depend on aligned durable guidance. |
| Timeout and cancellation behavior | `verify-stateful-change` | Conditional skill procedure | Material for long-running state operations, not every task. |
| Browser refresh, return, direct URL, and multiple-tab interruption | `verify-workflow` | Conditional workflow examples | These are journey risks, not universal root rules. |
| Debug/generated artifacts and secrets in final diff | `validate-evidence` | Conditional readiness procedure | Relevant when validating a candidate diff. |
| Repeated evidence for intermittent failure | `diagnose-bug`; `validate-evidence` | Conditional diagnosis/validation procedure | Repetition matters only when instability is plausible. |
| Multi-agent worktree/branch/dirty-file ownership | Root work ownership | Restated concisely | Protects unrelated and user-authored work. |
| Secrets, PII, and payment-sensitive data | Root boundaries | Restated concisely | Repository-wide safety requirement. |
| Evidence vocabulary and completion limits | Root evidence; relevant skills; document 07 | Normalized and routed | Keeps material evidence categories consistent without requiring labels everywhere. |
| Independent correctness review | `review-change` plus `docs/review-benchmark.md` | Moved and narrowed | Separates semantic review from validation orchestration. |
| Validation and readiness evidence | `validate-evidence` plus validation guide | Moved and narrowed | Owns checks and claims without duplicating full correctness review. |
| Product workflow verification | Conditional `verify-workflow` | Moved and narrowed | Avoids re-proving unrelated product workflows. |
| Large negative-path and provider checklists | Relevant skills and current Aqua documents | Deferred on demand | Reduces root cost and stale duplication. |
| Generic SOLID/KISS/DRY/YAGNI and repository tour | Removed from root | Changed | Generic/discoverable guidance did not justify always-loaded cost. |
| Repeated handoff/report templates | Skills and worklog template | Consolidated | Keeps durable state without forcing one report shape on every task. |

## Autonomy boundary

Agents may be authorized per task through discovery, isolated implementation,
validation, commit, push, and PR creation. Merge, deployment, production migration
or mutation, provider/payment mutation, financial reconciliation, enablement,
uncertain destructive action, and material policy confirmation remain human-gated.

GitHub inspection on 2026-08-28 found `main` has no classic branch protection. The
active `gatekeeper` ruleset requires deletion and non-fast-forward protection only;
it does not require PRs, reviews, or passing CI/status checks. This blocks expansion
to autonomous merge. Before that expansion, enforce PR use, required CI/status
checks, appropriate review approval, and justified non-bypass behavior. Repository
prose is not equivalent to platform enforcement.

## Product-scope check

This refactor changes no backend or frontend application source, database schema,
migration, CI application pipeline, deployment configuration, AQGreen behavior,
Onyx behavior, or payment behavior. The only file below a product directory is the
frontend's instruction-only `AGENTS.md`.
