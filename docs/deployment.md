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

### Bird transactional email

Registration verification, password resets, enquiry responses, and confirmed
AQGreen or Onyx payment notices use Bird's Email API. Before enabling the
feature, use Bird's current [Email API send-message reference](https://bird.com/en-us/docs/api/reference/create-email-message),
not the older Channels API documentation:

1. In Bird, add `aqualifestyleclub.co.za` as the sending domain. Add every DNS
   record Bird provides (domain verification, SPF, DKIM, bounce, and tracking)
   in Vercel DNS, because Vercel is the domain's authoritative DNS provider.
   Wait until Bird shows all required checks as verified.
2. Create a least-privilege Bird API key for email sending. A current key has a
   `bk_{region}_...` format; the API selects its regional host from that prefix.
3. In Render's API service, enter `Bird__ApiKey`, `Bird__FromEmail`, and
   `Bird__ReplyToEmail` directly as secret values. Set `Bird__FromName` to the
   customer-facing sender name. No Bird workspace or channel ID is required by
   this API.
4. Set `Bird__Enabled=true`, sync the Blueprint, and redeploy. Production startup
   deliberately fails if transactional email is enabled with incomplete values,
   or if it is disabled while verification is required.
5. Register a disposable test Club Member, confirm the verification message is
   delivered, follow the link, sign in, and test one password-reset request.

The API stores delivery intent in `TransactionalEmailOutboxMessages` in the same
transaction as the business change. A worker retries pending records with capped
backoff. Inspect pending rows by status, attempt count, next-attempt time, and the
redacted last-error summary; never copy stored token-bearing message bodies into
logs or support tickets. Bodies are cleared after Bird accepts a message for
delivery; this does not prove final delivery to the recipient. Terminally failed
records redact the recipient, subject, and bodies while retaining
only the operational metadata needed to diagnose the failed intent. Support staff
must create a new email intent after correcting the cause; terminal messages cannot
be replayed from retained customer content. The worker
sends the durable outbox key in Bird's `Idempotency-Key` header. Bird replays the
original response for a matching request during its documented idempotency window
(three hours by default), closing the normal accept-then-crash retry gap. The
database's unique outbox key permanently prevents duplicate business intents.
This is still not an indefinite exactly-once guarantee: a lost success followed
by a retry after Bird's window can send again. Database-backed claim tokens ensure
that only one API worker sends an eligible row at a time, and abandoned claims are
eligible for recovery after ten minutes. Final delivered, bounced, or rejected
outcomes are not retained until Bird delivery webhooks are implemented.

To rotate the API key, create the replacement in Bird, update
`Bird__ApiKey` in Render, redeploy and verify delivery, then revoke the old
key. Never place keys in source files, Vercel variables, screenshots, commits,
logs, or documentation. Existing confirmed users remain confirmed. Existing
legitimate unconfirmed users must use the generic resend-verification flow; no
bulk auto-confirm migration is performed.

### Yoco programme payments

Yoco credentials belong only in the Render API service. They must never be
added to Vercel, a `NEXT_PUBLIC_*` variable, source control, screenshots, or
application logs. The hosted Checkout API does not require a public key in the
frontend.

Before deploying the payment branch:

1. Rotate any key that has been pasted into chat, email, an issue, or a terminal
   transcript shared with another person.
2. In the Yoco App, open the Checkout API integration and copy a new **Test
   secret key**. Test keys begin with `sk_test_`.
3. Register one test webhook for
   `https://aqualifestyle-api.onrender.com/api/payments/yoco/webhook`. The Yoco
   registration response returns a `whsec_` verification secret only once;
   save it directly into the secret store.
4. In the Render service Environment page, set `Yoco__SecretKey` to the test
   secret, `Yoco__WebhookSecret` to the returned webhook secret, and
   `Yoco__Mode` to `test`. The Blueprint marks both secret values `sync: false`,
   so Git never contains them.
5. Sync the Blueprint and deploy. Complete a payment with Yoco's published test
   card details, then verify that exactly one active Onyx participation, one
   payment ledger entry, and one completed checkout intent exist.

The API rejects a mode/key mismatch: `test` requires `sk_test_`, while `live`
requires `sk_live_`. A successful browser redirect is not proof of payment;
only a valid Yoco webhook activates AQGreen or creates and activates the Onyx
participation and network placement.
The webhook signature uses the raw body, `webhook-id`, and
`webhook-timestamp`, and rejects notifications more than three minutes old.

