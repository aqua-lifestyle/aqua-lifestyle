# Customer-11 offline canonicalisation adapter

This file-only .NET 8 tool invokes `AQGreenLegacyCanonicalisationSimulator.Simulate`.
It creates candidate output, not persisted topology or writer readiness. It does not
start ABP, connect to a database, contact Render or initialise a network client.
Core's existing transitive dependencies remain; no EntityFrameworkCore project,
writer tool or generic snapshot-runner project is referenced.

## Exact bounded input

Owner Stage-3B authority binds manifest SHA-256
`74a8e8bedd14d151c119173e44671c7ec8fd8c03ebb75cbd0763974c6dd3e416`
and seven-record capture SHA-256
`b84119e66c886ac0086b9f531186d7345a007359df465ac945afe033787e2494`.
There is no command-line hash override. Inputs remain protected outside the repository.
Only Tenant 1 / AQGreen, the exact owner-authorised six-person component, its accepted
root and sponsor edges, and scope `06a452ea-9958-5911-8c5b-5d350e516095` are admitted.

UNKNOWN V1 evidence maps to the domain's nullable structural level. Domain impacts
retain null and `Ambiguous`; the output also preserves the original UNKNOWN markers.
Missing/null/malformed structural evidence is rejected. Genuine numeric 0–3 evidence
is supported by the internal mapping and synthetic fixtures, without changing the
exact operational input binding. Historical approval for the root remains UNKNOWN.
Payment completion does not prove bank settlement. No historical financial facts are
reinterpreted. No participant confirmation or acquisition provenance is fabricated.

The reader validates all seven capture records and cross-checks participant, ordering,
payment, receipt, approval and supplement facts. Null aggregate correction fields are
accepted only alongside exact zero correction counts; null current lockout evidence
remains unavailable rather than becoming a historical security assertion.

## Local build and fixture validation

From `AqualLifeStyle/9.4.2/aspnet-core`, create lockfiles with the initial authorised
restore, then require locked restore:

```bash
dotnet restore tools/AqualLifeStyle.Customer11Canonicalisation.Tests/AqualLifeStyle.Customer11Canonicalisation.Tests.csproj
dotnet restore tools/AqualLifeStyle.Customer11Canonicalisation.Tests/AqualLifeStyle.Customer11Canonicalisation.Tests.csproj --locked-mode
dotnet build tools/AqualLifeStyle.Customer11Canonicalisation.Tests/AqualLifeStyle.Customer11Canonicalisation.Tests.csproj --configuration Release --no-restore /p:ContinuousIntegrationBuild=true
dotnet test tools/AqualLifeStyle.Customer11Canonicalisation.Tests/AqualLifeStyle.Customer11Canonicalisation.Tests.csproj --configuration Release --no-build --no-restore
```

Fixtures use synthetic payment/decision/security records. They do not contain the
production capture or provider secrets. Their internal hash-binding seam cannot be
selected from the CLI. No PostgreSQL/provider test is needed or claimed for this pure
adapter. Existing domain and writer source files are unchanged.

## Offline execution

After authorised validation, run the built DLL twice with identical arguments and
redirect stdout into two distinct evidence files. Only stdout redirection differs:

```bash
dotnet tools/AqualLifeStyle.Customer11Canonicalisation/bin/Release/net8.0/AqualLifeStyle.Customer11Canonicalisation.dll \
  --manifest '<protected manifest>' --capture '<protected capture>' > '<output-1.json>'
dotnet tools/AqualLifeStyle.Customer11Canonicalisation/bin/Release/net8.0/AqualLifeStyle.Customer11Canonicalisation.dll \
  --manifest '<protected manifest>' --capture '<protected capture>' > '<output-2.json>'
cmp '<output-1.json>' '<output-2.json>'
sha256sum '<output-1.json>' '<output-2.json>'
```

Canonical bytes are UTF-8 without BOM, ordinal property order, invariant formatting
and one final LF. Domain placement order is preserved. Runtime clocks, machine paths
and a self-referential output hash are excluded. Full file hashes are external evidence;
`domainManifestDigest` is separately labelled. Because the existing domain digest uses
platform newlines, pin Linux, SDK/runtime and invariant culture for repeatability. No
cross-platform byte identity is claimed.

## Provenance and acceptance boundary

Record base commit, exact reviewed new source hashes, lockfiles, SDK/runtime/OS,
restore/build/test commands, assembly/dependency hashes and validation results in a
protected source-to-artifact receipt. New files leave a changed worktree; this is not
a clean committed-source artifact. Source acceptance and commit require a later owner
decision. Archive identities are new and are not the lost historical writer artifact.

Capture origin remains the recovery clone. Keep its observation time separate from
PITR cutoff and effective time. Interrupted historical custody is disclosed. The
adapter does not inherit the generic runner's operational/Gate-Q qualification.

The new input hash changes all six prospective writer-derived placement IDs. This
tool neither produces persisted placement IDs nor changes writer pins, host-denial
bindings, capture-format expectations or manifests. Candidate output acceptance is
separate from writer rebinding and storage-ID acceptance. No database, V2, D10, clone,
commit, push or deployment authority follows from passing this adapter.

## Retained Stage-3B validation

At base commit `417bbcc38b68ecf9fd0b1f83eb52bf5d72d47abd` plus the reviewed new
adapter files, local fixture validation passed 63/63 with no skips. Two actual offline
runs produced byte-identical 24,489-byte output with SHA-256
`a1bcf510b41fa97e449e0dcb938d2b492ad08f215f6d4e5d75ff117f8b6af11d`.
Independent native reconciliation passed 25/25, six participant digests and the domain
digest recomputation. This establishes candidate canonicalisation only; new source
and output require owner acceptance. No writer or PostgreSQL validation is claimed.
