> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

# Isolated payment and Area approval qualification

Owner scope: exercise the existing payment → pending Area approval → transactional
email → original administrator portal → decision workflow with synthetic records.
Production release `1251fb8` and its Test Mode financial isolation remain unchanged.

## Established cause and execution boundary

The production `YocoPaymentNotificationProcessor` validates Test Mode notifications
and returns before `ProgrammePaymentConfirmationProcessor`. Consequently no pending
participation or administrator notification is expected from that production test.
This is an intentional safeguard, not a missing email integration.

Reuse the existing PostgreSQL web-test host, authentication, confirmation processor,
notification scheduler, outbox processor, Bird adapter, and original Next.js pages.
The qualification invokes confirmation explicitly inside the test assembly after
proving the signed Test Mode callback creates no business state. This is synthetic
offline confirmation authority; it is not a real provider settlement or a production
callback bypass. No product endpoint or configuration switch is added.

## Checks

- Guard the fixture before test-host initialization: explicit opt-in, loopback
  PostgreSQL, disposable database-name prefix, model-created schema only.
- Exercise the real authenticated web endpoints and original frontend; do not mock
  application responses. Isolate external checkout transport only.
- Prove pending records, scoped queue/count/details, cross-Area/permission denials,
  approval and rejection audit, retry idempotency, and financial holds.
- Deliver only one specifically identified administrator outbox message to the
  owner-approved recipient when secured Bird configuration is supplied. Deny every
  other recipient/reference at the external gateway. Otherwise report delivery as
  unavailable, without manufacturing a provider success.
- Obtain Agent 2's independent review before proposing integration or deployment.

## Results — 2026-10-15

Status: IMPLEMENTED and TESTED on `test/payment-area-approval-qualification`,
based on production source `1251fb82979e18be53d1d0e7f5c6fab2d0c50a58`.
No application/business source, production configuration, database or deployment
was changed. Only qualification tests, their configuration and documentation were
added. The owner's subsequent closure instruction authorizes a tests/documentation
commit and focused pull request on this branch. Merge and deployment are excluded.

The PostgreSQL web-test host and original administrator portal passed together:
backend **1/1**, browser **1/1** (48.4 seconds). Eight related existing regressions
also passed, including AQGreen confirmation/approval, notification delivery failure,
Test Mode isolation, cross-Area denial and historical-outbox cutoff. These eight
regressions use their existing test provider; the new browser qualification uses
real disposable PostgreSQL, model-created schema and real HTTP authentication.
Frontend focused ESLint and TypeScript validation passed. The build passed with
existing dependency/analyzer warnings and a Linux-only fixture file-permission
analyzer warning; these are not production runtime findings.

| Stage | Result and evidence boundary |
| --- | --- |
| Payment | PASS: locally signed synthetic `payment.succeeded` Test Mode event accepted, with no business writes. Not a provider settlement. |
| Confirmation | PASS: explicit test-assembly invocation of the existing processor persisted two isolated payments; replay reused the same payment. |
| Pending approval | PASS: both Onyx participations awaited approval without activation. |
| Email | PASS: protected administrator alerts generated; the real outbox processor and Bird adapter delivered exactly the selected alert to the approved recipient. |
| Portal notification | PASS: original dashboard/sidebar count changed 2 → 1 → 0. |
| Administrator details | PASS: original Onyx queue displayed the expected member, Area, payment amount/currency, provider, reference and confirmation evidence. |
| Approval/rejection | PASS: authorized UI decisions persisted Active/Rejected states and exactly two audit decisions; matching retry succeeded and conflicting decision failed. |
| Security | PASS: cross-Area approval and cross-Tenant access denied; host visibility with explicit AllTenants permission did not grant missing approval permission. |
| Production safety | PASS: database guard ran before host initialization; frontend proxy origin was independently verified as loopback. Signed Test Mode created zero payments/participations/receipts; final synthetic confirmation/decisions created no monthly obligations, commissions or AQGreen placements. No production DB audit is claimed. |

Bird's matched provider events receipt returned HTTP 200 and recorded
`email.delivered` at `2026-10-15T19:36:31Z`. Its initially observed message summary
still reported `delivered_count=0`; that discrepancy was retained in restricted
evidence rather than replaced by a fabricated aggregate. Delivery is supported
by the specific delivered event. No second email or payment was sent.

Agent 2 independently inspected source and the final PostgreSQL, browser,
regression and provider evidence: **PASS — isolated qualification changes**, with
no material branch-owned security, authorization or regression defect.