For live payments, first verify `https://www.aqualifestyleclub.co.za` in Yoco.
Create a live webhook, then update all three Render values together to the live
secret key, the live webhook's verification secret, and `live`. Keep the test
and live webhook secrets separate. Do not reuse the test webhook secret in live
mode.

The Render Blueprint runs the migrator before every API deployment. Deployment
of migration `20260728110000_AddYocoWebhookReceipts` therefore creates the
successful-event receipt table and unique event-ID index before the updated API
starts. Do not run the migrator from a workstation against production; confirm
the Render pre-deploy step succeeds instead.

### Payment operations alerts

The API emits structured JSON events whose message begins with
`PaymentOperationsAlert`. It never includes webhook bodies, signatures, customer
IDs, payment amounts, checkout URLs, or credentials. Current alert types are:

- `yoco_webhook_processing_deferred`: authenticated delivery could not complete
  and Yoco should retry;
- `yoco_webhook_signature_rejected`: signature validation failed;
- `yoco_webhook_payload_rejected` and `yoco_webhook_validation_rejected`: a
  signed or unsigned request was malformed or failed authoritative validation;
- `yoco_payment_monitor_failed`: the stale-checkout monitoring query failed;
- `stale_yoco_checkouts`: aggregate AQGreen and Onyx checkout counts remain
  awaiting confirmation beyond the operational threshold.

The monitor scans every 15 minutes and treats a checkout as stale after 60
minutes. Override these values through
`Yoco__Monitoring__ScanIntervalMinutes` and
`Yoco__Monitoring__StaleCheckoutThresholdMinutes`; both must be positive whole
minutes. A stale checkout does not prove that payment succeeded. Operations must
compare it with Yoco before changing any programme state.

Render's normal email/Slack notifications cover Render service events, not
application log patterns. To turn these signals into human notifications:

1. In the Render workspace, open **Integrations → Log Streams** and connect an
   HTTPS or syslog destination such as Better Stack, Datadog, or Papertrail.
2. In that destination, create an immediate alert for
   `AlertType=yoco_webhook_processing_deferred` and
   `AlertType=stale_yoco_checkouts`, plus
   `AlertType=yoco_payment_monitor_failed`.
3. Create a rate-based alert, rather than one notification per request, for the
   signature and validation rejection event types.
4. Send alerts to the monitored club operations email or Slack channel and run a
   test-mode payment to verify delivery end to end.
5. Until a log destination is connected, search Render logs for
   `PaymentOperationsAlert` at least daily and manually reconcile every stale
   checkout.

### AQGreen migration rollback safety

Before applying migration `20260726162000_AddAQGreenSingleJoiningPayment`
in any environment, ensure a verified database snapshot or other restorable
backup exists. The `Down()` path is deliberately blocked once AQGreen payment
checkouts or confirmed joining payments exist, because a partial downgrade
could falsify financial history. If a downgrade becomes operationally
necessary after such records exist, restore the database from the snapshot
taken before the upgrade; the migration cannot safely reconstruct the
original values on its own.

## Vercel frontend

Import the repository and set the project Root Directory to `AqualLifeStyle/9.4.2/aqua-frontend`. Configure these Vercel environment variables separately for Preview and Production:

- `NEXT_PUBLIC_ABP_API_URL`: the matching Render API origin, without a trailing slash.
- `NEXT_PUBLIC_DEFAULT_TENANT_NAME`: normally `Default`.
- `NEXT_PUBLIC_MONITORING_ENDPOINT`: optional browser telemetry collector.
- `NEXTAUTH_SECRET`: a server-only random value of at least 32 characters.

`NEXT_PUBLIC_*` values are embedded during `next build`; changing them requires a new Vercel deployment. Do not put secrets in public variables.

Do not add a frontend registration flag. Registration availability comes from the live Area-scoped ABP setting `Abp.Account.IsSelfRegistrationEnabled`, which defaults to enabled for active Areas. An Area can explicitly disable customer self-registration when it requires managed registration. Changing an Area setting changes the sign-in and sign-up experience without rebuilding the frontend, while server-side checks continue to enforce the same value.

Migration `20260722161500_EnableCustomerSelfRegistrationByDefault` changes existing active Area settings to `true` once. It does not continually overwrite later administrator changes.

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
