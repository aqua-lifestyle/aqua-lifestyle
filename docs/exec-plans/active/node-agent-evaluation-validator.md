# Replace the agent-evaluation runtime

> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

Starting commit:e098142ad8f19da06b43c1c4c7b64f314825a233, PR106, branch
feature/unattended-production-deployment. The owner requires no Python source or
commands and authorizes replacement using existing tools; Bash is permitted.
Use the existing Node.js runtime and built-ins, with no package installation.

Scope: record the owner's tooling restriction in AGENTS.md, replace the single source
validator, preserve structure/routing/schema/result/
authority/link checks and built-in negative self-tests, update current commands and
describe historical validation truthfully. Run the replacement and its tests in the
existing Node24 CI job and update that workflow's security source pin; retain all209
security identities. No application runtime, release gate or provider changes.
Historical source digest manifests are records, not executable dependencies; do not
rewrite prior source identities to make them resemble the current source tree.

Implementation uses validate.mjs and Node test fixtures. Preserve compact sorted-key
UTF-8 case hashes, full-match identifier validation, exact skill/assertion ordering,
clean/dirty commit identities, missing-evidence distinctions and rejection of invalid
claims. Test valid and invalid CLI files and meaningful repository mutations without
executing the retired runtime. Confirm current source files and active commands no
longer depend on it, then review/commit/push only this PR branch under standing scope.

Current: initial25 Node regressions passed with no skips; repository validator passed
six skills/sixteen cases including built-in negative self-tests. Additional disposable
repository mutations and all16 canonical hash comparisons remain to run. Existing
application/security/runtime evidence belongs to e098142; new source qualification
must identify its own commit and run. Production acceptance remains BLOCKED as recorded
in the preceding privilege-isolation report.

Review reproduced three introduced compatibility defects using disposable Node
fixtures: alias invocation could exit0 without validation, symlinked Markdown was
omitted from link checks, and permissive text decoding replaced corrupt evidence
bytes. Corrected physical module/entrypoint resolution, Markdown discovery and fatal
UTF-8 decoding (retaining BOM semantics). Added explicit negative/positive regressions
for all three; final29-case run remains required. No retired-runtime execution occurred.

Before these corrections,26 regressions passed, all16 current case digests matched
independent Bash/jq/sha256sum encoding, mandatory209 and mutation fixtures passed,
and syntax/actionlint/diff checks passed. Source inventory changes only the CI workflow
hash; all209 security identities and21 source entries remain. Full CI and exact-final
review remain pending. An optional disposable-export shell command was rejected
before execution because its cleanup used rm -f style syntax; it provided no test
result. Use Node's bounded temporary-fixture cleanup for subsequent export validation.

Final working-source validation:29/29 regressions, zero skips; six skills/sixteen
routing cases; identical16 case hashes; syntax/actionlint/diff checks;209 mandatory
security and retained mutation fixtures all passed locally. Local Node version is26;
hosted Node24 execution is pending. A subsequent disposable source export succeeded:
the retired file was absent, repository validation and all29 regressions passed, and
its generated directory was removed with bounded Node fixture cleanup.

Agent2 independently reran29 tests/repository validation and inspected all three fixes,
confirmed unchanged209 identities and matching21 source pins, and reported no further
material findings. The complete policy digest isb73fd131ea17c6464dd938cb440eea19d971fbb674e257809e1fcd262dc5dfdd;
this differs from the CI workflow source hash2ad67d28c287ef5b9e279a7544a9d11782444246e1007be9f220c33e06500022.
Next: commit/push this branch, exact-commit review and automatic hosted CI; record final
source/run/attempt evidence on PR106 without relabeling prior privilege qualification.
