# B6S prospective baseline — normative candidate

Version `B6S/1.0.0-candidate.1`. Every MUST below is a **PROPOSED** obligation,
not an adopted runtime rule. Preparation authority does not establish acceptance.

## B01. Identity, scope and authority

1. Successor package identity is the exact manifest digest defined in the index.
   Historical freeze hashes are historical reference identities only. New subjects,
   results and receipts MUST name the successor, their exact source/release and
   scope; they MUST NOT impersonate an original receipt or use an old package pin
   to admit new behavior. TQ1 equivalence remains UNKNOWN.
2. Initial execution qualification is isolated, read-only/test-money and explicitly
   scoped. PB1 v1 accepts only `ISOLATED_QUALIFICATION`. Operational parser profiles,
   production mutations, provider/worker authority, financial cutover and D10/B7
   commissioning are not selected by this candidate.
3. Existing repository authority precedence and document07's decision convention
   govern. Preserved B6 register sectionQ records D03B/D09 decisions; main retains
   older unresolved labels. This draft uses those preserved design records as
   recorded evidence, does not create confirmation, and requires the actual policy,
   scope and exception authority to be bound at operational acceptance. It resolves
   neither D08/D10 nor historical authorization/accounting uncertainty.
4. A digest or parsed `SUCCESS`/`PROVEN`/`QUALIFIED` label authenticates no issuer.
   Actual owner/operator authority, release/supply selection, independent examination
   and current scope must be established outside the subject being evaluated. The
   first parser's trust terminates in independently examined tools, original bytes
   and an explicit examiner/owner decision, never self-qualification.
5. AQGreen analysis remains .NET-native; existing native qualification components
   remain narrow tooling. Core owns domain invariants, Application orchestration,
   EF persistence and Web authentication/HTTP. No new commissioning API is proposed.

## B02. Evidence and semantic integrity

Typed representation/correlation, original observed facts, accepted authority,
qualified execution and authorized business use are separate claims. A consumer
MUST require the exact claim it needs, not promote a weaker result.

- `REPRESENTATION_CHECKED` means only selected grammar/relationships matched.
- `INVALID` means a contradiction or violation was established.
- `UNSUPPORTED` means the explicitly selected version/profile/schema is not supported.
- `UNDETERMINED` means required evidence/observation cannot be established safely.
- `BUSY`, `CONFLICT`, `COMMIT_UNCERTAIN` and `RECONCILIATION_REQUIRED` retain their
  distinct control meanings where a stateful role applies. They are not success.

Unknown/unsupported/corrupt states MUST NOT become zero, an empty business
population, false eligibility, a default timestamp, Level0, `Incomplete`, a usable
authority grant or silent V1/legacy fallback. A legitimate empty set requires a
complete, independently established scope. A selected path's failure stays failure.

Evidence graphs MUST bind role/type/version, exact bytes/size/hash, source/release,
context and independently accepted validator/issuer where applicable. Missing,
duplicate/conflicting, dangling, cross-scope or cyclic references deny qualification.
Per-role limits precede allocation/IO; legacy field names do not infer authority.
New role codecs must have a closed field/variant/limit specification, selected native
source mapping and independent evidence oracle before implementation; this baseline
does not supply those missing broader codecs by inference from retained C tables.

## B03. Source, build and supply

The release must bind the exact reviewed Git object and first-party source/modes,
approved dependency/toolchain inputs, clean materialization, configuration, executable
and full materially used runtime closure. Git OIDs and artifact SHA-256s are different
typed identities. Live worktree paths and generated candidate manifests are not
source authority. Two clean builds compare the declared deterministic outputs;
declared path-bound test metadata is not silently normalized into runtime evidence.

Retained native/library source and tests are reusable at their existing scope.
Compiled historical S1/AS1/SS1/AR1/TQ1 selections MUST NOT be repinned to B6S hashes.
Successor dispatch requires a distinct schema/profile and explicit release; unchanged
legacy parsers remain representation tools. Actual selected supply must bind its
source, native format/ABI, acquisition/custody, qualified decoder and full dependency
closure. The source inventory is not that selection or qualification.

## B04. Producer and historical/current separation

A complete producer must retain exact original sources and role-specific raw
observations, prove their origin/custody and derive each predicate through the
selected source/validator. It must distinguish historical prefix validity from
complete-current evidence, immutable acquisition origin from a later pre-call
observation, and original durable effect from present projections.

No current `/proc`, mutable Customer/Area/security state, payment confirmation or
newly generated data may manufacture an old approval, root, acquisition, commitment
or financial fact. Unavailable original facts remain UNKNOWN/exception evidence.
The producer cannot grant its own issuer authority or accept its own qualification.

## B05. Journal, recovery and publication

The accepted account frame/transition/replay foundation remains byte-unchanged for
its historical namespace. A separate successor publication/controller namespace
must bind store, generation, transaction, request and exact before/after heads to
its selected event/record codecs. Cross-namespace conversion needs explicit review.

