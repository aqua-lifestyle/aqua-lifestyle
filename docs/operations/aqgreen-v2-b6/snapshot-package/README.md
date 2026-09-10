# AQGreen B6 representative snapshot acquisition package

This package creates and verifies a **pseudonymised and data-minimised
representative snapshot**. Stable pseudonymous keys remain linkable inside the
dataset; the package does not claim anonymisation or determine legal compliance.

It does not authorise production access, carry production credentials, apply a
migration, add `LegacyUnknownAcquisition` to a production schema, enable V2, choose
`V2EffectiveAt`, resolve D10, or start B7.

If a later approved Phase 4 persists `LegacyUnknownAcquisition` in recruitment
attribution, its reviewed change surface includes `AQGreenAcquisitionSource`, the
domain source/source-shape guards, EF check constraints and model snapshot, an
additive migration, and every consumer currently restricted to invitation/direct
admission. None of that production surface is changed by this package.

## Contents

- `allowlist.json`: executable table/column allowlist. Every field records source,
  purpose, classification, transform, type, and necessity. Unlisted data is not
  exported.
- `operator-runbook.md` and `operator-extract.sh`: authorization, three-zone
  handling, and exact acquisition/handoff steps.
- `AqualLifeStyle/9.4.2/aspnet-core/tools/AqualLifeStyle.B6SnapshotRunner/`:
  authoritative .NET CLI containing the read-only extractor, sanitizer,
  manifest/digests, fail-closed file/restore verifier, reviewed root-disposition
  adapter, population/component analysis, impact recomputation, package-owned
  PostgreSQL runtime, finalizer, and verified receipt generator.
- `manifest-schema.json`, `root-disposition-schema.json`, and
  `handoff-receipt-schema.json`: machine-readable contracts.
- `run-analysis.sh`: package-owned PostgreSQL 16 lifecycle, repeated Zone 3
  inventory, D09 population, D03B component, simulator, structural/impact, final
  manifest, verification, and receipt pipeline.
- `authoritative-runner.sh`: the reviewed-Git-object bootstrap. Operators stream
  its exact bytes from the selected commit into Bash; it materialises two private
  independent clones with isolated Git configuration, disables replacement
  objects/hooks/system and global attributes, verifies every materialized file against
  its raw reviewed blob, performs two selected-only locked reproductions and then
  builds both with `--no-restore` without B6 operational capabilities,
  verifies exact Runner/Core/EntityFrameworkCore byte equality, selects the
  private read-only build-A artifact, and only then releases operation-specific
  authority.
- The three project-local `packages.lock.json` files are native NuGet candidate
  approved-decision artifacts. Their exact hashes and canonical G197 union are
  repository gates, but operational trust remains pending qualification.
- `../dependency-qualification-history.md`: durable Attempts 1–16 and native-lock
  architecture evidence. Failed experiments remain evidence rather than active
  resolver authority.
- `fixture/`, `tests/`, and `test-package.sh`: actual EF-migrated PostgreSQL fixture
  E2E plus malformed-snapshot negative tests for scope, relationships, identity,
  ordinals, permissions, symlinks, key strength, collisions, destination safety,
  manifest identity, and simulation/impact binding.

## Enforced scope and root evidence

Extraction requires one explicit authorised Tenant plus the versioned
`ALL_TENANT_AQGREEN_PARTICIPATIONS` analysis-population scope. Queries select that
Tenant's AQGreen participations and only the referential closure needed to
interpret them. In particular, unrelated Tenants and invitations are absent;
`ProgrammeInvitations` contains only non-deleted AQGreen invitations referenced by
retained `MemberInvitation` attribution. `AuthorisedDirectAdmission` is validated
against its own production source shape and does not require an invitation.

A recruiterless participant is a `RootCandidate`, never an accepted root. An
`AcceptedRoot` requires one root-disposition row with the snapshot participant key,
`ReviewedLegacyRootEvidenceV1`, a SHA-256 evidence digest, a controlled decision
reference, review time, and the `AuthorisedB6RootReviewer` role. Missing review
evidence quarantines the component. Sponsor evidence, root evidence, and
`LegacyUnknownAcquisition` are separate typed dimensions. Components are built
through sponsor ancestors before lifecycle exceptions are applied; an exceptional
ancestor quarantines the whole affected component without compression or
re-parenting.

Frozen topology is cutoff-effective rather than current-status-based. A participant
conclusively approved/activated after the cutoff is excluded when no cutoff-effective
topology depends on them. If a cutoff-effective sponsor edge or recruiter-correction
chain requires that post-cutoff participant, the complete affected component is
quarantined without ancestor skipping, reconnection, or reflow. Their commission,
loan, graduation, or resulting Onyx evidence remains historical-impact inventory
only and never grants canonicalisation eligibility.

