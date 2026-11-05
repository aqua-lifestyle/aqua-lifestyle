# AQGreen first-customer activation

This is an execution guide, not business authority or proof of activation. The
owner has already authorized controlled MVP activation. No further general
authorization is requested. Reuse the completed cutover/deployment records.

## Effective configuration: do not infer it from one Dashboard list

The application loads `appsettings.json`, then `appsettings.Production.json`, then
the **process environment**. Render service variables override linked environment
groups; group variables and group secret files can therefore supply values absent
from the service-level list. Collisions across several groups are not guaranteed
by Render. Production does not load `.env` or development user secrets. Secret
files are not automatically converted into configuration keys.

Use the existing service's **Environment → Linked Environment Groups**, **Secret
Files**, and existing Render Shell to establish the effective source. Do not dump
the whole environment or file contents. The following facts are distinct:

- Bundled verification-only delivery and commissioning default to `false`.
- There is **no bundled delivery cutoff or default D10 path/hash**.
- Last completed release evidence verified CLEAN_START selected, commissioning and
  financial/background execution held, and Bird startup configuration enabled.
  It did not prove a delivery cutoff, mail delivery, evidence-file mounts or valid
  D10 authority. Those private facts are not established by repository inspection.
- No new environment flag is needed for the first-account correction. It permits
  only existing Tenant1 account operations while CLEAN_START commissioning,
  financial operations and all background/automatic financial work remain held.
  The existing Tenant self-registration setting still controls account creation.

## One ordered owner/operator action table

Only add an override if the effective value is absent or wrong. Keep valid inherited
settings. Do not populate example values as real authority.

| Order / actor | Exact setting or operation | Required value/source and expected result |
|---|---|---|
| 1 / owner | `App__AQGreenV2__Commissioning__AdmissionMode` | Retain effective `CLEAN_START`. |
| 1 / owner | `App__AQGreenV2__Commissioning__Enabled` | Retain `false` during first-account registration. Leave all six D10 pins below absent/blank until the complete verified set is installed. |
| 1 / owner | `App__FinancialOperations__Enabled`, `App__BackgroundJobs__Enabled`, `App__WeeklyCommissions__Enabled`, `App__EntryMonthlyObligations__Enabled`, `App__AutomaticPayouts__Enabled` | Retain `false` for bootstrap. The last four stay false throughout this MVP activation. |
| 1 / owner | `App__Recovery__Enabled`, `AQGreenV2Demo__Enabled`, `AQGreenV2Pilot__Enabled`, `SEED_DEMO_DATA` | Retain `false`; these are not onboarding workarounds. |
| 1 / owner | `App__YocoCutover__Enabled` | Retain literal `true`; durable capture is retained after Normal. Never delete captured/control records. |
| 2 / owner | `Bird__Enabled` | Retain effective `true` and the existing API key, verified sender and reply-to settings. Startup validation is not proof of delivery. |
| 2 / owner | `Bird__DeliveryNotBeforeUtc` | Existing approved mail-activation boundary, or the actual operator-approved boundary for this activation: non-future UTC ending `Z`. No default or date is invented here. Older mail stays held. |
| 2 / owner | `App__AQGreenV2__VerificationEmail__Enabled` | Set effective `true` after the Bird sender and cutoff are ready. This selects only Tenant1 `EmailVerification` mail; it does not enable general jobs. |
| 3 / customer | `/signup` → email verification → `/login` | After the first-account correction is deployed, existing registration/confirmation/resend work with financial holds intact. Registration also requires the existing Tenant1 `Abp.Account.IsSelfRegistrationEnabled` setting, an active Tenant, valid default role and valid Area. A Render override does not establish a persisted Tenant setting's value. |
| 3 / customer | `GET /api/services/app/Customer/GetMyCustomer` | Existing verified member access supplies the actual new Customer identity privately for root selection. No customer ID is guessed or taken from a test account. |
| 4 / protected operator | Select the actual first root, Area/approver and effective boundary; validate the existing commissioning records | Use original accepted CLEAN_START facts, current source/schema/runtime/configuration bindings and valid authority. Do not recreate migration/reset facts or re-date historical receipts. |
| 4 / protected operator | `App__YocoCutover__OperatorUserId` | Existing authorized Host user ID, confirmed through the existing Session API. No default operator is granted. Selection requires a controlled restart. |
| 4 / owner with protected operator | `Yoco__Mode` and existing Yoco merchant/webhook configuration | Genuine joining requires effective `live`, the already-approved matching live merchant key/signing setup and existing checkout-return configuration. The completed deployment accepted either test or live and does not prove which merchant mode is currently effective. Do not invent or expose keys or re-register the integration as a shortcut. |
| 4 / protected operator | `App__AQGreenV2__Commissioning__DecisionPath` | Actual readable runtime location of the approved D10 Decision JSON. No default filename. |
| 4 / protected operator | `App__AQGreenV2__Commissioning__DecisionSha256` | Lowercase SHA256 of that exact file's bytes. |
| 4 / protected operator | `App__AQGreenV2__Commissioning__VerifiedAuthorityPath` | Actual readable runtime location of its approved VerifiedAuthority JSON. |
| 4 / protected operator | `App__AQGreenV2__Commissioning__VerifiedAuthoritySha256` | Lowercase SHA256 of that exact authority file's bytes. |
| 4 / protected operator | `App__AQGreenV2__Commissioning__CleanStartEvidencePath` | Actual readable runtime location of the accepted CleanStartEvidence JSON. |
| 4 / protected operator | `App__AQGreenV2__Commissioning__CleanStartEvidenceSha256` | Lowercase SHA256 of that exact evidence file's bytes. |
| 5 / owner with protected operator | Install all six verified pins, select `App__AQGreenV2__Commissioning__Enabled=true`, and set `App__FinancialOperations__Enabled=true` for the authorized payment/replay procedure | Apply as one consistent configuration matching the Decision's configuration digest. Complete account verification first: the bootstrap exception closes when commissioning/payment processing is selected. HTTP business access still requires durable Normal. |
| 6 / protected operator | Yoco status, manifest/replay and Normal procedure below | Stop if any captured event or outstanding reference is unresolved. |
| 7 / protected Host administrator | `POST /api/services/app/AQGreenCleanStartRoot/AuthorizeRoot` | `tenantId: 1` and the actual admission reason. Uses the pinned real Customer. Returns an unpaid participation, not activation. |
| 8 / customer and authorized approver | `/member/programmes`, then `/admin/programme-participations` | Existing Yoco checkout and signed confirmation complete R1,200 once or two distinct R600 instalments; funeral inclusion follows full completion; separate authorized approval creates Active membership/placement. The first root requires the existing dedicated Host bootstrap approver; subsequent sponsored members use their assigned Area Administrator. |

