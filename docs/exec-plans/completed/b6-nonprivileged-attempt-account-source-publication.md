# B6 nonprivileged attempt-account source publication

> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

## Goal and owner authorization

Preserve the established candidate grammar, pure transition validator and bounded
historical replay foundation on a dedicated feature branch. The owner's source
publication continuation authorizes exact transfer, native verification, one
bounded commit and a push of that branch only. It does not authorize merge,
deployment, V2 enablement, production migration, or additional implementation.

## Verified lineage

The repository base is `e164c08039e828c91c93cb039301435cf9e0a748`, tree
`a46563082a2a4445d854849939a79b1c2ced613c`, initially clean, with no untracked files.
The candidate receipt is 229274 bytes, SHA-256
`b5bb79fb88b4ba2b7e519799b22df17aa37f642723dc4e7a6a6bb6037f83f967`.
Its complete 213514-byte predecessor, SHA-256
`8414f5a3132f4964eee4e772b9a00975dc4d50abcff98c8c054125753f7122d1`,
and all 15 earlier recorded prefixes verified byte-identical before transfer.
The candidate receipt and its transient evidence are retained outside Git.

Continuations 05D, 07 and 09 source manifests identify the 15 transferred files:
three source/header pairs, six native fixtures, and three implementation documents
in `docs/operations/aqgreen-v2-b6/snapshot-package`. All transfers preserve exact
candidate bytes. This worklog and the repository fixture runner are publication
supporting files; they introduce no parser, transition or replay policy. The
source-controlled `ATTEMPT-ACCOUNT-FOUNDATION.sha256` records all 15 transferred
identities with repository-relative paths for subsequent native verification.

## Bounded implementation

The foundation implements strict grammar and canonical serialization, the frozen
eleven-event pure semantic relation, and deterministic historical replay. Replay
distinguishes historical prefixes from complete-current history and consumes
explicit independently validated evidence dispositions. The fixtures model those
dispositions; neither fixtures nor these APIs establish their authenticity.

The three transferred implementation documents retain their original continuation
context. Their workspace-specific evidence references describe historical
verification; use the repository command below for this integrated source.
Literal temporary paths in fixtures are synthetic protocol values or disposable
native fixture resources, not copies of host evidence or production data.

## Integrated verification

Run from the repository root, with an output directory outside the repository:

```bash
b6_results=$(mktemp -d)
bash docs/operations/aqgreen-v2-b6/snapshot-package/test-attempt-account-foundation.sh "$b6_results"
```

The runner rebuilds all six suites from repository sources, using C11, strict
compiler warnings and OpenSSL libcrypto. Normal builds use O2. ASan/UBSan builds
use O1, leak detection, halt-on-error, frame pointers and non-PIE executables.
Exact build/execution commands and per-suite stdout/stderr are retained in the
supplied external output directory. Counts are unique across build modes.

Fresh integrated execution passed on 2026-10-02:

| Suite | Unique cases | Distinct assertions | Normal | ASan/UBSan |
| --- | ---: | ---: | --- | --- |
| Primitive/schema | 156 | 564 | PASS | PASS |
| Inherited schemas | 399 | 1513 | PASS | PASS |
| Account grammar | 363 | 1497 | PASS | PASS |
| Event/frame grammar | 4397 | 26880 | PASS | PASS |
| Pure transitions | 1877 | 26502 | PASS | PASS |
| Durable replay | 499 | 45768 | PASS | PASS |
| Total | 7691 | 102724 | PASS | PASS |

All compiler/runtime stderr files were empty; sanitizer diagnostics: 0. Normal
and sanitizer fixture stdout matched byte-for-byte for every suite. These are
freshly executed native component/fixture results, counted once across modes.
They do not prove production workflow, external evidence validation, PostgreSQL,
provider, deployment or runtime qualification. Backend, frontend, containers and
production operations were not run: their source/configuration is unchanged.

All 15 transfers reconciled by SHA-256 and byte comparison. All 2094 pre-existing
tracked files remained byte-identical after execution. The changed paths comprise
six implementation files, seven test/support files and five documentation/identity
files, including this worklog. No binaries, draft clarification packages, temporary
receipts, host-specific evidence, credentials or production data are included.

## Open dependencies and next action

The independent historical-evidence producer remains unimplemented. Continuation
10's clarification is ready for independent review only: it has not been reviewed,
accepted or frozen. Its two open questions concern account-support source-role and
original-proof binding, and separate recovery grant/report referent binding. The
draft package is excluded from this commit.

Live evidence adapters, journal writer/commit/locking, helper/BIND integration,
runtime qualification and migration/cutover remain open. V2 remains disabled;
BIND remains fail-closed; the helper remains absent/unstarted with future required
refusal 125; all six adapters remain unstarted. A pure modeled admission transition
does not perform EXECUTION_ADMITTED or grant runtime authority.

The requirement map remains 0 complete, 4 incomplete, 0 contradicted, 23 absent.
Source publication does not complete any outstanding requirement. The next gate is
independent review of the Continuation-10 clarification, followed by a separate
owner acceptance/freeze gate before any producer implementation authorization.

## Git and publication boundary

Branch: `b6/nonprivileged-attempt-account-foundation`, created from the exact base.
Commit message: `B6: add nonprivileged attempt-account validation and replay foundation`.
Only this feature branch may be pushed; no force push, merge or deployment.
Checked-in CI runs on main, develop and feature/** pushes or pull requests;
image publication runs on version-tag pushes. This b6/** branch push triggers
neither checked-in workflow. No workflow or security control is changed.

## Historical operational disclosures

The historical accidental sed attempt against the null device remains classified
UNCERTAIN ATTEMPT + NO OBSERVED PERSISTENT CHANGE. Its exact outcome is uncertified.
The independent semantic reviewer created and cleaned up two transient zero-byte
files. Neither disclosure establishes accepted-package modification. Neither
incident is recreated by this publication work.
