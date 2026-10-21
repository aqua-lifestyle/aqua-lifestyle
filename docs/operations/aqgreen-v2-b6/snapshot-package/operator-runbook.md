# Operator runbook — AQGreen B6 representative snapshot

**V4 compatibility candidate:** PostgreSQL18.4/180004 and the custodian-observed
54-migration schema ending at `20261005194901_AddIsolatedAQGreenPilotEvidence`.
Use only a separately reviewed/qualified release of these exact source changes;
V3/PG16 qualification does not transfer. Existing operator authority, protected
Zone2/Zone3 custody, frozen build/supply and privileged-bootstrap gates remain.

> **DO NOT RUN WITHOUT PRODUCTION/DATA AUTHORIZATION.** This is a template for an
> authorised DBA/platform operator. D03B/D09, repository content, and disposable
> test success do not grant access.

## Pre-extraction checkpoint

### New-lineage native candidate boundary

The fresh native process-group candidate is PRECOMMIT_SYNTHETIC_VALIDATION only.
Its source/build/ELF identities come from its new manifest, not lost historical
artifacts. Application and real qualification remain unauthorized. Preserve the
base `authoritative-runner.sh` bytes (SHA-256
`5ed57d64c74014002708dc679d5d26e9d7b53e888d1124dce3a74432e16a6244`).
Do not modify Gate-Q/Gate-A policy or mistake ordinary-user native fixture success
for operational gate execution.

For a separately authorized future installation, the native binary has frozen
package/output-parent locations recorded in its build manifest. These must be
ordinary-user-owned private0700 directories, with trusted non-symlink single-link
regular package files and a new absent output leaf. The only launch boundary is
static BusyBox `env -i PATH=/usr/bin:/bin LC_ALL=C` followed by the reviewed native
ELF and exact `--output-dir` argument. Do not launch through caller-controlled
dynamic helpers or supply a replacement harness, trust identity or environment.
BusyBox is not a trap/wait supervisor. Native owns the direct Bash child, signalfd
acceptance, pidfd, authenticated READY and terminal COMMIT.

Numeric process-group forwarding is authorized only by exact unreaped pidfd/direct
child identity plus authenticated READY, PID=PGID and Bash monitor OFF. Before
READY, only exact-child pidfd cleanup is allowed. After consuming reap, group
authority is permanently closed. A successful kill is not acceptance or proof of
universal delivery/termination. INT/TERM consumed after reap can change arbitration
before COMMIT, never forwarding. Successful no-replacement rename publishes the
immutable terminal result; no subsequent signal may change receipt or exit status.

Generic post-READY failure must never force-kill Bash alone. Native requests
cooperative TERM on the authorized unreaped group, then reaps exact Bash and closes
group authority. Cleanup TERM is not lifecycle acceptance: accepted TERM143 >
accepted INT130 > infrastructure125. Publication failure stays UNCOMMITTED.
Native does not discover members or prove their disappearance. The external
known-member observer retains PID/starttime, verifies PGID before failure and
checks response/disappearance/residue, including after Bash reap if necessary.
No post-reap group signaling, new timeout or force escalation is authorized.
TERM-resistant programs remain a limitation, not a universal cleanup guarantee.

Do not accept stdout PASS or child proof alone. Require strict child-proof/native
terminal/external-wait reconciliation and disappearance/residue checks. Missing,
duplicate, malformed or contradictory fields reject the entire result. The README
defines the 36-field terminal grammar. Never default missing evidence to success.
Abrupt parent death before COMMIT remains UNCOMMITTED, even if child proof exists.

Installed Bubblewrap `0.9.0-1ubuntu0.3` remains forbidden and corrected approved
identity unresolved. The production harness deliberately refuses qualification.
`--new-session` escapes Bash's group: separate bounded session cleanup and exact
launcher/disappearance/residue evidence is mandatory. Private synthetic fixtures
cannot authorize real Bubblewrap, package/policy changes, or Attempt 21.

### Existing extraction authorization