## Canonical digest

`B6CanonicalLengthPrefixedUtf8V1` uses allowlist field order and primary-key row
order after canonical type normalisation. Each table begins `B6CANON1` plus LF. A
row begins `R`; null is ASCII `N`; a value is `V`, its invariant decimal UTF-8 byte
length, `:`, then its UTF-8 bytes. Rows end LF. Timestamps are UTC ISO-8601 with six
fractional digits and `Z`; integers are invariant; booleans are lowercase; UUIDs
and snapshot UUID keys are lowercase hyphenated. Table SHA-256 inputs never depend
on JSON formatting, locale, local time, or source row order.

The `BASE_SNAPSHOT` manifest seals the verified snapshot, Tenant/population scope,
cutoff, source MVCC/database identity, transaction semantics, versions, repository
commit, EF migration head, and first-party executable toolchain. The chronology is
fail-closed: `AnalysisSnapshotCutoff` must be no later than `SourceObservedAt` at
raw-source validation, sanitised verification, base construction/verification,
restored-database verification, and final-chain reverification.

The toolchain identity hashes the executing `AqualLifeStyle.B6SnapshotRunner`,
`AqualLifeStyle.Core`, and `AqualLifeStyle.EntityFrameworkCore` assemblies in
canonical ordinal order and binds their actual bytes to the repository commit.
`PRECOMMIT_SYNTHETIC_VALIDATION` permits a dirty local tree for synthetic tests;
those artifacts remain explicitly classified as precommit evidence.
`OPERATIONAL_EVIDENCE` does not execute a live-worktree Runner or live repository
script. The operator-controlled shell asks trusted Git for
`<reviewed-commit>:docs/operations/aqgreen-v2-b6/snapshot-package/authoritative-runner.sh`
and streams those exact reviewed bytes into Bash. That bootstrap verifies the full
commit identity with replacement objects disabled, creates two independent private
no-checkout clones with empty templates, and checks out the commit with repository
hooks plus system/global Git configuration and attributes disabled. Mutable source
repository hooks, `info/attributes`, filter configuration, replace objects, and
working-tree files are not copied. The bootstrap rejects ignored/untracked state
and compares the raw, no-filter hash of every regular materialized file to its
reviewed Git blob before dependency reproduction. Restore and build receive a
minimal environment with isolated `HOME`, `TMPDIR`, .NET, output, and intermediate
state plus one reviewed selected dependency set; DSN,
snapshot, output, key, and other B6 operational variables are absent. Both builds
use non-incremental deterministic Release compilation, build-server suppression,
suppressed ambient parent `Directory.Build` imports, and repository-relative
isolated artifact roots. Source-path normalisation makes Runner, Core, and
EntityFrameworkCore bytes independent of the two physical checkout paths.

Dependency handling has four separate responsibilities. **B1-Q (resolution
qualification)** is an explicitly triggered, controlled dynamic resolution that generates candidate
native `packages.lock.json` files, canonical graph and package/provenance evidence.
Generated locks are not trusted locks; separate review must approve their exact
hashes and graph. B1-Q is not a routine build step. The approved candidate locks for
Core, EntityFrameworkCore, and Runner are stored beside their projects.
For the pinned toolchain its native generation operation is `dotnet restore
--use-lock-file --force-evaluate` (plus the controlled source/packages/artifact
coordinates). B1-Q must retain the existing raw archive, canonical NuGet content,
nuspec, installed-tree, and source/provenance validation and emit the toolchain,
source, lock, and canonical-graph hashes. Dependency declaration, SDK/NuGet,
approved source-policy, legitimate refresh, or another explicitly approved
dependency-resolution change is required to trigger it.

**A-L (routine locked acquisition)** obtains an already-approved dependency
decision; it does not make a new one. Starting with the approved exact locks,
canonical G, toolchain and source policy, a network-capable acquisition child uses
native locked restore to populate a fresh cache with exactly the locked material.
The existing archive/content/nuspec/tree/provenance checks must reconcile that
cache to content-bound G197 before network authority terminates. A-L must not use
ambient caches, unlocked resolution, `--force-evaluate`, ResolverSupport, or
downloaded-but-unselected residue. Its selected, content-bound G197 output is the
only acceptable input to `--seed-nuget-packages-from`.

For the first adoption of these candidate locks, B1-Q may regenerate them and
require exact byte equality with the reviewed repository candidates as part of the
architecture qualification. That first-adoption comparison does not make B1-Q a
prerequisite for later ordinary runs. After approval, the locks remain inputs until
a legitimate dependency-authority trigger requires reconsideration.

