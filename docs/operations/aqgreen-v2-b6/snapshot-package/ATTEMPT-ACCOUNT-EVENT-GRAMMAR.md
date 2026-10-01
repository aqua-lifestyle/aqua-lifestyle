# Typed attempt-account event/frame grammar — Continuation05D

The disposable new-lineage candidate implements a pure in-memory codec for exactly
one B6_ATTEMPT_ACCOUNT_EVENT_V1 frame in attempt-account-schema.c/.h. Its closed
11-event enum selects an exact suffix union. The API owns all returned storage,
allocates no dynamic memory, and clears the entire output on every failure.
No journal, state transition, process, output-object or authority-validation API
is implemented by this codec. GRAMMAR_ONLY is its sole success state.

b6_syntax_parse_event_frame accepts one canonical positive-length/TAB/lowercase
SHA256/LF header followed by exactly the declared canonical ordered ASCII payload.
The payload is at most 16384 bytes; the derived whole-frame bound is 16455 bytes.
Each of the twelve common records and the selected suffix is mandatory. Unknown,
missing, duplicated, reordered, extra, noncanonical and malformed input fails;
there is no schema probing, event alias, case folding or normalization.
The checksum is local payload integrity only; previous_event_sha256 is retained
without chain/history validation.

b6_syntax_serialize_event_frame validates the selected typed shape before exposing
canonical bytes. Canonical input -> typed frame -> serialization is byte-identical.
Use supplied readable spans, and do not alias inputs with outputs. All strings have
bounded owned storage; serializers reject unterminated active fields.

Authority is ATTEMPT §I, exact accepted account grammar §§2–3, the common-domain
clarification §2 and the suffix-domain clarification §2. Exact identities, positions,
terminal domains, applicability clauses and freeze receipts are in successor
continuation05d-evidence/V2-SYNTAX-INVENTORY-V7.tsv. V6 and earlier inventories remain
unchanged historical evidence. Fixed-generation successor eligibility remains a
separate required semantic predicate; the codec does not select governing sets.

writer_pid, supervisor_pid and bash_pid use canonical decimal 2..2147483647.
writer_starttime, supervisor_starttime, bash_starttime and output_inode use U64+.
recorded_boot_cs uses CS and permits zero; output_device uses U64 and permits zero.
Recovery references reuse exact REF grammar without lookup or dereference.
GENESIS retains its exact previous-head, STORE and NOT_APPLICABLE value exceptions.

ATTEMPT §I supplies a conditional attempt_number=0 representation, whose use is
lawful only for commission/store events where there genuinely is no individual
attempt. The codec preserves this representation; grammar success does not prove
that condition. §I does not enumerate an event-to-zero applicability map. No such
map or state/history predicate is inferred in this grammar module. A later semantic
validator must reject any use lacking the required nonattempt evidence. event_seq
remains U64+; its initial value/increments/history are likewise later validation.

The local payload SHA uses the already installed OpenSSL libcrypto (3.0.13 here).
Standalone builds must link -lcrypto. No package installation was performed.
From the existing implementation workspace, run:

```bash
bash continuation05d-evidence/run-focused.sh
```

That versioned Bash script records exact normal and ASan/UBSan compiler/execution
commands and rebuilds all three causally affected prior fixtures as well as the new
b6-event-frame-fixture.c. Normal and sanitizer runs execute the same cases, counted
once. Evidence is bounded nonprivileged C fixture evidence; it establishes no real
process identity, account semantics, journal semantics, authorization, BIND,
recovery validity, retirement, qualification or production behavior.

The next dependency is exact pure-account lifecycle/transition authority, including
current state + typed event -> allowed/rejected + next state, generation effects,
permanent consumption, terminals and reconciliation, only where frozen authority
supplies each rule. This continuation implements none of those predicates.
