# PB1 successor invocation — normative candidate

Version `B6S/1.0.0-candidate.1`; wire namespace `B6S_PB1_*_V1`.
PROPOSED only. Historical TQ1/PB1 compatibility is UNKNOWN. This specification
supplies new prospective semantics; it does not recover or amend historical bytes.

## P01. Scope and trust boundary

This first profile is one native Linux read-only parser process operating only on
private immutable qualification inputs. It returns representation results, never
accepted authority, qualification, BIND, journal commitment or financial permission.
It does not launch the .NET representative-analysis pipeline or a historical TQ1
entrypoint. Such profiles require separate explicit selection/review.

A separately selected controller validates the descriptor and actual launch facts;
the parser cannot authenticate its own release, supply, examiner or issuer. The
externally approved campaign plan must bind all selected role identities and their
qualified validators/original provenance. A hash-shaped reference or caller
`approved=true` cannot replace that approval. Development fixtures stay labeled
synthetic and cannot be registered as actual accepted subjects.

## P02. Exact descriptor bytes

Encoding reuses the retained canonical native-record framing: ASCII bytes only,
one `key<TAB>value<LF>` row per field, in the exact order below, including final LF.
There is exactly one TAB per row. No BOM, CR, NUL, empty row/value, escaping,
whitespace normalization, duplicate/unknown/reordered/missing field or trailing
byte is admitted. Maximum descriptor length16,384 bytes. No external schema table
or input-selected fallback changes these rules.

Types: `H`=64 lowercase ASCII hex SHA-256 characters, excluding the all-zero
placeholder; `U`=unsigned decimal with no leading zero except literal0;
`Q`=lowercase hyphenated UUID, not all-zero; `R`=1–64 ASCII characters matching
`[A-Z][A-Z0-9_]{0,63}`. Digest roles are explicit; a Git OID is not inferred as H.

| Order/key | Type/value | Binding |
| --- | --- | --- |
| 1 `schema` | literal `B6S_PB1_INVOCATION_V1` | Distinct successor descriptor |
| 2 `version` | literal `B6S1` | No downgrade/legacy fallback |
| 3 `realm` | literal `ISOLATED_QUALIFICATION` | Reject production/operational reuse |
| 4 `successor_manifest_sha256` | H | Exact adopted package manifest |
| 5 `invocation_spec_sha256` | H | Exact adopted PB1 specification bytes |
| 6 `qualification_plan_sha256` | H | Independently approved static campaign policy |
| 7 `release_manifest_sha256` | H | Reviewed source/build/executable/dependency closure |
| 8 `entrypoint_source_sha256` | H | Exact source implementing this entrypoint |
| 9 `executable_sha256` | H | Actual selected native ELF bytes, not a path/wrapper |
| 10 `parser_manifest_sha256` | H | Parser code/configuration/entrypoint identity closure |
| 11 `grammar_manifest_sha256` | H | Exact compiled grammar, versions and limits |
| 12 `schema_id` | R | One explicit schema in the compiled whitelist |
| 13 `schema_manifest_sha256` | H | Exact selected schema definition/version closure |
| 14 `layout` | H or literal `CANONICAL_TEXT` | Qualified native layout or explicit canonical-text profile |
| 15 `configuration_sha256` | H | Immutable configuration bytes, at most65,536 bytes |
| 16 `platform_manifest_sha256` | H | Linux/CPU/ABI/loader/libc/crypto/tool/runtime profile |
| 17 `selected_supply_sha256` | H | Actual role-typed selected source/native supply closure |
| 18 `input_sha256` | H | Exact immutable input bytes |
| 19 `input_bytes` | U,0..1,048,576 | Observed byte count;0 means an actually empty file, not unknown |
| 20 `invocation_id` | Q | Scoped intent identity; not authorization or a timestamp |