**B1-R (locked reproduction)** requires
`--seed-nuget-packages-from <absolute-canonical-G-directory>`. The directory must
contain exactly the 197 approved selected ID/version pairs. S19/L1, L2,
ResolverSupport, and every other A-G acquisition residue are prohibited. The
bootstrap validates the hierarchy, administrative files, and archive hashes; derives
one read-only local source; and starts each reproduction with an independently empty
global-packages cache and fresh assets. It verifies the three reviewed lock hashes,
runs native `dotnet restore --locked-mode`, rejects every restore warning or error,
requires the lock bytes to remain unchanged, and reconciles each materialized cache
to the selected manifest. A global-packages folder is not treated as a package
source. The reviewed `NuGet.Config` remains present as a reviewed input, while the
explicit private source is the only viable package source.

The two B1-R runs use separate source checkouts, package caches, tool homes,
temporary directories, HTTP caches, and assets. Their selected package manifests
must be equal. Raw `project.assets.json` bytes may contain physical paths, so the
future qualification harness must use the existing C# graph tooling to require the
canonical union and SHA rather than manufacture raw byte equality across different
paths. **B2** then runs `dotnet build --no-restore` against each approved B1-R result,
requires locks, assets, and package inventories to remain unchanged, and compares
the three operational assemblies.

Each current A/B lane preserves the same source checkout, package-cache and
artifact paths from its B1-R restore into its B2 child, so the path-bound assets are
consumed at the coordinates they record. If a future qualification uses separate
mount namespaces for B1-R and B2, their distinct host locations must be mapped to
the same reviewed namespace-local source, package and artifact coordinates; the
qualification must inspect the assets `projectPath`/`packagesPath` and must not
present host paths as sandbox authority. A and B may have different host roots;
canonical graph equality is always required, while raw assets equality is required
only when their namespace-visible coordinates are identical.

The locks deliberately remain beside all three project files. NuGet creates assets
and consults a lock per restored project, including project references: Core governs
Core assets, EntityFrameworkCore governs EF assets, and Runner governs Runner
assets. All three are therefore required and hash-gated before restore. Their
presence also means ordinary wider-repository restores consult the Core and EF
locks and may propose lock changes when declarations change unless locked mode is
used. That maintenance visibility is intentional: such a diff is a B1-Q trigger,
not authority to accept a silent lock update.

`DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true` is deliberately set for B1-R/B2
configuration hygiene and determinism. It is not network confinement. Operational
qualification must launch these stages in the established Bubblewrap model and
prove network capability absent from the child. Locked mode, no-restore, a local
source, proxy hygiene, and environment variables do not establish that property.

The bootstrap requires exact byte equality for all three assemblies, makes the
selected build-A tree read-only inside its owner-only temporary root, and rechecks
the selected hashes immediately before launch. The exact reviewed
`operator-extract.sh` or `run-analysis.sh` adapter then receives only its required
operation environment and rechecks all three hashes before every Runner invocation.
The Runner binds its captured commit and assembly hashes to that pre-authority
selection. These environment bindings are defence-in-depth and evidence binding,
not the root of trust; the root is the operator-selected commit, trusted Git object
database/executable, operating system, Bash/core utilities, cryptographic hash
implementation, and canonical .NET SDK/MSBuild/NuGet build base. Ignored, modified,
or stale live-worktree source and build state are never selected. `test-package.sh`
remains dirty-tree `PRECOMMIT_SYNTHETIC_VALIDATION` and cannot establish operational
provenance.

The reviewed root disposition binds `SnapshotId`, `SourceSnapshotDigest`, and
`BaseManifestDigest`; reusing a review against another snapshot/base fails closed.
Inventory executes only against a restored schema carrying the same three-part
authority marker. Its artifact additionally seals the inventory rules and semantic
versions, exact external SQL query digest, result digest, and complete inventory
digest. The `FINAL_ANALYSIS` manifest is computed from the base snapshot, verified
inventory, reviewed root disposition, all three D09 populations, D03B
classification, package-generated simulator input/output, and recomputed impact.
The impact artifact explicitly marks every quarantined-component participant as
`STRUCTURALLY_AMBIGUOUS` / `QUARANTINED`, emits no simulated V2 level for that
participant, and reports its commission, payout, loan, graduation, and resulting
Onyx evidence as historical intersections only. Those intersections do not add the
participant to the canonicalisation population.
No authoritative analysis or inventory digest is caller-supplied. Both stages can
be verified; final verification independently reruns the inventory query and the
complete chain. A handoff receipt can be generated only from a successfully
verified final manifest.

The overall snapshot digest uses canonical compact JSON of stable manifest identity
fields (sorted object keys, UTF-8, no insignificant whitespace) and excludes report
creation time. `manifestHash` seals the complete manifest.

## Security boundary

Package entry points set `umask 0077`; controlled directories are `0700` and files
are `0600`. Existing files, symlinks, non-regular paths, and the former predictable
verification-temporary name are rejected. Temporary verification files use secure
exclusive creation inside an owner-only directory.

