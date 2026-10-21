# B6 V4/PostgreSQL 18 replacement-supply qualification — amendment 1

**Status: PROPOSED supply baseline; preparation and implementation authorized.
Independent baseline approval and privileged execution are NOT authorized.**

## Authority and exact boundary

Decision ID: `B6-V4-PG18-REPLACEMENT-SUPPLY-1`.
Owner: Xolani-M, direct instruction **“AGENT 2 — IMPLEMENT THE MINIMUM GITHUB B6
QUALIFICATION ROUTE”**, 2026-10-21. The review PR containing this amendment is its
durable preparation-authority locator. The instruction authorizes acquisition,
implementation, private custody, development validation and review publication.
It does not designate an independent reviewer or approve candidate-generated hashes.

The proposed operational amendment permits one independently approved replacement
supply baseline for the existing V4/PG18 implementation. It does not amend business
rules, source data authority, migration, cutover, D10, or financial use. Historical
source `a556249448ec143ecbe3a29bd36a5e3d4d376111`, bootstrap SHA256
`5ed57d64c74014002708dc679d5d26e9d7b53e888d1124dce3a74432e16a6244`, original
toolchain manifest `1c609222deecbbc9ef65d311528ad1eb19ff26adf70227752ee95cca73a5910c`
and historical receipts remain historical. They are not rewritten or reissued.

## Candidate sources and identities

[`candidate-baseline.json`](candidate-baseline.json) pins the candidate archive,
complete frozen toolchain inventory, native NuGet version, dotnet host, exact feed,
host packages and bootstrap. These hashes are integrity candidates, **not approval**.
Full manifests, archives, download records, native logs and signed repository
metadata are restricted private release assets, not public Git content/artifacts.
The852588720-byte input archive is transported as13 numbered private release
assets. Concatenation must reproduce the pinned complete archive SHA256 before
any extraction; transport parts do not establish separate approval authority.

- SDK8.0.130/runtime8.0.30: exact Microsoft Linux-x64 release archive, independently
  pinned publisher SHA512 from the official release metadata. The verified Microsoft
  SDK contains **NuGet6.8.2.3**, not the historical Ubuntu-build6.8.2.32767. Its dotnet
  host and complete tree differ. That difference requires express baseline review.
- Exactly215 U215 coordinates: NuGet.org service index/flat-container, existing
  three-leaf repository-signer policy, native `nuget verify --all` with online
  revocation and complete logs. All four existing native lockfiles are unchanged.
  Fresh locked restore and each installed content identity are checked against them.
  The newly acquired feed manifest happens to hash identically to the historical
  feed pin. That establishes the measured identities, **not historical custody**.
- mawk and BusyBox: exact Ubuntu archive packages, with InRelease signature,
  signed-index hash and archive hash verification. Existing executable pins match.
- Bubblewrap0.12.0: exact flatpak/stable Launchpad PPA package, verified using the
  PPA identity, signing-key fingerprint, signed InRelease, signed-index and archive
  hashes. The **new PPA source policy and executable pin require explicit independent
  approval**. No package was installed by preparation. This is the original
  privileged-bootstrap route; it adopts no separate controller/native-harness path.

Preparation uses no ambient package cache, sudo or production input. Its native
trust results are development acquisition evidence; operational A-L must regenerate
all215 online trust results and enforce its original freshness/chronology checks.
B1-Q's separate resolution/selection-approval role remains: there is no lock or graph
refresh in this candidate. If native locked materialization or graph checks fail,
stop; neither refresh locks nor promote newly resolved dependencies automatically.

### Two verified native materialization corrections

Microsoft's NuGet writes raw signed-archive SHA512 in `.nupkg.sha512`, while
`.nupkg.metadata.contentHash` and the locks carry canonical NuGet content identity.
The bootstrap now checks each against its own authority, not against each other.
Raw SHA256/SHA512, signature/source, installed nuspec, complete content/tree and
canonical identity checks are retained. Native extraction also decodes ZIP percent
escapes (for example `%2B`); the bootstrap compares the actual normalized path and
rejects traversal, NUL/control separators and normalized collisions. Regression
cases preserve raw/canonical distinction and independently reject tampering.

## Substantive protections retained

Reviewed Git-object launch; exact source/materialization; four locks and distinct
G197/TQ186/U215; native online signatures/revocation; parent-generated transaction
ledger; <=3600s freshness and civil/monotonic continuity; root0:0; exact final maps
`65534 0 1`; zero capabilities; no-new-privileges; network-denied package-consuming
children; narrow mounts and independent caches; Gate-Q61/no skips/TRX verifier;
Gate-A independent A/B B1-R/B2 complete runtime and simulation equality and frozen
semantic projection; cleanup, atomic terminal evidence and external-exit matching.

