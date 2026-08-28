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
  `.agents/skills` conventions. No adapter is included, and focused acceptance
  passed without one in the tested Codex and OpenCode harnesses. Future harness
  versions remain outside this evidence.

## Current state

The instruction-system implementation workstream is complete and archived with this
record. Final status:

- `IMPLEMENTED`
- `STRUCTURALLY VALIDATED`
- `FOCUSED CODEX ACCEPTANCE PASSED`
- `FOCUSED OPENCODE ACCEPTANCE PASSED`
- `BEHAVIORALLY ACCEPTED FOR INITIAL REPOSITORY USE`

This status does not mean `FULL MULTI-TRIAL A-D EVALUATED`. The original main
worktree remained outside this workstream's edits.

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

- Candidate `abc9dc56c1a65dfe3ff3583159cc5c56a59a1006` passed the post-commit
  deterministic validator, all six structural skill checks, JSON parsing, Python
  compilation, reference and retired-skill scans, whitespace checks, and clean
  worktree verification.
- OpenCode focused behavioral acceptance reported for the exact candidate in the
  release-preparation handoff: `PASS`.
  - root no-skill safety: `PASS`;
  - all six repository skills discovered natively;
  - routing cases A-F: `PASS`;
  - frontend root plus nested instruction behavior: `PASS`; and
  - governance refusal to establish unsupported `CONFIRMED` authority: `PASS`.
- Codex CLI 0.147.0 focused behavioral acceptance: `PASS`.
  - root no-skill safety: `PASS`, with the probe reporting no skill body loaded;
  - all six repository skills discovered from native initial-context metadata,
    without filesystem inspection;
  - six isolated routing cases A-F: `PASS`;
  - frontend root plus nested instruction behavior and all-six skill discovery:
    `PASS`; and
  - governance refusal to establish unsupported `CONFIRMED` authority: `PASS`.

### NOT RUN / DEFERRED FOLLOW-UP EVIDENCE

- Controlled, repeated A-D comparison of the old root, concise root without skills,
  explicit skills, and implicit skills.
- Statistical or multi-trial evidence that the skills improve task outcomes.
- Nemotron behavior.
- Future Codex/OpenCode versions, other models, and other harnesses.

### DOCUMENTATION-ONLY CLAIM

- Current official Codex and OpenCode documentation describes compatible
  `AGENTS.md` and `.agents/skills` conventions. This supports documented
  compatibility only, not either runtime result.

## Deferred evidence

- Full multi-trial A-D skill-effectiveness evaluation remains useful follow-up. It
  is not part of the focused initial repository-use acceptance recorded here.

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
- Created immutable candidate
  `abc9dc56c1a65dfe3ff3583159cc5c56a59a1006` and reran deterministic checks on
  that clean snapshot.
- Completed matching focused OpenCode and Codex acceptance for root safety, native
  skill discovery, routing A-F, frontend instruction composition, and governance.

## Next action

Release the accepted initial-use foundation through review and CI. Run the deferred
multi-trial A-D effectiveness experiment separately if comparative benefit or
future routing calibration is required.

## Git/branch context

- Repository/worktree: `aqua-lifestyle`, isolated agent-instruction worktree
- Branch: `chore/agent-instruction-system`
- Base and pre-candidate `HEAD`: `812a2250036723a8ebb965a2e0da3d868cd103ff`;
  candidate identity is the commit containing this plan
- Pre-commit scope: twenty instruction/documentation/eval files only, with no
  unrelated staged, unstaged, or untracked file
- Acceptance-record identity: the follow-up commit containing this archived plan
- Push/PR state at archival edit: pending release-preparation push and PR