Proposed control sequence: validate current scope → retain PREPARED intent → execute
only the named operation → retain original IO/effect observations → synchronize
required files/directories → atomically publish immutable result/commit → compare
freshly reloaded bytes/heads → retain ACK. No early PASS or ACK may substitute for
the actual durable boundary. Exact ordering/atomic commit points for a role require
its reviewed codec/native operation profile; retained filesystem primitives alone
do not supply a complete controller.

Qualified fixed store descriptors and identity/custody are prerequisites. Reuse the
retained original-process G/C/A/J lock order/nonblocking discipline where selected;
content verification occurs under the same protected scope. Lock names/history
are not deleted. Fork/exec/FD ownership cannot leak or upgrade borrowed guards.

Duplicates with identical scoped intent/result must be detected; contradictory
reuse fails. After crash, timeout or uncertain effect: preserve prepared/partial
bytes, determine actual durable state using original evidence, and quarantine when
it cannot be established. Never blindly repeat mutation, erase history, salvage a
corrupt prefix as complete, or report an uncertain operation as unapplied.

The publication root must be immutable and content-addressed, bind the authenticated
request/context plus every required constituent and transitive typed reference,
and be independently reopened/reloaded before admission. A caller hash bag, stale
head pair or in-memory pointer does not prove publication, custody or completeness.

## B06. Helper, BIND and adapters

The helper is a separately qualified narrow read-only verifier of the selected root
and scope. It accepts only a specific current permit binding caller/helper release,
principal, stores, request, generation, transaction, boot and validity window. It
must not become a generic privileged command, path reader, repairer or issuer.

BIND admission requires current externally grounded authority, qualified releases
and supply, complete original/current producer evidence, committed/reloaded root,
matching current protected heads, fresh qualified resource/handle observations and
unexpired exact-scope permission in one stabilized admission window. A fact verifier
does not authorize execution. Admission and durable execution/consumption facts
remain distinct; uncertain execution is reconciled, not automatically repeated.

All six adapters remain separately qualified operations:

| Operation | Required effect and evidence boundary |
| --- | --- |
| CREATE | Establish only the scoped owned capsule from proved absence; preserve collision/uncertain state |
| POPULATE | Copy only the selected source closure and compare exact output under origin/custody brackets |
| PROTECT | Establish declared DAC/content sealing and no retained writable handles; never claim hostile-root immutability or confinement from chmod |
| PIN | Retain only qualified existing handles with proved acquisition/ownership/lifetime; no physical namespace/HOLD/pin provisioning is inferred |
| VERIFY | Recompute facts, root/current-head/request association under the protected scope; issue no execution/financial authority |
| RELEASE | Close only proved-owned leases/handles; preserve source evidence, journals, locks, accounting and terminal uncertainty |

Each needs an exact role codec/native operation profile, original observations,
permit/deadline and separate durable adapter-ledger/confirmation/recovery semantics.
The current envelope correlator is not any live adapter. Existing helper/harness
refusals remain until their own accepted release and qualification replace them.

## B07. Reconciliation, history and D10

Tenant is the hard data/topology boundary; Area is same-Tenant administration and
approval scope. Sponsor attribution is not placement. Structural topology is not
financial eligibility. Payment verification does not activate participation; only
authorized Area approval performs activation/placement under existing transactions.

Preserve Customer11's exact accepted artifacts, bounded outcomes and UNKNOWN facts.
No successor relabels or extends its six-placement result to another population.
Preserve V1 payments, approvals, commissions, releases/payouts, terms and accounting
exceptions. An analysis cutoff is not V2EffectiveAt. No prospective result resets
history or grants migration/apply permission. Approved representative/final/applied
evidence and D10's actual Tenant/Area/week/capability/recovery/provider/worker authority
remain separate gates, specified in QUALIFICATION.md.

## B08. Finite owner decisions and implementation boundary

| Decision | Exact proposed scope | Present state |
| --- | --- | --- |
| S01 | Adopt this exact new package for isolated successor qualification preparation/implementation scope; preserve historical namespaces and unknown compatibility | PENDING owner approval |
| S02 | Accept PB1's native read-only profile, wire/identity/admission/verdict rules and limits as proposed | PENDING owner approval |
| S03 | Require selected explicit codecs/native profiles for broader roles; authorize no invented authority/capture format or implied equivalence | PENDING owner approval |
| S04 | Select actual source/release/tool/platform/supply/custody and independent examiner/observations for a named campaign | NOT SELECTED; later actual inputs |
| S05 | Accept representative population, root/exception/final reconciliation evidence and any separately authorized apply scope | NOT SUPPLIED; later data/acceptance gate |
| S06 | Authorize D10/production financial/provider/worker commissioning | OUTSIDE this candidate; remains closed |

S01–S03 are the candidate-adoption decisions. Actual S04/S05 acceptance does not
follow from contract adoption. Documentation merge, hashes, local tests and
same-agent review confer none of these decisions. No undefined broader role may be
implemented just because PB1 becomes accepted. The first bounded implementation
step after S01/S02 is the new PB1 descriptor codec/admission boundary and isolated
entrypoint fixture; actual qualification waits for S04 and its independent evidence.
