# Repository validation commands

This guide records validation that is supported by the current solution, `package.json`, Dockerfiles, and CI workflow. Run commands from the stated working directory. Do not substitute real credentials in command history or documentation.

## Scope gates

| Change scope | Required validation |
| --- | --- |
| Every change | Inspect `git status`, the complete diff, staged changes, and relevant untracked files; run `git diff --check`; run focused tests or checks for changed behaviour. |
| Backend | Release restore/build and the relevant backend tests. Run the full backend suite before merge for shared, cross-layer, or high-risk changes. |
| Frontend | ESLint, TypeScript, relevant Vitest tests, and the production build. Run the full frontend suite before merge for shared or high-risk changes. |
| Database | Backend requirements plus the EF model check, migration discovery, and PostgreSQL migration tests. Docker must be available for the PostgreSQL tests. |
| Payment, authentication, authorization, privacy, or other security-sensitive work | Relevant full backend/frontend suites, negative-path tests, dependency advisory review, and production builds. Validate container images when deployment behaviour can be affected. |
| Container or deployment | Build the affected image; validate both images and Compose configuration before release when shared deployment files change. |
| Documentation only | `git diff --check`, link/path inspection, instruction-consistency review, and any directly affected generated specification check. No Markdown linter is currently configured. |

CI currently runs the full backend, frontend, production dependency audit, and both container builds for pull requests and configured pushes. Local scope gates reduce feedback time but do not replace required CI.

## Backend

Working directory:

```bash
cd AqualLifeStyle/9.4.2/aspnet-core
```

Local restore, Release build, and full test suite:

```bash
dotnet restore AqualLifeStyle.sln
dotnet build AqualLifeStyle.sln --configuration Release --no-restore
ASPNETCORE_ENVIRONMENT=Development dotnet test AqualLifeStyle.sln --configuration Release --no-build --logger "trx;LogFileName=backend-tests.trx" --results-directory TestResults
```

CI executes the same application inventory in eight disjoint partitions:
administration, membership, authentication/account, and remaining application cases; migration, placement/
structural, and remaining EntityFrameworkCore cases; and remaining namespaces. Web
tests use placement, controller, and remaining-case partitions. Discovery must equal each full inventory as a
multiset; every shard must also match discovered identities to completed TRX
results. Splitting does not remove cases or increase the existing test-job limits.
The explicit PostgreSQL job remains separate: default web skips and provider-gated
bodies do not establish PostgreSQL execution. See the workflow's required passing
cases and execution markers.

Use `ASPNETCORE_ENVIRONMENT=Development` for local tests, matching CI. The default
Production environment deliberately disables seed data and cannot establish the
seeded web-test fixture prerequisites.

The full application test project includes PostgreSQL migration tests that start `postgres:16-alpine`; Docker must be running. A focused xUnit test can be selected with its fully qualified name:

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet test test/AqualLifeStyle.Tests/AqualLifeStyle.Tests.csproj --configuration Release --filter "FullyQualifiedName~Namespace.ClassName"
```

Use `--no-build` only after a successful build containing the current changes.

## Isolated member Area authorization

`MemberAreaAuthorizationPostgreSqlTests` exercises real authentication, persisted
permissions, list/count/paging, member/customer aliases, mutations and authority
lock races using synthetic records. It requires `MEMBER_AREA_AUTHORIZATION=true`,
`REPRO_PG=true`, `REPRO_TRANSACTIONAL=true`, `REPRO_PG_SCHEMA_FROM_MODEL=true` and
a secured `REPRO_PG_CONNECTION` with `Host=127.0.0.1` and a disposable database name
starting `member_area_authorization_`. Never supply a production connection.
The host checks these guards before database initialization. From the backend:

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet test test/AqualLifeStyle.Web.Tests/AqualLifeStyle.Web.Tests.csproj \
  --configuration Release \
  --filter FullyQualifiedName~MemberAreaAuthorizationPostgreSqlTests
```

All twenty-four cases must pass with zero skipped. The additional import and
restoration regressions exercise the real CSV preview/import API, absent/revoked/
future/insufficient Area authority, explicit Host permissions, per-row rollback,
both revocation/restore orderings, restore-with-demotion and strict removed-record
lock semantics. CI validates the TRX with a PowerShell shell/XML check.
Schema is model-created; this does
not prove migration behavior. CI runs a dedicated step on its existing PostgreSQL
service in a separate disposable database and rejects missing/skipped cases.
Without opt-in, default web-suite skips are intentional and are not PG evidence.
Member/customer component fixtures use SQLite with an explicit lock double;
their lifecycle results cannot be promoted to PostgreSQL concurrency evidence.