Responsible Aqua personnel must record the processing purpose, minimum allowlist
and Tenant scope, authorised operator, secure destination, access principals,
retention/destruction treatment, and whether Information Officer/privacy or other
regulatory/prior-authorisation review is required. This is a non-legal operational
checkpoint; the tooling does not determine POPIA or other legal compliance.

Production/data-owner and privacy/security authorization are distinct from B6
business-policy approval. DBA/platform authorization must separately name the
source, least-privilege role, window, load limits, observation-cutoff approach,
Zone 2 path, abort/recovery procedure, and Zone 3 destination.

## Three zones

1. **Zone 1 — production:** authoritative raw data; only the authorised operator
   connects, with a `CONNECT`/`SELECT`-only role.
2. **Zone 2 — operator sanitisation environment:** temporary controlled processing.
   It sees the minimum raw identifiers needed to create pseudonyms and ordering
   metadata. The key and any mapping stay access-controlled here, outside Git,
   logs, reports, and the handoff.
3. **Zone 3 — B6 analysis environment:** receives only the pseudonymised,
   data-minimised CSV set, metadata, manifests, reviewed root dispositions, and
   receipt—no direct identifiers, key/mapping, production credentials, Aqua
   application workers, or provider configuration.

The data owner must specify retention/destruction triggers for Zone 2 raw files,
key/mapping, Zone 3 snapshot, reports, and backups, and retain destruction evidence
under Aqua policy. This package does not invent a corporate retention period.

## Exact acquisition

The safest practical option is **Option A**: an authorised operator first creates
one transactionally consistent copy/restore inside the controlled operator zone,
then runs this allowlist package against that copy and releases only Zone 3 output.
It best confines production access, preserves PostgreSQL fidelity and referential
integrity, and supports repeatable extraction, at moderate restore/storage burden.
**Option B**, running the same single-transaction allowlisted extractor through a
production read-only role, minimises raw columns and avoids a broad copy but has
greater live-source and bespoke-export operational risk; use it only if separately
approved. **Option C**, aggregate-only inventory, has lowest disclosure but cannot
reproduce participant graph/simulation and is not a representative snapshot. The
parent specification records the detailed security, exposure, fidelity,
referential-integrity, repeatability, and burden comparison.

`allowlist.json` is the export contract. The operator must supply one explicitly
authorised Tenant and the versioned
`ALL_TENANT_AQGREEN_PARTICIPATIONS` population scope. Every extraction query
mechanically starts from that Tenant's AQGreen participations and includes only its
minimum necessary referential closure: their Customer/recruiter/Area identity
guards, corrections and approval decisions, relevant attribution confirmations
and MemberInvitation evidence, existing V2 conflicts, and historical
commission/loan/graduation/Onyx rows intersecting those AQGreen participations.
Unrelated Tenants, customers, invitations, Onyx participations, commissions,
loans, and graduations are excluded. Each field records purpose, classification,
transform, type, and why it is required. Section G of the parent
[specification](../representative-snapshot-specification.md) and the allowlist deny
patterns exclude direct identifiers, free text, provider/payment data, unrelated
products, and combo data.

Operational evidence starts from an operator-selected full reviewed Git commit,
not a mutable live-worktree executable or script. The operator-controlled shell
streams the exact `authoritative-runner.sh` blob from that commit through trusted
Git into Bash with replacement objects disabled. That already-reviewed bootstrap
creates one private Gate-Q checkout plus two independent Gate-A lane checkouts and
binds the exact frozen toolchain inventory, U215 feed and native-restored bytes,
parent-produced online trust evidence, repository-signer policy, four locks,
and separate G197/TQ186/U215 manifests. Gate Q runs the 61 filtered xUnit/VSTest
cases and existing TRX verifier in Bubblewrap without external networking. Gate A
performs two B1-R/B2 lanes, compares the complete runtime trees, and executes the
existing `simulate` command from each exact B2 Runner. The complete A/B output
bytes must remain identical. The Runner then validates the exact output schema,
the frozen `B6GateASemanticProjectionV1` semantic digest, independently supplied
source/B2 provenance, and the recomputed simulator digest. The complete output
SHA is retained as current-transaction evidence. The historical complete output
SHA remains historical evidence only: source-specific commit, assembly, and
evidence-classification values are not frozen as universal semantic truth. Gate A
publishes and verifies
qualification evidence before operation release. After the operation it completes
transaction-critical cleanup/reconciliation, resolves one terminal result, and
binds that result into the terminal receipt and final verified manifest. Removal of
the owner-only temporary root is post-result best-effort housekeeping and cannot
change the bound process result.