Earlier failed attempts were fixture/configuration problems: test-session
registration, fresh Tenant/role setup, secure checkout URL, test-host capability
availability and browser workspace selection. They did not establish a product
defect. The final run used canonical role names and verified the actual login
request workspace. No production guard was relaxed to obtain the result.

Overall isolated qualification passed. **Provider-settled production payment →
approval end-to-end acceptance remains UNVERIFIED**: synthetic signed notification
and explicit isolated confirmation are separate steps. Production Test Mode
deliberately cannot create that approval queue. Member states were checked through
authenticated existing APIs and PostgreSQL; member-page rendering is not covered
by this MVC test host, which lacks the production health route. AQGreen has focused
existing service regression evidence, not this run's Onyx browser qualification.

Raw synthetic fixtures, TRX/browser logs, isolated-origin verification and sanitized
Bird acceptance/delivery receipts are retained in the restricted, non-Git directory
held by the qualification operator. No machine-specific evidence location,
fixture credentials, provider identifiers or recipient details are published.
Sensitive provider settings were used in memory only. The production release and
financial/commission/monthly/D10 restrictions remain untouched.

## Closure and production administration boundary — 2026-10-15

The owner accepts the following separate results. PASS does not imply any later
deployment, enablement or full production workflow status.

| Acceptance boundary | Result |
| --- | --- |
| Isolated payment confirmation and approval workflow | PASS |
| Administrator email delivery through Bird | PASS |
| Original dashboard approval and rejection behavior | PASS |
| Tenant/Area authorization in the isolated participation workflow | PASS |
| Production Yoco Test Mode payment safety | PASS (carried-forward owner acceptance; not rerun here) |
| Production provider-to-administrator approval end-to-end | UNVERIFIED |

The original isolated artifacts matched the preserved evidence manifest before
sanitization. The fixed synthetic password was replaced with a per-process random
value; no password is committed. Fresh closure checks are recorded separately
from the original run. Bird delivery is carried forward; no new message is sent.

Fresh closure validation on the sanitized test artifacts:

- Release Web.Tests project build (`--no-restore`): PASS, zero errors and four
  warnings (existing xUnit diagnostics and the Unix-only fixture permission API).
- PostgreSQL 16 isolated qualification: PASS, 1/1, zero skipped; original-portal
  Playwright: PASS, 1/1, zero skipped (1.2 minutes), using Node 24 and local Chrome.
  The owned frontend proxy was verified as loopback. Bird sending was explicitly
  disabled; outbox generation and notification presentation were still asserted.
- The same eight related existing service regressions: PASS, 8/8, zero skipped.
  These retain their existing test provider; they are not production evidence.
- Focused ESLint on the spec/configuration and frontend TypeScript: PASS.
- Scoped staged sensitive-artifact scan, documentation link inspection, scope
  review and `git diff --cached --check`: PASS. Only the five qualification
  test/configuration/documentation files are staged. No raw evidence is included.
- Initial local rerun attempts failed at runner origin preflight, symlinked
  Turbopack startup, and authentication with the first overlong generated password.
  The runner waits for its process environment, uses documented Webpack support,
  and the final fixture uses a 28-character random password. The final complete
  execution above supersedes those attempts; their restricted logs are retained.
- Owned local processes and disposable databases were stopped/removed. No new
  provider request, production mutation or deployment was performed. Full suites,
  frontend production build and container checks were not rerun locally; PR CI
  remains separate required integration evidence before merge.

Authoritative references: the owner's qualification-closure instruction,
[payment workflow](../../development/programme-payment-approval-workflow.md), and
[repository validation](../../development/validation.md). The production browser
observations below remain an external dependency, not an assumption of success.

Successful production administrator login is owner-confirmed. The environment has
no connection to that authenticated browser, so current production member lists,
participation counts, effective permissions and notifications remain UNVERIFIED.
No public-page, login or API-health qualification was restarted. Use the existing
[owner-assisted dashboard check](../../development/programme-payment-approval-workflow.md#owner-assisted-production-dashboard-check)
to obtain only aggregate observations from the established session.

Repository inspection identifies an existing limit: the Club members page labels
`tenantId` as Area, and `AdminMember.GetAll` filters Tenant but has no business-Area
assignment filter. Its display cannot prove Area-scoped member visibility. This is
a pre-existing commissioning gap, not a qualification-branch regression; no
business code is changed. Participation queue authorization was separately tested.

Next action: complete that read-only owner walkthrough and classify the member
scope gap before proposing any production commissioning. A later provider-to-
approval production acceptance needs separate explicit authorization and genuine
provider evidence. Test Mode cannot supply business-payment authority. Yoco Test
Mode, financial holds, commissions, monthly automation and D10 remain unchanged;
no production records, callbacks, configuration or deployments are touched.
