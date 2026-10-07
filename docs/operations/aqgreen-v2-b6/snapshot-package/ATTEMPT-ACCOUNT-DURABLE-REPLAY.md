# Nonprivileged bounded account history replay

`b6_account_replay` consumes supplied canonical store.identity/journal bytes, a
matching independently validated identity snapshot and historical evidence from a
separate producer. It has no journal IO, write/commit/lock, process/provenance
adapter, clock or execution operation. The exact frozen replay clarification is
accepted by receipt71da32de193abf01e19a55fd18fe1d96e8661f2afc8693cc2272802ce6f6242c.
Its proposed-status text remains unchanged; the owner freeze makes those exact
bytes authoritative. Original ATTEMPT §§H-T and accepted pure semantic §§2-7 remain
incorporated unchanged. This document describes implementation, not new authority.

`b6_replay_history_scope` distinguishes an explicitly historical view from a
candidate complete-current view. The successful result is HISTORICAL_STATE unless
both the candidate scope and separate validated complete-current identity/continuity
proof are supplied. It always labels presence of historical/current context.
Prefix validity, its hash, record count or last event never manufacture completeness.
A stale but valid view with absent/invalid current-completeness proof can describe
historical state only. Proof of completeness comes from the separate authoritative
producer and is bound to the supplied journal digest and store identity snapshot.

All external evidence must be established independently. These C dispositions can
be fabricated by callers and supply no authentication. Every per-record contributor
names original preceding projection, original writer provenance, retained original
sources and original durable commitment, correlated to exact preceding bytes. The
existing transition evidence supplies the remaining precisely named original role
predicates, event snapshot/digest, permit/scope, custody assignment and result facts.
Missing required evidence returns UNDETERMINED; established invalid evidence or
history returns INVALID_HISTORY; neither publishes partial context. No protected
source/reference validation producer is implemented in this module.

At an original event, the validated preceding history supplies the existing pure
validator's complete-history prerequisite AT THAT ORIGINAL POINT, as authorized by
replay clarification§3. The adapter copies the explicitly validated original-point
projection disposition, after matching its prefix digest and replaying every prior
frame, into that legacy field for this invocation only. The incoming historical
transition object must leave `complete_current_history` UNVALIDATED. The independent
complete-current result proof is a different input and cannot be substituted. No
pure-validator source, rule or API changes are needed for this qualified invocation.

The sole event parser and transition function are reused. Replay checks one global
sequence starting1, exact +1/no wrap, prior RAW PAYLOAD digest and store equality.
The first event is GENESIS under the supplied proven ORIGINAL provisioning context;
a zero-byte normal account supplies no usable context. New commission projections
are introduced only by registration after validated provisioning and independently
validated preceding nonenrollment. Separate commission/attempt/intent/bind/admission/
consumption facts are retained; all original record/next-context snapshots remain
available, including older generations and terminal evidence. Cross-commission
resource conflicts are checked against preceding projections before invoking the
same pure validator. A committed UID or generation in an older accepted reservation
cannot reappear; full independent custody BUILD history remains separately required.

Writer/time values are original facts only. There is no present /proc, PID/pidfd,
starttime, output stat or boot lookup. CS0 and decreasing account CS remain valid
when original event predicates validate; no generic monotonicity is added. The
custody journal's independent clock rule is unaffected. Historical BIND and abstract
ADMIT fixture evidence confer no present BIND/execution authority. The latter still
requires its separately applicable enabling-authority disposition, which is modeled
in fixtures and never issued here.

Physical duplicates are not semantic ACK: repeated sequence/chain fails, and a
fresh repeated semantic event goes through the frozen rejection relation.
ACK_COMMITTED is a separate read-only pure API operation, forbidden as a new replay
record. Any malformed/unknown/foreign/partial/corrupt/unauthorized record anywhere
invalidates this entire candidate; no skipping, sorting, resynchronization, valid-
prefix salvage or recovery is performed. A separately supplied complete historical
prefix is different from salvaging damaged bytes. Current reuse is never concluded
from historical pre-consumption state, and replay failure returns no freeing effect.

Caller-owned workspace arrays make resource capacity explicit. No allocator is used
by replay. Outputs own no heap/handle, and inputs are immutable. Valid nonaliasing
workspace is cleared on failure. Capacity/size overflow or input/output alias guards
return an empty result without unsafe writes. A successful borrowed view lasts until
workspace reuse; consumers must retain its scope/presence qualifiers. Identical
inputs and capacities produce field-equivalent results, verified by native fixtures.

Continuation09 adds this module/header/document and its replay fixture only. The
fixture reuses unchanged prior fixture constructors by including its C source with
the old main renamed; it never executes that prior main or baseline suites. Normal
and ASan/UBSan tests cover all11 events/legal edges, meaningful closed-world cells,
prefix/current/consumption/terminal boundaries, historical roles, bad frame SHA and
correct-SHA malformed payloads, tails, physical duplicates, identity/head/sequence,
immutable inputs, determinism, stale-output invalidation, aliases and size overflow.
The producer dispositions in these fixtures are MODELED evidence, not real runtime
or protected-store validation. BIND FAIL-CLOSED, helper ABSENT/UNSTARTED (future125),
six adapters UNSTARTED; no deployment/runtime qualification is claimed.