This is a trust-before-sensitive-operation boundary. Trusted operating-system
process isolation, the operator-controlled invocation and `PATH`, Git executable
and object database, Bash/core utilities and SHA-256 implementation, and the .NET
SDK/MSBuild/NuGet build base are the minimum trusted computing base. Mutable source
repository hooks, `info/attributes`, checkout filters, replace objects, ignored files,
and live worktree contents are explicitly outside that base. The exact U215 feed,
installed package root, and feed manifest are mandatory inputs. The bootstrap
itself generates the transaction ID, native verification logs, ledger, promoted
U215, and receipt, then derives a separate G197 filesystem source. Restore receives no
production/representative DSN, snapshot location, output authority, pseudonym key,
provider secret, or B6 test sentinel. The pre-authority environment variables
supplied to the Runner bind the selected commit and hashes for evidence; they do not
make a live script trusted.
Ignored, modified, or stale live-worktree source/build state is never selected.
Dirty development execution remains only `PRECOMMIT_SYNTHETIC_VALIDATION` and must
not be promoted as operational evidence.

Dependency changes first require a separately approved **B1-Q resolution
qualification**. B1-Q uses the reviewed SDK/NuGet/runtime and source policy to
generate candidate native `packages.lock.json` files, assets, canonical graph, and
the existing raw/archive/content/nuspec/tree/provenance evidence. It runs only for
an approved dependency declaration, toolchain, source-policy, refresh, or other
dependency-resolution event. Generated locks and resolved graphs are candidates,
not authority. Separate review must approve their exact hashes and graph before
B1-R.

Normal operation does not rerun B1-Q. **A-L routine locked acquisition/trust**
starts from U215, the exact locks, frozen toolchain, and approved signer policy.
It uses network authority for native acquisition and online signature/revocation
validation, binds exact bytes in the ledger, promotes only those bytes, then
terminates network authority. U215 remains acquisition authority only; G197 and
TQ186 remain separate selection authorities. Replay keeps signature verification
enabled and uses offline revocation only as an execution control.

The fresh ledger is produced by the parent transaction, never supplied by the
operator. Every one of its 215 rows carries the same generated transaction ID, one of
the three approved repository-signing leaf fingerprints, the exact signer-policy
and frozen-toolchain identities, identities equal to the approved feed row, and the
hash of its retained complete native NuGet log. The parent receipt binds all logs,
the ledger, promoted bytes, process starttime, boot ID, civil time, and monotonic
completion. Do not relabel or supply a historical ledger.
The signed package/log supplies repository-service-index provenance. Installed
`.nupkg.metadata` truthfully records the custody or private replay source used by
that restore; do not rewrite it to resemble an external acquisition.

For first adoption, an explicitly approved B1-Q may regenerate the candidate locks
and compare them byte-for-byte with the repository copies. Once approved, the locks
are recurring inputs; B1-Q runs again only after a legitimate dependency-authority
trigger.

Supply the exact flat acquisition feed and feed manifest as separate canonical
inputs. The bootstrap takes private custody, uses native locked restore to create a
fresh installed U215 cache, performs online native verification, promotes and
rechecks all 215 byte identities, then derives G197-only material for B1-R.
Acquired U215 does not become resolution authority merely by being present.

