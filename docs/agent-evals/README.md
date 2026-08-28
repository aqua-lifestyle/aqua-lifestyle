# Agent skill evaluations

The six repository skills are initial hypotheses. Valid prose and discovery do not
prove that a skill improves behavior. This directory defines small, repeatable tests
for structure, routing direction, cross-skill composition, and later behavioral
comparison.

## Deterministic validation

Run from the repository root:

```bash
python3 tools/agent-evals/validate.py
```

The validator checks format-portable frontmatter, skill/directory names, duplicate
IDs, positive/negative boundary coverage, composition cases, root routing and
no-skill safeguards, decision-state fields, frontend bootstrap, and local Markdown
references. Its built-in negative self-tests exercise result-schema and case/result
coherence enforcement. It uses only the Python standard library and makes no
network or model calls.

Case definitions are in [cases/skill-routing.json](cases/skill-routing.json). A
result may be checked with:

```bash
python3 tools/agent-evals/validate.py path/to/result.json
```

Results follow [result.schema.json](result.schema.json). Each result copies its case
classification and assertion IDs, records the canonical SHA-256 of that case
definition, and identifies one exact snapshot. A clean identity is `git:<commit>`;
a dirty identity is `git:<commit>+diff-sha256:<hash>`, where the hash must cover the
complete tracked and untracked candidate diff. Controlled candidate evaluations
should use a clean commit. Store a result under `results/` only when its commit,
harness, model, reasoning level, assertions, and evidence are worth carrying
forward. A missing or unavailable run is `NOT_RUN`, never `PASS`.

## Initial acceptance protocol

Use the same repository snapshot and cases in Codex and OpenCode. For every skill,
include a positive trigger, a negative/non-trigger, explicit invocation, implicit
discovery where supported, and at least one relevant composition case.

Compare four conditions:

| Condition | Instructions | Skill use |
| --- | --- | --- |
| A | Previous large `AGENTS.md` | No project skill |
| B | New concise `AGENTS.md` | No project skill |
| C | New concise `AGENTS.md` | Explicit relevant skill |
| D | New concise `AGENTS.md` | Implicit skill discovery |

Record assertion correctness, trigger precision and false triggers, evidence
quality, tool trajectory, tokens/cost when available, and elapsed time when useful.
Do not compare different commits, inputs, permissions, or model settings as though
they were controlled results.

Initial adoption needs structure validation plus a small cross-harness smoke set.
After acceptance, run deterministic checks on every skill change, focused cases for
the changed skill, and periodic full Codex/OpenCode comparisons. Network/model calls
must not be placed in basic CI. Do not claim statistical confidence from the initial
smoke runs.

## Harness behavior under test

- Root launch: root instructions and all six skills should be available.
- Frontend launch: root safety invariants and the nested frontend constraint should
  both apply; all six root skills should remain discoverable.
- Explicit load: the named skill body should load.
- Implicit behavior: positive prompts should tend toward the named skill and
  negative prompts should avoid it.
- Composition: relevant skills should compose without forcing every skill into the
  task.

No project `opencode.json` or `.codex/config.toml` adapter is included because the
official discovery conventions are documented compatible with this layout. Adapter
necessity remains a runtime hypothesis; add the smallest adapter only if a
reproducible harness failure demonstrates a need.
