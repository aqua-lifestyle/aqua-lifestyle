# AQGREEN V2 — consolidated production release candidate

This handoff records source and evidence, not production reconciliation, cutover or deployment authority. Owner reports Agent 2 verdict **A. VERIFIED — D10 OFFLINE ENGINEERING ACCEPTABLE** for `38a5d4f4f53e7d1d1b5dd5dfba6da237aa839d29`.

Candidate: persistent `/home/wtc/Downloads/newAqua/aqua-lifestyle-v2-production-candidate`, branch `feature/aqgreen-v2-main-integration`. The consolidation commit follows verified `38a5d4f`; its exact SHA is reported in the task handoff. No history is rewritten. Runtime source, frontend, containers and Render configuration are byte-identical to `38a5d4f`; the only additional executable changes are test metadata and CI routing.

## Included engineering

| Commit | Capability |
| --- | --- |
| `fd0d188` | Accepted isolated test-money MVP |
| `2aac8e4`, `2ddaa49` | Accepted evidence/source preservation and exact feature-branch deployment suppression |
| `e11edda` | Main integration, complete CI partitions and compatibility corrections |
| `33cdbbe` | Legacy repeat-joining guard, complete discovery, frozen export, review-to-evidence binding and offline simulation |
| `8132c50` | Recorded historical Area supplement |
| `f33ed63` | Production startup/recovery suspension, worker/provider isolation and release preflight |
| `21af7fd` | Explicit D10 authority/source/population/configuration-bound production selectors |
| `38a5d4f` | Complete prospective Area envelope and locked final joining/checkout destination enforcement |

The accepted original and derivative archive/manifest digests still match their preserved records. Accepted evidence/source-identity directories are unchanged from `2ddaa49`. All listed commits and current remote main are ancestors of this candidate.

## Source inventory and comparison

Authoritative remote `main`, confirmed read-only during consolidation: `21a41694163ef09628fd84a9f4e12cb0efe1bb85`. Local `main` still points to older `26803037`; it was not changed. Use the remote SHA for this comparison, not that stale local ref.

Application inventory: .NET solution `AqualLifeStyle/9.4.2/aspnet-core/AqualLifeStyle.sln`, seven runtime projects (`Core`, `Application`, `EntityFrameworkCore`, `Web.Core`, `Web.Host`, `Web.Mvc`, `Migrator`) and two test projects (`Tests`, `Web.Tests`), all prefixed `AqualLifeStyle.`. Frontend: `AqualLifeStyle/9.4.2/aqua-frontend`, Next.js under the existing Node 24 container/toolchain. Existing API/web Dockerfiles and both GitHub workflows are included. Offline B6 contracts/normalization/simulation live in the existing backend; external B6 operational tooling/source identities remain as previously recorded, not a new deployed application.

[Application/CI source inventory](../verification/aqgreen-v2-production-candidate/application-source.sha256): **2,031 tracked files**, using the same four-prefix scope as the 2,007-file integration inventory: `.github/workflows/`, backend, frontend and `docker/`. SHA-256: `b129d9f1f40f807d386333ab9a608e54c4fd32a7ca859773ead6b45f5fbe60b1`. This is a new candidate inventory; accepted manifests are not replaced. Full Git commit identity also binds files outside this inventory, including `render.yaml` and documentation.

Exact comparison: `git diff --binary 21a41694163ef09628fd84a9f4e12cb0efe1bb85 HEAD`; use `git diff --name-status` for the complete path inventory. Differences are accepted MVP/frontend adaptations, integration/test/CI corrections, discovery/export/history/review and B7/D10 engineering plus their evidence/docs. Main's startup/provider checks, worker switches, Tenant/Area authorization, test-money/payout constraints, ledger uniqueness/history and complete partition/result checks remain. Dockerfiles, `render.yaml`, image-publishing workflow and frontend lockfile match remote main.

## Narrow publication defect corrected

New D10 facts/theories were unconditionally selected by the default SQLite placement shard. They also shared the existing PostgreSQL job's mixed legacy population, incompatible with their frozen whole-population fixture. Reuse the existing PostgreSQL-only discovery attributes: default mode explicitly skips these methods; model-created PostgreSQL mode retains the exact fourteen qualified identities. The existing PostgreSQL job now runs those fourteen separately in a fresh synthetic database on its existing service, requires five cutover plus nine Area outcomes with no missing/duplicate/skipped/failed cases, and retains all original regression markers. Explicit Development configuration is limited to synthetic web-test jobs; Production seed restrictions are preserved.

No business assertions, qualified bodies or application behavior changed. No new service/job, dependency, timeout extension or production configuration was introduced.

## Evidence carried forward and integrity checks

- Owner's independent verdict and the fourteen PostgreSQL/32 focused regression passes apply to verified `38a5d4f`; they are carried forward, not rerun or promoted to production evidence. Earlier MVP, integration, B6 and recovery evidence retains its original scope and status.
- New Release test-project build passed with zero errors; warnings remain.
- Default D10 metadata execution: ten explicit skips, no passed business bodies or database initialization. PostgreSQL-mode discovery: fourteen identities exactly equal the retained passing TRX.
- Actual default web discovery: **153 = 41 placement + 65 controllers + 47 remainder**, identical as multisets. Existing eight application/three web CI partition mechanisms remain.
- Workflow YAML parses; exact ancestry, accepted archive/manifest hashes, unchanged runtime/accepted evidence and Git connectivity/whitespace checks pass. No database access, migration, provider operation or publication occurred.

Ignored local logs/TRX/discovery and integrity results remain in backend `TestResults/release-consolidation/`. PowerShell workflow execution and hosted CI are pending; local inventory/static validation does not claim either passed.

## Controlled publication and recovery handoff

Separate authorization may publish only this exact feature branch. Expected CI: application discovery/eight shards, web discovery/three shards, frontend install/audit/lint/type/tests/build, API/web container builds without publication, and existing PostgreSQL regression job including isolated D10 execution. All required checks must pass on the published SHA before any separately authorized merge.

The exact branch's existing Vercel `deploymentEnabled=false` rule remains. First-push platform suppression is carried-forward repository configuration, not a newly observed hosted outcome. No Preview override work was performed. Do not push a `v*.*.*` tag: that independently triggers image publication, without a CI dependency. No PR/main merge is authorized.

`render.yaml` still declares `checksPass` auto-deployment and a migrator pre-deploy command. The preserved 2026-10-12 platform observation records main-only service attachment, auto-deployment off and previews off; this task does not refresh live Render state. Before publication, the owner/operator must confirm those suspension/trigger boundaries still hold. Applying the Blueprint or merging to main is a separate operation; it must not implicitly restart the API or run migrations.

No unresolved local Git conflict or application integration change remains. The new CI routing still needs hosted execution after separately authorized publication. Production recovery additionally requires actual build/schema/provider verification, restorable authorized backup/key/certificate custody, approved suspended configuration and operator access, authoritative scoped B6 evidence/review/reconciliation/applied verification, and separate owner D10/provider/worker authorization. Offline acceptance authorizes none of those operations.