Generate a pseudonym key with the runner's `generate-key` command. Production use
requires at least 32 CSPRNG bytes and rejects weak/all-zero input. The HMAC-SHA-256
key is read only in Zone 2 and is never placed in the snapshot, manifest, receipt,
ordinary logs, Git, or command arguments. Pseudonym collisions across delivered
identifier domains are fatal; the tool never repairs a collision with an
order-dependent counter.

Before provenance, only the trusted operator shell, Git, reviewed bootstrap, and
system build tools possess execution authority. A-L may use only its approved
package-source network while obtaining the locked material; that authority ends
before its content-bound G197 output enters this bootstrap. The bootstrap rejects
no-seed execution and uses only its derived private selected-package source. The
bootstrap removes every B6 operational variable before materialisation/restore/build
children start. After provenance it constructs a new minimal environment for the
exact reviewed adapter and selected artifact. Package restore network access is a
build capability and receives no database, snapshot, output, key, provider, or B6
test-sentinel authority. The operator must set `GIT_NO_REPLACE_OBJECTS=1` on the
initial `git show` that supplies the bootstrap; the bootstrap keeps replacement
objects disabled for its Git-object resolution and private materialisations.

Analysis accepts no arbitrary destination DSN. The package creates a uniquely
named, loopback-only PostgreSQL 16 container with generated credentials, verifies
its ownership labels/nonce and database identity, refuses source/destination
identity equality, and can destroy only that verified resource. This proves the
narrow boundary that the package-owned Postgres-only container has no Aqua
application worker attached by the package; it does not claim to inspect arbitrary
external worker state. Npgsql receives generated credentials from the verified
owner-only runtime state; credentials do not appear in process arguments or output.

## Local validation

With Docker, .NET 8, Bash, and `jq`:

```bash
bash docs/operations/aqgreen-v2-b6/snapshot-package/test-package.sh
```

The wrapper writes a TRX result and validates it before printing PASS. The exact
TRX must have the repository VSTest 17.8 namespace
`http://microsoft.com/schemas/VisualStudio/TeamTest/2010` and its document root
must be `TestRun` in that namespace. Exactly one element in the entire document
may have that namespace URI and local name `TestRun`, so any additional
same-namespace `TestRun` fails closed regardless of prefix spelling. Exactly one
`ResultSummary` in that namespace must be a direct child of the authoritative root,
its `outcome` must be explicitly `Completed` (the successful value emitted by the
repository's current VSTest), and exactly one `Counters` in that namespace must be
a direct child of that authoritative summary. Foreign-namespace extension metadata,
including an element whose local name is `TestRun`, may coexist but cannot
substitute for the root, `ResultSummary`, or `Counters`. Namespace URI, not prefix
spelling, determines authority. The emitted accounting/success counters are
`total`, `executed`, and `passed`. Every emitted non-success counter—`failed`, `error`,
`timeout`, `aborted`, `inconclusive`, `passedButRunAborted`, `notRunnable`,
`notExecuted`, `disconnected`, `warning`, `completed`, `inProgress`, and
`pending`—must be present and zero. B6 evidence permits no skipped/not-executed
tests. Unknown unqualified `Counters` attributes fail closed as unsupported test
execution states; namespace-qualified extension metadata does not become an
execution counter merely because it is numeric. Discovery and execution must be
nonzero, executed/pass/fail and total/executed/not-executed accounting must
reconcile, and all executed tests must therefore pass. `B6_TEST_FILTER` may select
a focused set. Evidence-sensitive focused runs may additionally set a positive
`B6_EXPECTED_TEST_COUNT`, which must exactly match the TRX total. This avoids
hard-coding identities for every future test while allowing review gates to bind a
known focused suite.

The test migrates a disposable PostgreSQL 16 source with the current EF migrations,
inserts only production-schema-valid synthetic cases, and exercises the full
scoped package twice. Conditions prevented by production constraints are tested as
explicit malformed delivered-snapshot negatives. Its source update is intentional
drift-test setup, not a production operation.

## Architecture decision

AQGreen B6 operational and migration analysis tooling is .NET-native. This keeps
the migration-critical behavior aligned with the ABP/.NET backend, reuses the
authoritative C#/EF/domain semantics, avoids cross-language semantic duplication,
and reduces the operational runtime surface. Shell remains thin orchestration.
The package's reviewed-Git-object `authoritative-runner.sh` is the one privileged
bootstrap/build authority. Its mutable live-worktree path must not be executed for
operational evidence. `run-analysis.sh` and `operator-extract.sh` are reviewed-tree
operation adapters executed only after double-build provenance; they reject a
missing pre-authority selection and never build or choose a Runner. This decision
is specific to B6 and is not a repository-wide prohibition on Python.
