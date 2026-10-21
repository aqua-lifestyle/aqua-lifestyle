> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

# B6 V4/PG18 GitHub replacement supply — worklog version 1

## Scope and actual authority

Xolani-M's direct instruction **“AGENT 2 — IMPLEMENT THE MINIMUM GITHUB B6
QUALIFICATION ROUTE”**, 2026-10-21, authorizes candidate acquisition, minimal
amendment/bootstrap/tests/manual workflow, restricted GitHub custody, ordinary
non-privileged validation and review publication. No named independent reviewer,
baseline acceptance or privileged run is authorized. No production access,
migration, financial change, V2 activation or separate-controller work is included.

Source: branch `feature/b6-github-replacement-supply`, from clean PG18 candidate
`a556249448ec143ecbe3a29bd36a5e3d4d376111`; original B6 base
`438852eca9f91ef973802156ebbd31c3fa3940ed`. Main has advanced independently; the
review PR is stacked on the original B6 branch rather than reverting main or
silently incorporating changed domain/migration/dependency semantics.

## Completed acquisition and development observations

- Obtained the publisher-hash-verified Microsoft SDK8.0.130/runtime8.0.30 archive
  and all215 exact NuGet.org U215 coordinates in private local preparation custody.
- The Microsoft archive actually contains NuGet6.8.2.3, not historical Ubuntu-build
  6.8.2.32767. The first preparation attempt rejected that mismatch. Replacement
  baseline explicitly records the real version/new tree and dotnet-host identity.
- A second attempt exposed the native raw `.nupkg.sha512` versus canonical
  `.nupkg.metadata.contentHash` distinction. Native locked restore already accepted
  the unchanged locks. Verification was corrected to retain/check both identities,
  not fabricate equality. The bootstrap receives the corresponding scoped fix.
- All215 native online `nuget verify --all` results, repository signer/source
  records, full archive/installed content, canonical lock hashes and exact U215
  installed inventory passed. Four lock hashes are unchanged. No resolver refresh.
- The replacement feed manifest SHA equals the historical pin; newly measured byte
  identities do not establish continuity of historical custody.
- Three host-tool archives were verified against signed InRelease/index/archive
  identities without installation: Ubuntu mawk/BusyBox and Flatpak stable PPA
  Bubblewrap0.12.0. New PPA policy/binary identity needs independent review.
- Candidate archive SHA256
  `db193861ef5d607d390564d3543d71c486283fd1c6e5b7c889f500da7ea85619`,
  size852588720 bytes. Toolchain manifest
  `3fb331fd78afd15ba94aa2ac11c1cfaace2a654d32258ce1acfa128dbcc7590a`.
- Rehydration first correctly rejected Python's default data-filter mode changes.
  The custody extractor now retains authenticated archive modes after path/link/type
  safety checks. Full source-bound archive/215 files/toolchain/locks/host archives
  verification then passed. This does not approve the candidate.
- Private GitHub candidate readback passed:13 transport parts reconstruct the exact
  archive hash, then complete frozen-tree/feed/locks/host-archive verification
  passed after fresh extraction. The original bootstrap package-validation
  functions also passed against all215 actual native-restored packages, without
  executing the bootstrap or any privileged isolation lane.
- Workflow/custody development checks passed:5 unittest groups, including22
  first-boundary authority rejection cases and safe read-only-mode transfer checks;
  all embedded shell blocks pass `bash -n`. These API responses are mocked test
  cases, distinct from the actual GitHub configuration readback and422 above.
- Release build passed with three existing Core warnings. An initially over-broad
  `FullyQualifiedName~Native` filter selected an artifact test lacking its required
  synthetic fixture (2 passed/1 failed). Correct full primitive-class execution then
  passed **61/61, zero failed/skipped**, including existing synthetic bootstrap and
  lifecycle matrices plus new raw/canonical/path/Bubblewrap identity cases. These
  are ordinary-user development tests, not privileged Gate-Q/A.

## GitHub custody and concrete platform blocker

Created private `aqua-lifestyle/b6-qualification-evidence`; no teams granted access.
Effective collaborator readback initially shows only organization administrators
Xolani-M and aqualifestyleclub. Public forks are disabled by organization policy.
Actions are local-only, default token read-only, PR approval by Actions disabled.
Immutable releases are enabled. No extra server or cross-repository PAT is needed
for the proposed private workflow's own repository token.

Attempting the required private environment reviewer/self-review/no-bypass settings
was rejected by GitHub HTTP422: **“Please ensure the billing plan supports the
required reviewers protection rule.”** No protection was simulated or waived.
The workflow explicitly requires those live properties and will refuse privilege
without them. Appropriate GitHub entitlement/configuration is an additional known
execution blocker; no plan purchase/upgrade is authorized by this task.

## Remaining boundary

Finish source/workflow/custody development validation and exact review publication.
Independent reviewer appointment, exact baseline/source approval, supported required
environment protection, and a separate run/attempt-bound owner authorization are
required before privileged execution. Independent resulting-evidence acceptance
remains separate from all GitHub workflow/environment success.
