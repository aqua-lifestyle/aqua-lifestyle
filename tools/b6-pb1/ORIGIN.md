# Native framing reuse and new-source boundary

The copied-record TAB/LF scanner, canonical uint64 overflow algorithm, owned-byte
views/destruction and EVP SHA-256 encoding are narrowly adapted from retained
`docs/operations/aqgreen-v2-b6/snapshot-package/successor-record.c/.h` in the original
B6 worktree. Relevant source ranges are C89–104,500–526,580–592 and header13–33.
The new PB1 fixed-field branch replaces the old role table/repeat/tuple/semantic
dispatch rather than importing those unavailable historical contracts.

Reuse-input identities observed at implementation start2026-10-18:

| Retained source | SHA-256 |
| --- | --- |
| `successor-record.c` | `7118aaeb864bfb592d0d6cd35231de0b9b1450c38322225a42ba020b465ebb91` |
| `successor-record.h` | `38fa83c672d216dc031dd17824b9f3b2a8558afbb2f445fd858f1369a6b30f24` |

This identifies the single reused source pair, not a repeated45-file preservation
check, old-suite execution, recovered historical normative subject or actual supply
selection. All adaptation is new PB1 source requiring its own focused evidence.

PB1-specific differences are required by the adopted contract:20/8 fixed field
tables and smaller bounds, nonzero H/Q checks, the stricter R alphabet, new
schema/version/realm namespace, closed exit/outcome/reason pairs and local typed
identity/result accounting. No historical package pin/schema is edited or aliased.
Explicit no-config OpenSSL initialization prevents this standalone hash helper from
requesting automatic configuration loading; it does not qualify a host/library
that was initialized elsewhere, the loader closure or confinement.