The reviewed bootstrap validates the selected hierarchy and complete package
material and records ID/version/`.nupkg` SHA-256 manifests. It derives separate
read-only U215 and G197 sources, runs Gate Q, then gives B1-R A and B independently
fresh empty global-packages caches and fresh assets. Each uses native
`dotnet restore --locked-mode`, the four approved lock hashes, and the explicit
private source; warnings, errors, NU1603, lock mutation, missing/extra packages, or
a non-G inventory fail closed. The external seed and private source must remain
unchanged. The existing C# graph tooling must independently establish that fresh
assets represent exact G197; path-bound raw assets bytes are not conflated with the
canonical graph.

B2 is a separate `dotnet build --no-restore` stage over the B1-R result and G197
only. It must not change locks, assets, or package inventory. The bootstrap sets
`DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true` to suppress workload advertising
updates as configuration hygiene. Neither that variable, locked mode, no-restore,
source clearing, nor proxy/environment sanitisation proves network absence. The
bootstrap retains the established Bubblewrap mechanism and requires the confined
child's effective network/capability witness before package consumption. Its
filesystem view is deliberately narrow: required system runtime files, the public
certificate store, the frozen toolchain, read-only authority inputs, and only its
own explicit writable lane/output paths. Parent witness/evidence, sibling lanes,
future authority inputs, and the transaction root are absent. The parent reconciles
the Bubblewrap launcher and namespace child by PID/starttime, exact single-row
UID/GID maps `65534 0 1` and `65534 0 1`, respectively,
groups, all capability sets, `NoNewPrivs`, network namespace/interface/routes,
exit status, and cleanup before accepting its own witness record.

The reviewed bootstrap requires effective UID 0 and GID 0 before launching any
Bubblewrap lane. On this path Bubblewrap 0.9.0 creates an intermediate setup user
namespace and then a distinct final workload user namespace. In the final namespace,
each map is `sandbox identity -> identity in Bubblewrap's immediate parent user
namespace`; thus `--uid 65534 --gid 65534` produces the final literal maps
`65534 0 1`. The parent deliberately reconciles the final namespace init and final
workload child, rather than mistaking Bubblewrap's intermediate process or setup
namespace for the witnessed workload. A bootstrap UID/GID other than 0 fails closed;
it is not substituted into the contract. The privileged qualification invocation
remains separately human-gated.

During ordinary execution, one stable SIGINT/SIGTERM handler records the first
requested terminal code, 130 or 143, without becoming a second transaction engine.
The EXIT-owned terminal transition performs transaction-critical cleanup first:
required Bubblewrap termination and reaping, child PID/starttime reconciliation,
namespace cleanup, network-status reconciliation, and transaction-state closure.
Any failure in that class prevents PASS. It then suppresses INT/TERM, consumes the
recorded signal exactly once, and fixes the final result and exit code.

Terminal evidence is built in the private transaction root. The final manifest is
published and verified against the staged receipt before `final-verdict.tsv` is
atomically published as the terminal commit point. Receipt staging failure,
manifest staging or publication failure, manifest verification failure, or receipt
publication failure therefore leaves no authoritative PASS receipt. Only after the
receipt commit may the runner print `B6_QUALIFICATION=PASS`; it then exits with the
exact code bound by that receipt. The receipt binds the transaction ID, final phase,
qualification/operation/cleanup and final results, process exit code, UTC terminal
timestamp, qualification-manifest identity, and the final-manifest relationship.

After terminal commitment, only best-effort housekeeping remains: restoring write
permission on, and removing, the disposable private transaction root. Housekeeping
cannot rewrite the receipt or manifest, change the bound result or exit code, or
turn a committed PASS into failure. INT/TERM remain suppressed through this bounded
post-commit section and process exit. Before commitment the EXIT handler may perform
the transaction-critical cleanup above, but it never manufactures a successful
receipt after terminal publication fails.

NuGet owns one lock and one assets decision per restored project. The project-local
Core, EntityFrameworkCore, and Runner locks govern their corresponding assets and
all three exact hashes are mandatory. A missing project lock is rejected by the
bootstrap before NuGet runs; `--locked-mode` alone is not treated as that gate.
Because Core and EF are also used by wider solution workflows, ordinary restores
will consult those locks and can expose a proposed lock change when a dependency
declaration changes. Do not accept that change as routine output; route it through
B1-Q.

