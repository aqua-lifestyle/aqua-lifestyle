# Local mandatory-security evidence

The starting commit is `1b662546de119edd4dd78514bd655a639444fdc5`.
Original raw Node execution at that source: 171 passed, zero failed/skipped.
A throwaway archive changed only the Render-failure regression to `test.skip`:
170 passed, one skipped, zero failed; process exit 0. No mutant was pushed.

`local-negative-probes.jsonl` is the executed output of
`node tools/deployment/mandatory-security-probe.mjs` against the working change.
It is reproducible local evidence, not a signature, CI artifact, independently
retained receipt or production claim. Its source fixture has its own synthetic
Git commit; it does not claim that fixture SHA is the application PR SHA.
The 171-case probe disambiguates two duplicate display names without changing their
assertions. The full proposed suite has 193 mandatory cases, including 22 verifier
checks, and passed locally on Node 26.6.0 and isolated Node 24.21.0.

`bash tools/deployment/disposable-recovery.sh` passed 15 synthetic PostgreSQL checks
using image `sha256:81bd698b4594e751a3269e4dcd3e03a4a0ec0daf7b72e7aa1abd43cce9887542`.
This does not prove application PostgreSQL regressions or production privileges.
Actionlint and `git diff --check` passed. Final-commit CI and review evidence is
recorded in PR 106 after publication, avoiding a self-referential commit claim.

Independent GitHub enforcement and deployment credential isolation are BLOCKED.
See [the custody contract](../../operations/mandatory-security-verification.md).
