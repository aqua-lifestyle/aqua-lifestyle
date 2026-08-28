# Repository agent-instruction system

> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

## Goal

Create a format-portable, documented-compatible instruction and skill candidate for
Codex and OpenCode without changing product, database, CI application, deployment,
AQGreen, Onyx, or payment behavior.

## Authoritative references

- The owner task brief authorizes this task's execution scope and candidate design;
  it is not durable product-policy authority.
- Root `AGENTS.md`, `docs/aqua-system/README.md`, document 07,
  `docs/development/validation.md`, and `docs/review-benchmark.md`.
- Current official Codex, OpenCode, and Agent Skills documentation recorded in
  `docs/agent-instruction-system.md`.

## Task-scoped design constraints

- Six initial skill hypotheses; semantic integrity remains in root and
  `review-change`; workflow verification is conditional; production and material
  business decisions remain human-gated. These constraints govern this task but do
  not create or confirm an Aqua business-policy decision.

## Assumptions

- Official documentation describes compatible root `AGENTS.md` and
  `.agents/skills` conventions. No adapter is included in this candidate. Whether
  Codex or OpenCode needs one remains a runtime hypothesis.

## Current state

The local correction pass is complete in the isolated instruction-system worktree.
The original main worktree was clean at task start and remains outside this task's
edits. Deterministic checks pass on the corrected dirty worktree. Cross-harness
behavioral acceptance remains pending and the skills are still hypotheses.

## Evidence

### CARRIED FORWARD

- Base `812a2250036723a8ebb965a2e0da3d868cd103ff` matched `origin/main`; provenance:
  task-start `git rev-parse HEAD` and `git rev-parse origin/main`.
- Codex CLI 0.147.0 was locally available; OpenCode CLI was unavailable; provenance:
  pre-correction version checks.
- Three pre-correction Codex subprocesses ran against a dirty worktree. Root and
  frontend agents used repository inspection commands such as `rg` and `sed`; an
  explicit prompt named all six skills and received a boundary summary. This is an
  explicit-name smoke, not isolated native discovery, full-body loading proof, or
  behavioral routing evidence.
- GitHub inspection reported that the `gatekeeper` ruleset blocked deletion and
  non-fast-forward updates only as of 2026-08-28. This is external-state evidence
  carried from the initial pass, not rechecked by this correction.

### RUN ON THIS WORKTREE

- `python3 tools/agent-evals/validate.py`: passed six skill structures, sixteen
  routing definitions, strict result-schema/coherence self-tests, authority fields,
  root safeguards, frontend bootstrap, and local links.
- Skill Creator `quick_validate.py` was run once for each of the six skill
  directories: all passed.
- `python3 -m json.tool` parsed the case and schema files; `py_compile` compiled the
  validator with its cache outside the repository. The optional `jsonschema`
  package was unavailable, so no claim depends on it.
- `rg` scans found no retired standalone semantic-integrity skill reference or
  trailing whitespace. `git diff --check` passed.
- `git status --short --untracked-files=all`, `git diff --name-only`, and worktree
  inspection showed twenty instruction/documentation/eval files only; the original
  main worktree remained clean. The frontend product tree contains only the nested
  instruction-file change.

### RUN ON COMMITTED CANDIDATE

- Post-commit results are intentionally not embedded in the commit that they verify.
  Resolve the candidate with `git rev-parse HEAD`, rerun deterministic validation,
  and report the exact SHA and result in the handoff.

### NOT RUN

- OpenCode runtime discovery or behavior.
- Isolated Codex native discovery.
- Implicit positive/negative routing cases.
- Controlled A-D behavioral comparison.
- Any claim that the six skills improve task outcomes.

### DOCUMENTATION-ONLY CLAIM

- Current official Codex and OpenCode documentation describes compatible
  `AGENTS.md` and `.agents/skills` conventions. This supports documented
  compatibility only, not either runtime result.

## Open questions

- Cross-harness behavioral acceptance remains pending. OpenCode is unavailable and
  the task explicitly defers expensive model-backed A-D runs until an immutable
  candidate exists.

## Completed work

- Reviewed primary documentation and recorded its discovery/frontmatter conventions
  as documentation-only compatibility evidence; inspected current repository
  instruction, authority, ADR, validation, review, and frontend files.
- Created the isolated worktree and branch.
- Implemented the concise root/nested instructions, authority convention, six
  skills, worklog convention, eval cases/schema/validator, and architecture report.
- Performed a structural rule-disposition and product-scope review; behavioral
  preservation remains unclaimed.
- Corrected root no-skill safeguards, skill boundaries/composition, decision
  governance, eval fixtures/coherence enforcement, and overstated evidence claims.
- Ran the deterministic checks recorded above on the final corrected dirty
  worktree.

## Next action

The commit containing this plan is the intended immutable eval candidate. Resolve
its SHA, rerun deterministic validation on the clean candidate, then run matching
Codex/OpenCode routing and composition tests against that exact SHA before treating
the system as behaviorally accepted.

## Git/branch context

- Repository/worktree: `aqua-lifestyle`, isolated agent-instruction worktree
- Branch: `chore/agent-instruction-system`
- Base and pre-candidate `HEAD`: `812a2250036723a8ebb965a2e0da3d868cd103ff`;
  candidate identity is the commit containing this plan
- Pre-commit scope: twenty instruction/documentation/eval files only, with no
  unrelated staged, unstaged, or untracked file
- Push/PR state: no push or PR