Reference graphs must be acyclic. The static campaign plan binds selected profiles
and test predicates, but MUST NOT hash per-attempt descriptors/results/receipts.
A separately retained run intent binds descriptor hash and campaign-plan hash
after the descriptor exists. Results/observations may refer backward to those
objects. The descriptor contains neither its own hash nor a future result hash.

The selecting plan must give each manifest's exact role/type/version, bytes/size,
source/release and qualified validator/provenance mapping. Identity equality alone
does not validate a manifest's assertions. Missing or unqualified dependencies
deny launch; no arbitrary raw blob can fill a typed manifest role.

## P03. Actual process invocation

The C-string argv vector is **exactly seven elements**, followed by NULL:

```text
argv[0] = b6-pb1-v1
argv[1] = --request-fd
argv[2] = 3
argv[3] = --input-fd
argv[4] = 4
argv[5] = --result-fd
argv[6] = 5
```

No shell, command string, PATH lookup, script interpreter, extra/combined option,
variable expansion or caller executable path is admitted. The controller executes
the verified held native ELF object using Linux descriptor-based execution; the
read/identity/exec chain must address the same object, with its complete loader
and dependency closure in the selected read-only runtime view.

Environment is exactly two C strings in this order, followed by NULL:
`LC_ALL=C`, `TZ=UTC`. PATH/HOME, loader injection variables, credentials and other
inherited variables are absent. No environment override supplies configuration.
Configuration is an immutable selected dependency, never a cwd/home lookup.
In this first profile it is the controller's closed selection/launch configuration
plus the parser's compiled policy identity, not an unpassed dynamic parser option.
It binds every descriptor role/limit to the static campaign plan; it may neither
introduce extra argv/env nor change the compiled parser whitelist. Any profile
requiring a dynamic parser configuration input needs a distinct reviewed invocation
version with an explicit input channel.

The selected platform profile fixes non-root UID/GID, empty supplementary groups,
no effective/permitted/ambient capabilities, no-new-privileges, a private empty
working directory, namespace/mount/resource limits and allowed runtime objects.
Maximum wall duration is10 seconds, CPU5 seconds, address space128MiB, stack8MiB
and simultaneously open FDs16, with no child processes. The selected profile may
tighten these limits but cannot remove/enlarge them. This is a normal native
release profile; sanitizer component builds do not masquerade as that runtime.
No network/provider/production database access or subprocess execution is allowed.
If these facts cannot be independently observed, launch is UNDETERMINED, not safe
by assumption. This candidate neither provisions a privileged runtime nor claims
that chmod or environment sanitization establishes confinement.

| FD | Exact permitted use |
| --- | --- |
| 0/1 | Controller-supplied inert input/output endpoints; no governed payload/result |
| 2 | Private bounded diagnostics: category codes only, at most4,096 bytes; no input/configuration bytes or secrets |
| 3 | Descriptor: read-only regular file, offset0, exact descriptor bytes, no additional framing |
| 4 | Input: read-only regular file, offset0, exact input_bytes and input_sha256 |
| 5 | New private empty result file, write-only to child, offset0, maximum4,096 bytes |

Every other child FD is closed at entry. The controller retains separate handles
needed to observe/reload results. Descriptor/input/runtime immutability must be
proved by the qualified custody/read-only-view profile and absence of other
writers for the operation; read-only open flags or same-UID DAC alone are not
that proof. FD/object/access-mode substitutions, unexpected seek positions and
aliased input/output objects deny launch. No parser operation changes source,
authority, journals, leases, membership or financial records.

## P04. Admission predicates and rejected states

Before execution, the controller independently establishes:

1. An explicitly accepted successor/plan/release and valid exact qualification scope;
   preparation authority alone fails this predicate.
2. Descriptor canonicality and every typed dependency's original bytes, source,
   qualification/provenance, role, version and limit; no contradictory identities.
3. Executable/entrypoint/grammar/schema/configuration/platform/supply equality with
   the selected profile and compiled parser whitelist. A native schema requires its
   qualified explicit layout; `CANONICAL_TEXT` cannot substitute for it.