Within each A/B lane, B1-R and B2 use the same namespace-visible source checkout,
package cache and artifact coordinates, matching the path-bound values in
`project.assets.json`. Distinct host directories do not create authority. If B1-R
and B2 are placed in separate Bubblewrap children, bind their state to the same
reviewed namespace-local coordinates and verify the assets `projectPath` and
`packagesPath`. Compare canonical graphs across A/B unconditionally; compare raw
assets bytes only when their namespace-visible coordinates are identical.

The trust-freshness limit is 60 minutes from completion of the last successful
online validation to completion of the final package-consuming Gate-Q/B1-R
operation. This is an approved B6 governance heuristic, not a NuGet, PKI, or
cryptographic validity period. Enforcement uses parent-observed monotonic elapsed
time bound to the same process, boot, and transaction; UTC civil time remains audit
evidence and reversal or material discontinuity fails closed. The exact rule is
elapsed `<= 3600 seconds`:
3599 and 3600 pass, 3601 fails. PASS or FAIL evidence is atomically written before
exit. A NuGet.org repository-signing certificate rotation also fails
closed. A new leaf fingerprint requires explicit human authority and the relevant
acquisition/TQ-Q or B1-Q review; it is not accepted merely because the publisher
or repository name is unchanged.

The extractor starts one PostgreSQL `REPEATABLE READ READ ONLY` transaction,
records the approved `AnalysisSnapshotCutoff`, captures `transaction_timestamp()`
as separate source-observation evidence, and rejects a cutoff later than that
observation. It runs only `SET LOCAL` and parameterized allowlisted `SELECT`
commands through Npgsql, then rolls back. It neither discovers arbitrary
tables nor runs production `CREATE`,
`UPDATE`, `INSERT`, `DELETE`, `TRUNCATE`, `ALTER`, or `DROP`. Independently timed
exports are prohibited. If later parallelised, one coordinator must use
`pg_export_snapshot()` and every worker must import that snapshot before querying;
V1 intentionally uses one connection. Broad `pg_dump` is prohibited because it
would over-export disallowed columns.

Supply placeholders only through the approved secret channel—never Git, prompts,
reports, or shell history:

```bash
export B6_SOURCE_DSN='<authorised read-only DSN>'
export B6_SOURCE_SYSTEM_ID='<approved opaque source-system ID>'
export B6_RAW_ZONE2_DIR='<new secure Zone-2 path>'
export B6_PSEUDONYM_KEY_FILE='<Zone-2 key path>'
export B6_ZONE3_DIR='<new approved Zone-3 path>'
export B6_SNAPSHOT_ID='<approved opaque ID>'
export B6_TENANT_ID='<explicitly authorised TenantId>'
export B6_ANALYSIS_SNAPSHOT_CUTOFF='<approved UTC observation cutoff>'
repo_root="$(pwd -P)" # run from the repository root
reviewed_commit='<reviewed full lowercase Git SHA>'
frozen_dotnet_root='<approved absolute frozen 8.0.130 dotnet root>'
toolchain_manifest='<approved absolute frozen-toolchain manifest>'
acquisition_feed='<exact flat U215 acquisition feed>'
feed_manifest='<exact U215 feed manifest>'
evidence_dir='<new external evidence directory>'
set -o pipefail
GIT_NO_REPLACE_OBJECTS=1 git -C "${repo_root}" show "${reviewed_commit}:docs/operations/aqgreen-v2-b6/snapshot-package/authoritative-runner.sh" |
  bash -s -- --repository "${repo_root}" --reviewed-commit "${reviewed_commit}" \
    --frozen-dotnet-root "${frozen_dotnet_root}" --toolchain-manifest "${toolchain_manifest}" \
    --acquisition-feed "${acquisition_feed}" --feed-manifest "${feed_manifest}" \
    --evidence-dir "${evidence_dir}" operator-extract
```