No isolation property is inferred from GitHub sudo or a runner label. Ubuntu24.04
image revision/kernel are retained; actual witnesses must pass. A changed host,
tool identity, graph, signer, warning closure or semantic result fails closed.

## GitHub execution and custody

The one workflow is reviewed in the public source PR but runs **only** in private
`aqua-lifestyle/b6-qualification-evidence`. Its private copy must byte-match the
independently reviewed source commit. It uses that private repository's own scoped
`GITHUB_TOKEN`; no cross-repository PAT or new server is required.

Only `workflow_dispatch` on private `main` is allowed. The `b6-qualification`
environment must name the designated independent reviewer, prevent self-review and
disable admin bypass. Initially its branch policy denies `main` until that reviewer
is designated and a run is separately authorized. A public dispatch skips the job.
GitHub currently rejects required-reviewer/self-review protection for this private
repository with HTTP422, citing unsupported billing-plan entitlement. The workflow
continues to require the real protection properties and cannot execute while they
are absent. The installed branch-deny hold is not substituted for reviewer
protection; supported entitlement/configuration is a known remaining blocker.
Before any sudo, live API checks require private custody, environment protection,
the exact same-repository source PR head, a current exact-commit APPROVED review
from the named non-author/non-executor, and this exact review-body line:

```text
B6_SUPPLY_BASELINE_APPROVED sha256:<SHA256 of candidate-baseline.json>
```

Separately, an open private issue authored by Xolani-M must contain exactly this
JSON (replace all placeholders). The required reviewer must hold the pending job
until the owner records its assigned run ID/attempt. Nothing in this implementation
creates that authorization issue or appoints a reviewer.

```json
{
  "schema": "B6PrivilegedQualificationAuthorizationV1",
  "source_commit": "<reviewed full commit>",
  "source_pr": 0,
  "baseline_sha256": "<approved manifest SHA256>",
  "workflow_sha256": "<reviewed workflow SHA256>",
  "run_id": "<assigned private workflow run ID>",
  "run_attempt": "1",
  "reviewer": "<owner-designated GitHub login>",
  "runner": "ubuntu-24.04",
  "evidence_repository": "aqua-lifestyle/b6-qualification-evidence",
  "scope": "QUALIFICATION_ONLY_NO_PRODUCTION"
}
```

The operational command remains existing `runner generate-key`, using a disposable
private key that is excluded from evidence. It receives no production DSN, customer
data, provider configuration or analysis snapshot. Root source custody avoids
weakening Git `safe.directory`. The original bootstrap owns its transaction.

Full originals, failed/partial evidence, source bundle, authority records, outer exit
and host observations are retained only in private release assets. Archive
readback SHA256 must match before immutable publication. Missing terminal evidence,
failed transfer or interrupted VM execution cannot be accepted. Workflow green,
an upload, an environment approval or immutable publication is never acceptance.

Custody has no automatic deletion/expiry. Preserve candidate and qualification
records through independent acceptance and subsequent B6 audit closure; any later
destruction needs an explicit owner retention/destruction decision. Immutable
releases protect published assets; restricted repository administration remains a
trusted custody boundary. Read access is limited to existing organization admins
and subsequently owner-designated reviewers/custodians. Do not expose these assets
through public logs/artifacts, caches or releases. This store is not approved for
production snapshots, raw data, pseudonym keys or credentials.

## Independent acceptance boundary

First: the owner designates an independent reviewer, grants restricted custody
access and configures the protected environment. That person reviews the source
diff, publisher/source policy (including the PPA), raw versus canonical identities,
all manifests/archives, exact locks/graphs and preparation evidence, then records
the exact-commit/hash approval. Review cannot be Agent2's own confirmation.

Second: the owner separately authorizes an exact pending run as above. No execution
is authorized by preparation, this amendment or candidate publication.

Third: after a run, the independent acceptor verifies original retained checksums,
live source/input/run binding, all215 native trust records and freshness, Gate-Q/A,
effective isolation/cleanup, terminal receipt and actual outer exit, plus custody
readback. Record the accepted source, baseline, run/attempt and evidence archive
hash in a separate durable decision. No automatic production release follows.

Source compatibility remains the observed PG18.4/54-migration profile. A production
schema change requires a new compatibility assessment before later extraction;
qualification is not evidence of current production schema or data acceptance.
