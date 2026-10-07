# B6 pure attempt-account transitions — Continuation 07

`b6_account_transition_validate` consumes an immutable typed current context, an
already grammar-valid event, and named dispositions supplied by separate validated
evidence producers. It returns a new pure result by value. It performs no journal
IO/replay, runtime checks, action, commit or evidence authentication. The closed
codec and its scalar types are reused unchanged. Only memory-only scalar domain
checks are called by the semantic module; hashing/serialization are fixture-side.

The accepted semantic clarification (SHA-256
`d03c40b9c961059178fdfbb26896fa1da528f5b5348fd58ac15861754b401853`),
through the separate owner acceptance/freeze receipt SHA-256
`d81fe4c75f05ae346df7dcfb89830a5f216feba9913b8b0ad149bf4bd98c057b`,
is the exact successor authority. Its internal proposal labels are preserved in
the frozen bytes; the owner receipt establishes acceptance. The implementation
trace identifies its clauses and the incorporated exact predecessor obligations.

The context keeps provisioning/continuity, commission disposition and immutable
scope, positive account attempt number, separate custody UUID/E assignment,
phase, preparation intent, binding, and historical admission as separate facts.
Reconciliation never clears the latter facts. A retained-history digest identifies
independently validated prior terminal facts; it is not reconstructed here.
UNKNOWN_OR_DAMAGED cannot be substituted for initial provisioning. Provisioning
requires its separate no-prior-obligation evidence disposition. Zero attempt is
limited to GENESIS, registration and never-reserved unused closure.

The next context is a pure accounting proposal. It is not a durable history view
or an assertion that the proposed event was committed. In particular, GENESIS
cannot create a proven history digest. A later independent history producer must
establish complete committed history and its identity before supplying another
normal context. A pure result retains the prior history identity and does not
calculate a journal head, allocate a UUID/E principal, or construct history.

There are 32 separately named external evidence roles. Each has an explicit
UNVALIDATED, VALIDATED or INVALID disposition. They are bound to a candidate
payload identity plus the producer's exact typed event snapshot and matching
validated payload identity. A separate producer must establish those identities
and every asserted predicate; the pure validator compares them without opening
references, parsing reports, cryptographic hashing, or checking their truth.
The header has no generic trusted/authorized flag. These C inputs provide no
security boundary against a caller fabricating values. The fixture models such
producer outputs; it proves no actual process, provenance or authority validation.

A proven invalid source, phase or predicate returns REJECTED. Missing required
state/evidence truth returns UNDETERMINED. Both return zeroed unusable next state
and effects, and leave all three inputs untouched. ALLOWED returns only the
bounded pure next facts and effect; it creates no execution or publication power.
The abstract admission positive fixture assumes separately applicable future
enabling evidence. Current execution DISABLED and BIND implementation stop remain.

ACK_COMMITTED is a separate operation for an exact retained committed event and
its original request/evidence correlation and durability. All typed fields,
original writer/time/sequence, store/commission/attempt, and raw payload identity
must match. It returns current context unchanged, including after a newer phase
or generation. It never reapplies the old event or resends admission. Fresh
repetitions are checked against the closed relation. Terminal commissions never
reopen; failed attempts permit only a distinct deliberate eligible next attempt.

E is consumed forever at separate committed custody BUILD. NO_LINKED_ASSIGNMENT
means this account context has not linked a custody assignment; it does not mean
an E is unconsumed in the custody store. Reservation consumes no new principal:
it links the already committed externally validated assignment. Retry preserves
all old consumption/history and requires a distinct UUID/E and custody release.
All successful transitions and acknowledgments preserve committed consumption;
rejection, failure, reconciliation, retirement and unused closure never reverse it.

New fixtures cover all eleven events and a bounded 22-context / 11-event matrix,
including each coherent retained intent/binding/admission variant of reconciliation,
failed and retired attempts. Positive future admission is a pure modeled case.
Every required evidence role, exact historical acknowledgments, changed identities
and suffixes, stale terminal applications, generation overflow/reuse, authority
window narrowing, unknown tags, determinism, input immutability and result
invalidation are checked under normal and ASan/UBSan builds. Prior grammar evidence
is carried forward because no prior source/header/fixture file changed.
