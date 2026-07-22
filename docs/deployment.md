# Aqua Lifestyle deployment

The current production target is:

```text
Browser
  ├─ Vercel edge → Next.js
  └─ Render proxy/TLS → ABP API → PostgreSQL
                                  └→ Redis distributed cache
```

Render and Vercel provide the public reverse-proxy and TLS layers. Do not expose the local Compose ports directly to the internet.

## Local Docker environment

1. Copy `.env.example` to `.env`.
2. Replace `POSTGRES_PASSWORD` and `JWT_SECURITY_KEY` with generated values.
3. Run `docker compose up --build`.
4. Open the web application at `http://localhost:3000` and API health at `http://localhost:21021/api/health`.

Compose runs one process per service: PostgreSQL, Redis, migrations, API, and Next.js. PostgreSQL and Redis data use named volumes. Containers communicate over `aqualifestyle-network` using service names, never `localhost`.

## Render API

Create a Blueprint from the repository-root `render.yaml`. Before the first deploy, Render prompts for:

- `App__ServerRootAddress`: the final Render API URL with a trailing slash.
- `App__ClientRootAddress`: the final Vercel production URL with a trailing slash.
- `App__CorsOrigins`: the allowed Vercel origins, comma-separated and without paths.

The Blueprint provisions the Docker API, PostgreSQL, and Redis. It runs the migration executable as a pre-deploy command, waits on `/api/health`, and deploys only after GitHub checks pass. The free data plans are suitable for evaluation; upgrade them before relying on production availability or retention guarantees.

For a fresh production database, set the secret `AQUA_INITIAL_ADMIN_PASSWORD` before the first migration. It must contain at least 16 characters with uppercase, lowercase, number, and special characters. It is used only to create missing initial administrators and does not rotate existing accounts. Follow [administrator access security](administrator-access-security.md) to replace the bootstrap credential immediately and verify session invalidation.

The API runtime uses the slim Debian ASP.NET image and its built-in non-root `app` user. A shell-less chiseled image is intentionally not used because Render's pre-deploy migration command needs a shell inside the runtime image.

Render supplies `DATABASE_URL`; the application converts it to the Npgsql keyword format without logging credentials. Redis is supplied through `Redis__Configuration` and becomes ABP's distributed cache backing implementation.

## Vercel frontend

Import the repository and set the project Root Directory to `AqualLifeStyle/9.4.2/aqua-frontend`. Configure these Vercel environment variables separately for Preview and Production:

- `NEXT_PUBLIC_ABP_API_URL`: the matching Render API origin, without a trailing slash.
- `NEXT_PUBLIC_DEFAULT_TENANT_NAME`: normally `Default`.
- `NEXT_PUBLIC_MONITORING_ENDPOINT`: optional browser telemetry collector.
- `NEXTAUTH_SECRET`: a server-only random value of at least 32 characters.

`NEXT_PUBLIC_*` values are embedded during `next build`; changing them requires a new Vercel deployment. Do not put secrets in public variables.

Do not add a frontend registration flag. Registration availability comes from the live Area-scoped ABP setting `Abp.Account.IsSelfRegistrationEnabled`. The Default Area is seeded as enabled when it has no explicit setting; other Areas default to disabled. Changing an Area setting changes the sign-in and sign-up experience without rebuilding the frontend, while server-side checks continue to enforce the same value.

## GitHub Actions

`ci.yml` is the required pull-request check. It builds and tests .NET, runs frontend lint/type/tests/build, and builds both Docker images without publishing them. Configure branch protection on `main` to require all three jobs.

`publish-images.yml` publishes immutable versioned API and web images to GHCR only for semantic-version tags such as `v1.2.0`. Add repository variables `PRODUCTION_API_URL` and `DEFAULT_AREA_NAME` before publishing the optional web image. The workflow produces semantic-version, commit-SHA, SBOM, and provenance metadata; `latest` remains only a convenience tag for stable releases.

For the current Render/Vercel setup, use their Git integrations for deployment and GitHub Actions for validation. This avoids long-lived Render or Vercel deployment tokens in GitHub. If deployment is later moved to prebuilt registry images, add a protected GitHub Environment with required reviewers rather than deploying directly from the build job.

## Production requirements

- Keep all secrets in Render/Vercel environment settings or a managed secret store.
- Keep API containers stateless. Add external object storage before introducing durable uploads.
- Logs are written to stdout; production API and migration logs are newline-delimited JSON.
- `/api/health` reports PostgreSQL and Redis readiness and returns HTTP 503 when either configured dependency is unavailable.
- Graceful shutdown is provided by the .NET and Node container entrypoints.
- Version deployments with semantic versions or dated releases; do not deploy an unqualified `latest` tag.
