# Accepted AQGreen V2 preservation and Node 24 qualification

> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence.
> It cannot confirm or supersede a business-policy decision.

Owner directly accepted the isolated 17-case subject on 12 October 2026 and
authorizes preservation, then Node 24 production-build qualification. No
production/provider operations, deployment, financial release, B6/B7 or Git
publication. Acceptance is recorded separately in
`docs/aqua-system/aqgreen-v2-isolated-mvp-owner-acceptance-20261012.md`.

Plan: verify accepted source hashes; preserve required tracked/untracked source
and evidence in deterministic archive; verify extraction; perform locked npm
installation, production build/startup and browser/API/PostgreSQL smoke on a
separate disposable copy. Preserve original on failure; patch only a separately
identified derivative; focused regressions and affected production retest.

Starting state: `feature/admin-ui-remediation`, HEAD
`e9bd8e96d55d03466492ffd40dd657802ae36d4b`, extensive accepted local changes.
All 1,739 accepted source hashes matched before copying. Snapshot contains 2,101
files including 46 required untracked paths, no ignored credential/cache/dump
files. Snapshot location:
`/home/wtc/Downloads/newAqua/aqgreen-v2-accepted-20261012-72219b17d8cd`.
Snapshot manifest hash:
`a21093f4319ac454a3308ea68a3544b0eddc8d5b51852e84ff1262d3738dd384`.

Next: archive verification and clean offline npm ci using cached Node 24 Alpine;
qualify actual standalone production runtime, not Next development mode.

Completed 12 October 2026: accepted archive verified2101/2101 source and extracted
files; final original-repository comparison2101/2101 unchanged. Accepted archive
SHA256784836d801cb459f1026e24805fb4c0b52b79e86ad6b4ed0838b0349d4964aa5.

Fresh offline npm ci succeeded after provisioning18 exact locked tarballs into
cache outside the isolated build. Node24.21.0/npm11.19.0, Next16.3.6, React19.2.4.
Accepted production build failed on external Geist font downloads. Original
preserved. Separate derivative uses next/font/local with existing Latin variable
font assets and OFL license: exactly5 changed/added paths; manifest identity
5e133be584398b2db14a5c542e27860bd6f02adae0c66c1488ef30e7ee2573e0. Derivative
archive0af6babebb839c3e65cf4df4797d59822ba84700829b2f0e96e0cdbd87faf11d;
source/extraction2105/2105 verified. Its fresh offline install/build passed.

Actual standalone NODE_ENV=production browser smoke passed signup/verification,
R1200 and R600+R600, incorrect amount/retry/authorization, Area approval/placement,
immutable attribution, progress, Area/explicit central review, held/rejected/
shortfall/met evidence, versioned R400 unreleased ledger, cutoff, payout denials,
production route/font/session/logout checks. Backend stayed AQGreenV2Demo;
Production+demoRequested failed startup as required. .NET8.0.31/PostgreSQL16.15.
Focused frontend23/23 and isolation backend13/13 passed; layout lint passed.
Seven operator contract mistakes/stale-session attempts were corrected/resumed,
retained as failures in the new event log. Only application correction: derivative
fonts. Existing17-case results preserved, not claimed as newly rerun under Node24.

Task-owned containers/network/bridge and secret env files removed. No original
source edits, Git staging/publication, production/provider or financial operations.
Final report: docs/aqua-system/aqgreen-v2-accepted-preservation-node24-qualification.md.
Evidence: docs/verification/aqgreen-v2-node24-qualification-20261012/.
Verdict A applies only to the separate locally TESTED derivative; no deployment
or production authorization inferred.