## Entity Framework Core

The projects use EF Core 8.0.8. Confirm `dotnet ef --version` reports a compatible 8.x tool before trusting model comparisons; the repository does not currently pin `dotnet-ef` in a local tool manifest.

From the backend working directory, supply a syntactically valid non-production connection string so the design-time startup configuration can load. The model check does not connect to this database:

```bash
ConnectionStrings__Default='Host=localhost;Port=5432;Database=aqua_validation;Username=aqua_validation;Password=aqua_validation' \
ASPNETCORE_ENVIRONMENT=Development \
dotnet ef migrations has-pending-model-changes \
  --project src/AqualLifeStyle.EntityFrameworkCore/AqualLifeStyle.EntityFrameworkCore.csproj \
  --startup-project src/AqualLifeStyle.Web.Host/AqualLifeStyle.Web.Host.csproj \
  --context AqualLifeStyleDbContext \
  --no-build
```

Expected result: `No changes have been made to the model since the last migration.` A non-zero result must be investigated; do not generate a migration merely to silence an incompatible CLI version.

Migration metadata and PostgreSQL apply/rollback coverage:

```bash
dotnet test test/AqualLifeStyle.Tests/AqualLifeStyle.Tests.csproj \
  --configuration Release \
  --filter "FullyQualifiedName~AqualLifeStyle.Tests.EntityFrameworkCore"
```

The PostgreSQL migration suite is destructive only to its temporary Docker databases. Production migrations run through `AqualLifeStyle.Migrator` as a deployment step, never as an ad hoc review command against production.

## Frontend

Working directory:

```bash
cd AqualLifeStyle/9.4.2/aqua-frontend
```

Install exactly from the lockfile on a clean environment or after dependency changes:

```bash
npm ci
```

Supported checks from `package.json`:

```bash
npm run lint
npm run type-check
npm test
```

Run one or more focused Vitest files by passing their paths:

```bash
npm test -- src/path/to/component.test.tsx
```

The production build validates environment variables while collecting page data. Use non-secret validation values locally:

```bash
NEXT_PUBLIC_ABP_API_URL=https://api.example.test \
NEXT_PUBLIC_DEFAULT_TENANT_NAME=Default \
NEXTAUTH_SECRET=ci-only-placeholder-secret-at-least-32-characters \
npm run build
```

Production dependency audit, as enforced by CI:

```bash
npm audit --omit=dev
```

`npm run test:coverage` is available for coverage analysis but is not a universal completion gate. `npm run analyze` is optional performance/bundle diagnostics.

## Dependency advisories

NuGet emits configured audit warnings during restore/build. For payment or security-sensitive backend changes, explicitly inspect direct and transitive advisories from the backend directory:

```bash
dotnet list package --vulnerable --include-transitive
```

This command currently reports known direct and transitive advisories and may still exit successfully. Treat its output as review evidence; classify whether each relevant advisory is introduced, pre-existing, production-reachable, or test-only. Do not claim a clean backend audit solely from the exit code.

## Containers and Compose

Run from the repository root. CI builds these images with Buildx:

```bash
docker build --file docker/api/Dockerfile .
docker build \
  --file docker/web/Dockerfile \
  --build-arg NEXT_PUBLIC_ABP_API_URL=https://api.example.test \
  --build-arg NEXT_PUBLIC_DEFAULT_TENANT_NAME=Default \
  .
```

For Compose validation, copy `.env.example` to an ignored `.env`, replace every required placeholder locally, then run:

```bash
docker compose config --quiet
```

`docker compose up --build` is an optional local integration environment that starts PostgreSQL, Redis, the migrator, API, and frontend. It requires valid local configuration and provider test credentials; it is not a safe generic validation command for documentation-only changes.

## Formatting and final review

The repository currently has no configured Markdown linter, Prettier command, dedicated .NET formatter, or additional analyzer command. Do not claim those checks ran.

For all changes, run from the repository root:

```bash
git diff --check
git status --short
```

Also inspect `git diff`, `git diff --cached`, and untracked files directly. `git diff --check` does not inspect untracked file contents until they are staged, so review new files separately before handoff.