**Taking effect:** use Render **Save and deploy** to apply environment changes to
the existing build. **Save only** does not change the running process. Group and
secret-file changes also need an applicable service deployment. No source rebuild
is needed solely to change settings. Commissioning inputs/operator selection and
worker registration are startup-bound; changing JSON or a Dashboard value alone
does not complete activation. Do not reapply `render.yaml`, migrations or a reset.

## Existing file-custody mechanism

Render delivers Docker secret files at `/etc/secrets/<actual filename>`, including
files inherited from groups. The application consumes and verifies files; it does
not generate authority documents or discover filenames. Its API image runs as the
non-root application user. A file visible in the Dashboard is not automatically
readable by that user or acceptable to the evidence reader.

Before selecting commissioning, the protected operator verifies each actual file:
exists, readable by the application user, regular/non-symlink, outside Git, at most
32 MiB, and with no group/other permissions on either file or immediate parent.
The reader also rejects Git/symlink ancestry, changed hashes, duplicate JSON
fields and invalid/mismatched authority. Render's combined secret-file limit is
1 MiB. Check actual custody and size instead of assuming a new storage service is
required. Do not weaken permissions/checks or copy protected documents into source.

All six nonempty pins with commissioning still disabled cause
`D10_EVIDENCE_CONFIGURED_WITHOUT_EXPLICIT_SELECTION`: stage files privately, then
apply the complete configuration atomically. Missing files/hashes, bad custody or
invalid authority must stop the operation. No production file availability or hash
match is claimed by this document.

## Existing Yoco operator operations

Base: `https://aqualifestyle-api.onrender.com`. Use the existing authenticated Host
API session, permission `Pages.Tenants`, and the user pinned by
`App__YocoCutover__OperatorUserId`. A Tenant/Area session is not a substitute. If the
pin is absent, select the existing authorized Host user and restart; do not create
new credentials. Host authentication uses the existing TokenAuth endpoint with an
explicit empty `__tenant` header; omission otherwise selects the default Tenant.

1. `GET /api/operations/yoco-cutover/status` and private `GET .../events`.
2. If not already validly Normal, prepare the accepted
   `Aqua.YocoCutover.ReconciliationV1` manifest from actual merchant records. Pin
   `App__YocoCutover__ReconciliationPath` and `App__YocoCutover__ReconciliationSha256` through
   the existing protected file mechanism. Even an empty captured-event set needs
   actual outstanding-reference reconciliation, not an assumption of zero exposure.
3. `POST .../phase` with `{"phase":"Replay"}`; `POST .../replay` with each accepted
   private inbox `id`. Existing signature, Tenant, amount, mode, purpose, lock,
   immutable receipt and duplicate safeguards remain in force.
4. Only with no Pending/Held events and valid effective D10 authority:
   `POST .../phase` with `{"phase":"Normal"}`. Expected HTTP200/`Committed`.
   A 409/`Held` is a stop, not permission to drop events. If already Normal, do not
   repeat this transition: the implementation requires Replay as its source state.

The existing root endpoint requires both
`Aqua.Admin.ProgrammeParticipations.BootstrapAQGreenRoot` and
`Aqua.Admin.AllTenants` on a Host account. Area approval requires
`Aqua.Admin.ProgrammeParticipations.Approve` and actual assigned-Area authority.
The first root's approval additionally requires a Host session with
`Aqua.Admin.ProgrammeParticipations.BootstrapAQGreenRoot`; an ordinary Tenant/Area
session cannot perform that existing exceptional root transition.

No commission/payout/monthly/background enablement is needed for recruitment,
spillover or Level 1–3 progress. Those completed implementations are carried
forward. Do not enable their workers or replace sponsor attribution with placement.

## Actual acceptance boundary

Configuration success is not customer acceptance. Retain real registration and
verification, actual Yoco obligation/receipt completion, funeral inclusion, separate
Area approval/Active status and subsequent invitation/placement/progress evidence
through approved private channels. No production customer, payment, file, hash or
successful outcome is supplied by this guide.
