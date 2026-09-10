# Operator runbook — AQGreen B6 representative snapshot

> **DO NOT RUN WITHOUT PRODUCTION/DATA AUTHORIZATION.** This is a template for an
> authorised DBA/platform operator. D03B/D09, repository content, and disposable
> test success do not grant access.

## Pre-extraction checkpoint

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
creates two independent private clones, isolates Git configuration and attributes,
disables hooks and replacement objects, verifies every materialized file against
its raw reviewed blob, and gives locked restore/build only a minimal environment
with no B6 operational variables. It performs two independent B1-R locked
reproductions and then two B2 no-restore builds, and
requires exact Runner/Core/EntityFrameworkCore byte equality. It selects build A
inside the owner-only temporary tree, makes the selected tree read-only, and
rechecks all three hashes immediately before launch. Only then does the exact
reviewed operation adapter receive its DSN/snapshot/output/key environment, and the
adapter rechecks all three hashes before every Runner invocation.

This is a trust-before-sensitive-operation boundary. Trusted operating-system
process isolation, the operator-controlled invocation and `PATH`, Git executable
and object database, Bash/core utilities and SHA-256 implementation, and the .NET
SDK/MSBuild/NuGet build base are the minimum trusted computing base. Mutable source
repository hooks, `info/attributes`, checkout filters, replace objects, ignored files,
and live worktree contents are explicitly outside that base. The selected G197
package seed is mandatory; a no-seed operational restore is rejected. Restore uses
only its derived private filesystem source and receives no
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

Normal operation does not rerun B1-Q. **A-L routine locked acquisition** starts
from the already-approved exact locks, G197, toolchain and source policy, and uses a
network-capable child plus native locked restore to obtain exactly those approved
packages into a fresh cache. A-L makes no new dependency decision. Reconcile raw
archive, canonical NuGet content, nuspec, installed-tree and source/provenance
evidence; require missing and extra against G both to be zero; then terminate its
network authority. It must not rely on an ambient cache, unlocked resolution,
`--force-evaluate`, ResolverSupport, or unselected acquisition residue.

For first adoption, an explicitly approved B1-Q may regenerate the candidate locks
and compare them byte-for-byte with the repository copies. Once approved, the locks
are recurring inputs; B1-Q runs again only after a legitimate dependency-authority
trigger.

Transfer A-L's content-bound G197 output into a separate selected-only
global-packages directory, including the complete package directory for every
selected pair. Do not hand the bootstrap a raw acquisition cache: S19/L1, L2,
ResolverSupport, and other downloaded-but-unselected A-G versions are residue, not
B1-R dependency authority. Place
`--seed-nuget-packages-from '<absolute-canonical-selected-directory>'` immediately
after `--reviewed-commit "${reviewed_commit}"`. This remains the sole external
dependency input.

The reviewed bootstrap validates the selected hierarchy and complete package
material and records an ID/version/`.nupkg` SHA-256 manifest. It derives one
read-only hierarchical local source, then gives B1-R run 1 and run 2 independently
fresh empty global-packages caches and fresh assets. Each uses native
`dotnet restore --locked-mode`, the three approved lock hashes, and the explicit
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
future operational qualification harness must retain the established Bubblewrap
mechanical network witness and launch the confined child without network capability.

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
selected_packages='<approved absolute canonical-G197 directory>'
set -o pipefail
GIT_NO_REPLACE_OBJECTS=1 git -C "${repo_root}" show "${reviewed_commit}:docs/operations/aqgreen-v2-b6/snapshot-package/authoritative-runner.sh" |
  bash -s -- --repository "${repo_root}" --reviewed-commit "${reviewed_commit}" \
    --seed-nuget-packages-from "${selected_packages}" operator-extract
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
selected_packages='<approved absolute canonical-G197 directory>'
set -o pipefail
GIT_NO_REPLACE_OBJECTS=1 git -C "${repo_root}" show "${reviewed_commit}:docs/operations/aqgreen-v2-b6/snapshot-package/authoritative-runner.sh" |
  bash -s -- --repository "${repo_root}" --reviewed-commit "${reviewed_commit}" \
    --seed-nuget-packages-from "${selected_packages}" runner \
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
unique loopback-only PostgreSQL 16 container with generated credentials, restores
the bounded snapshot, and runs `verify-db`. It accepts no destination DSN,
verifies its container labels/nonce and database identity, rejects source/destination
identity equality, and cleans up only that verified package-owned resource. The
precise worker claim is limited to: the package-owned Postgres-only runtime has no
Aqua application worker attached by the package. The tool does not attest arbitrary
external worker state. Npgsql reads generated credentials only from the verified
owner-only runtime state; credentials are never command arguments.

The current default Docker bridge permits outbound connectivity. That blocks use
with representative controlled data until the approved Zone 3 environment enforces
and verifies no outbound connectivity. The mutable `postgres:16-alpine` tag is a
separate operational-environment reproducibility residual; it is not classified as
a source-commit blocker in this correction.

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
selected_packages='<same approved absolute canonical-G197 directory>'
set -o pipefail
GIT_NO_REPLACE_OBJECTS=1 git -C "${repo_root}" show "${reviewed_commit}:docs/operations/aqgreen-v2-b6/snapshot-package/authoritative-runner.sh" |
  bash -s -- --repository "${repo_root}" --reviewed-commit "${reviewed_commit}" \
    --seed-nuget-packages-from "${selected_packages}" run-analysis
```

There is no operator-supplied verification status. The receipt is created only when
snapshot and final-manifest verification succeed and binds the SnapshotId, final
manifest hash, cutoff, package/sanitisation versions, approved destination, and
`PASSED` result. Authorization identity remains in Aqua's durable operational audit
system if policy requires it. Inventory and all analysis artifacts run twice and
must compare byte-for-byte. An anomaly does not reopen D03B/D09. Evidence of
impossibility or catastrophic mismatch is reported explicitly as `OWNER DECISION
RECONSIDERATION REQUIRED`.