4. Actual argv, environment, executable object, private view, principals, descriptors,
   resource limits and observed input bytes. Authority/profile freshness is observed
   externally, not asserted by a field in this descriptor.

Before and after parsing, selected input/custody identities are bracketed and
compared. Missing observations or uncertain concurrent changes are UNDETERMINED;
established mismatch/corruption is INVALID. An unsupported explicit version/schema/
layout is UNSUPPORTED. No such state may select an old parser, remove a predicate,
repair input, resynchronize malformed records or invent missing evidence.

## P05. Result, exit and failure accounting

The child emits at most one exact canonical native record with the same framing,
these fields in order, no additional bytes, at most4,096 bytes:

| Key | Type/value |
| --- | --- |
| `schema` | `B6S_PB1_RESULT_V1` |
| `version` | `B6S1` |
| `descriptor_sha256` | H of exact input descriptor |
| `input_sha256` | H |
| `input_bytes` | U |
| `schema_id` | R |
| `outcome` | `REPRESENTATION_CHECKED`, `INVALID`, `UNSUPPORTED`, or `UNDETERMINED` |
| `reason` | `NONE`, `DESCRIPTOR`, `INPUT_DIGEST`, `GRAMMAR`, `SCHEMA`, `CONTEXT`, `LIMIT`, `DEPENDENCY`, `RESOURCE`, `IO`, `TIMEOUT`, or `CANCELLED` |

The input hash/count describe the completely observed input; success requires
equality with the descriptor. A negative digest case can retain the actual differing
hash/count as evidence. If complete observation is unavailable, no default hash,
zero count or invented complete child record may substitute for it.

Exit0 requires REPRESENTATION_CHECKED/NONE; exit10 requires INVALID with
DESCRIPTOR/INPUT_DIGEST/GRAMMAR/CONTEXT/LIMIT; exit11 requires UNSUPPORTED/SCHEMA;
exit12 requires UNDETERMINED with DEPENDENCY/RESOURCE/IO/TIMEOUT/CANCELLED/CONTEXT.
No other pairing is valid. If a trustworthy complete result cannot be emitted,
retain partial bytes and process outcome; do not manufacture a result record.

The controller accepts a representation result only after exit0, complete single
record readback, exact descriptor/input/schema binding, post-observation brackets
and the independent observation ledger all reconcile. Exit0 alone is insufficient.
Signals, timeout, OOM, premature EOF, partial/extra output, wrong exit/verdict pair,
missing observation or controller interruption cannot be presented as success.
Pre-launch failure produces a controller failure observation, not a child result.

These codes/records do not confer authority, qualification or permission to BIND.
The qualification assessor separately determines case outcome from original
observations and expected predicates. An UNSUPPORTED negative test may satisfy its
expected predicate while the parser operation itself remains unsupported.

## P06. Replay, recovery and independent examination

Equal descriptor/input/dependency bytes must yield byte-identical semantic child
results; no PID, wall clock or host path enters that result. Original platform,
process, monotonic/civil time, FD/view and syscall/exit observations are separate
per-attempt evidence, not normalized into that semantic result.

Read-only repetitions retain the same scoped invocation intent and use a fresh
attempt UUID, output file and original observation ledger. A crashed attempt
remains crashed; a later success does not overwrite it. No source/journal mutation,
exactly-once business effect or historical replay authority is inferred. Conflicting
reuse of an intent identity with changed governed bytes is rejected by the campaign
controller. Missing recovery facts stay explicit; no automatic state repair occurs.

Independent qualification must establish the first trusted parser/controller/tools
from reviewed source and reproducible selected inputs, independent known-answer
byte vectors and original process/custody observations, then exercise Q01–Q09 and
Q21 in QUALIFICATION.md. A second process/account/agent sharing implementation
custody is not by itself independent acceptance. No original TQ1 check count,
receipt, historical qualification or namespace is inherited by this new profile.