All destination directories must be new paths beneath an already trusted,
operator-owned `0700` directory. Package entry points apply `umask 0077`, require
directories to remain `0700`, create files as `0600`, and reject existing output,
symlink, or non-regular targets. Source credentials are read from the named
environment variable only; they are never copied to argv or output and must not be
logged. A Zone 3 ownership handoff must make the receiving analysis OS account the
owner while preserving these modes; shared group/world access is not a supported
handoff. Never enable shell tracing around these commands.

Generate the Zone 2 key, rather than typing a placeholder:

```bash
repo_root="$(pwd -P)"
reviewed_commit='<reviewed full lowercase Git SHA>'
frozen_dotnet_root='<approved absolute frozen 8.0.130 dotnet root>'
toolchain_manifest='<approved absolute frozen-toolchain manifest>'
acquisition_feed='<exact flat U215 acquisition feed>'
feed_manifest='<exact U215 feed manifest>'
evidence_dir='<new external evidence directory>'
set -o pipefail
GIT_NO_REPLACE_OBJECTS=1 git -C "${repo_root}" show "${reviewed_commit}:docs/operations/aqgreen-v2-b6/snapshot-package/authoritative-runner.sh" |
  bash -s -- --repository "${repo_root}" --reviewed-commit "${reviewed_commit}" \
    --frozen-dotnet-root "${frozen_dotnet_root}" --toolchain-manifest "${toolchain_manifest}" \
    --acquisition-feed "${acquisition_feed}" --feed-manifest "${feed_manifest}" \
    --evidence-dir "${evidence_dir}" runner \
    generate-key --output '<new Zone-2 key path>'
```

This creates 32 CSPRNG bytes. Sanitisation rejects weak/all-zero or shorter keys.
The HMAC-SHA-256 key stays only in Zone 2 and is absent from Git, snapshots,
manifests, receipts, stdout, and ordinary logs. Any pseudonym collision is fatal;
there is no counter-based repair.

`ParticipantSnapshotKey` is a deterministic namespaced keyed-HMAC UUID used
consistently for a participation and every sponsor, placement, commission,
graduation, loan, and Onyx FK. Acquisition evidence uses a shared namespace so a
retained `MemberInvitation.SourceReferenceId` resolves to its AQGreen invitation;
`AuthorisedDirectAdmission` uses its production-valid actor/reference shape and
does not require an invitation. Namespace identity remains explicit in the
allowlist, and collisions across delivered identifier domains fail closed. The key
is never delivered.

`LegacyOrderingTieBreakOrdinal` is independent. Before removing raw IDs, Zone 2
ranks original Participation UUIDs per Tenant with the exact original immutable-ID
comparator. Zone 3 uses `QualifiedUnderRecruiterAt`, then this ordinal. A
pseudonymous UUID is never used as the ordering tie-breaker.

## Verification and handoff

The extract path emits and verifies a `BASE_SNAPSHOT` manifest. Before analysis,
an authorised B6 root review supplies a root-disposition document. A recruiterless
row is only `RootCandidate`; `AcceptedRoot` requires its snapshot participant key,
`ReviewedLegacyRootEvidenceV1`, an evidence SHA-256, controlled decision reference,
review time, and `AuthorisedB6RootReviewer` role. Independent start, a recruiter-
clearing correction, or `LegacyUnknownAcquisition` is never root acceptance.
The disposition document must also copy the verified base manifest's `SnapshotId`,
`overallSnapshotDigest` as `SourceSnapshotDigest`, and `manifestHash` as
`BaseManifestDigest`. Missing or cross-snapshot review evidence quarantines or
rejects the component; it is never silently rebound.

Run `run-analysis.sh` for Zone 3. It verifies the base manifest, creates and owns a
unique loopback-only digest-pinned PostgreSQL 18.4 container with generated credentials, restores
the bounded snapshot, and runs `verify-db`. It accepts no destination DSN,
verifies its container labels/nonce and database identity, rejects source/destination
identity equality, and cleans up only that verified package-owned resource. The
precise worker claim is limited to: the package-owned Postgres-only runtime has no
Aqua application worker attached by the package. The tool does not attest arbitrary
external worker state. Npgsql reads generated credentials only from the verified
owner-only runtime state; credentials are never command arguments.

The default Docker bridge permits outbound connectivity. That blocks use
with representative controlled data until the approved Zone 3 environment enforces
and verifies no outbound connectivity. V4 pins its PostgreSQL18.4 image digest;
that pin is not proof of network isolation or approval of a custodian environment.

Verification rejects missing/extra tables or fields, prohibited field names,
unknown versions, absent cutoff, bad keys/ordinals, broken or cross-Tenant
relationships, invalid attribution source shapes, row/digest mismatch, and manifest
tampering. It also rejects `AnalysisSnapshotCutoff > SourceObservedAt`. Sponsor/root components are constructed before exceptional ancestors
are classified; any topology-affecting ancestor quarantines its whole component
without removal or re-parenting. Missing/contradictory approval evidence is an
explicit anomaly even when current status is Active.

The restore writes a package-owned snapshot-authority marker containing the
SnapshotId, source snapshot digest, and base-manifest digest. Inventory refuses a
schema restored under different authority, verifies the base again, and hashes the
exact repository inventory SQL because that external query remains executable
semantics. The inventory artifact and final manifest bind its rules version,
semantic version/digest, query digest, result digest, and complete digest.

`AnalysisSnapshotCutoff` means only “legacy state observed at this instant.” It is
not `V2EffectiveAt`, V2 activation, D10, financial authority, or consumer selection.

The finalizer computes a `FINAL_ANALYSIS` manifest from the verified snapshot,
verified inventory, reviewed root dispositions, three D09 populations, D03B
components, fixed simulator input/output, and impact analysis. It does not accept
caller-provided authoritative digests. Final verification independently reruns the
inventory and that chain; impact reporting recomputes it.
Create a receipt only after final verification by setting:

```bash
export B6_SNAPSHOT_DIR='<approved Zone-3 snapshot path>'
export B6_BASE_MANIFEST='<verified BASE_SNAPSHOT manifest path>'
export B6_ROOT_DISPOSITIONS='<reviewed root-disposition path>'
export B6_ANALYSIS_OUTPUT_DIR='<new owner-only output path>' # optional
repo_root="$(pwd -P)"
reviewed_commit='<same reviewed full lowercase Git SHA>'
frozen_dotnet_root='<same approved absolute frozen 8.0.130 dotnet root>'
toolchain_manifest='<same approved absolute frozen-toolchain manifest>'
acquisition_feed='<same exact flat U215 acquisition feed>'
feed_manifest='<same exact U215 feed manifest>'
evidence_dir='<new external evidence directory>'
set -o pipefail
GIT_NO_REPLACE_OBJECTS=1 git -C "${repo_root}" show "${reviewed_commit}:docs/operations/aqgreen-v2-b6/snapshot-package/authoritative-runner.sh" |
  bash -s -- --repository "${repo_root}" --reviewed-commit "${reviewed_commit}" \
    --frozen-dotnet-root "${frozen_dotnet_root}" --toolchain-manifest "${toolchain_manifest}" \
    --acquisition-feed "${acquisition_feed}" --feed-manifest "${feed_manifest}" \
    --evidence-dir "${evidence_dir}" run-analysis
```

There is no operator-supplied verification status. The receipt is created only when
snapshot and final-manifest verification succeed and binds the SnapshotId, final
manifest hash, cutoff, package/sanitisation versions, approved destination, and
`PASSED` result. Authorization identity remains in Aqua's durable operational audit
system if policy requires it. Inventory and all analysis artifacts run twice and
must compare byte-for-byte. An anomaly does not reopen D03B/D09. Evidence of
impossibility or catastrophic mismatch is reported explicitly as `OWNER DECISION
RECONSIDERATION REQUIRED`.
