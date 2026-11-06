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
- No new environment flag is needed for account registration. The earlier correction
  permits
  only existing Tenant1 account operations while CLEAN_START commissioning,
  financial operations and all background/automatic financial work remain held.
  The existing Tenant self-registration setting still controls account creation.

## One ordered owner/operator action table

Only add an override if the effective value is absent or wrong. Keep valid inherited
settings. Do not populate example values as real authority.

| Order / actor | Exact setting or operation | Required value/source and expected result |
|---|---|---|
| 1 / owner | `App__AQGreenV2__Commissioning__AdmissionMode` | Retain effective `CLEAN_START`. |
| 1 / owner | `App__AQGreenV2__Commissioning__Enabled` | Retain `false` during evidence preparation. Leave all six D10 pins below absent/blank until the complete verified set is installed. No customer is registered or preselected for launch. |
| 1 / owner | `App__FinancialOperations__Enabled`, `App__BackgroundJobs__Enabled`, `App__WeeklyCommissions__Enabled`, `App__EntryMonthlyObligations__Enabled`, `App__AutomaticPayouts__Enabled` | Retain `false` during evidence preparation. The last four stay false throughout this MVP activation. |
| 1 / owner | `App__Recovery__Enabled`, `AQGreenV2Demo__Enabled`, `AQGreenV2Pilot__Enabled`, `SEED_DEMO_DATA` | Retain `false`; these are not onboarding workarounds. |
| 1 / owner | `App__YocoCutover__Enabled` | Retain literal `true`; durable capture is retained after Normal. Never delete captured/control records. |
| 2 / owner | `Bird__Enabled` | Retain effective `true` and the existing API key, verified sender and reply-to settings. Startup validation is not proof of delivery. |
| 2 / owner | `Bird__DeliveryNotBeforeUtc` | Existing approved mail-activation boundary, or the actual operator-approved boundary for this activation: non-future UTC ending `Z`. No default or date is invented here. Older mail stays held. |
| 2 / owner | `App__AQGreenV2__VerificationEmail__Enabled` | Set effective `true` after the Bird sender and cutoff are ready. This selects only Tenant1 `EmailVerification` mail; it does not enable general jobs. |
| 3 / protected operator | Prepare the post-reset root-unbound D10 Decision, authorized Areas/approver and effective boundary | Use `AQGreenD10CutoverV2` / `AQGreenD10VerifiedAuthorityV2`. Set `cleanStartRootCustomerId: null`, `cleanStartRootSelectionPolicy: "HOST_SELECT_AFTER_REGISTRATION_V1"`, and `cleanStartResetBoundaryUtc` to the original accepted boundary. Record actual current owner/verification times and a separately authorized LIVE effective instant. Do not recreate reset facts or re-date historical receipts. |
| 4 / protected operator | `App__YocoCutover__OperatorUserId` | Existing authorized Host user ID, confirmed through the existing Session API. No default operator is granted. Selection requires a controlled restart. |
| 4 / owner with protected operator | `Yoco__Mode` and existing Yoco merchant/webhook configuration | Genuine joining requires effective `live`, the already-approved matching live merchant key/signing setup and existing checkout-return configuration. The completed deployment accepted either test or live and does not prove which merchant mode is currently effective. Do not invent or expose keys or re-register the integration as a shortcut. |
| 4 / protected operator | Native runtime bindings from the protected release audit | The existing OIDC readiness response version2 exports actual EF assembly and combined Application/Core/EF digests, current configuration digest, and intended LIVE configuration digest. Use the newly deployed revision, not predecessor/local build hashes. The intended digest uses the approved `production-release` identity, actual new build revision and financial enablement in memory only. |
| 4 / protected operator | `App__AQGreenV2__Commissioning__DecisionPath` | Actual readable runtime location of the approved D10 Decision JSON. No default filename. |
| 4 / protected operator | `App__AQGreenV2__Commissioning__DecisionSha256` | Lowercase SHA256 of that exact file's bytes. |
| 4 / protected operator | `App__AQGreenV2__Commissioning__VerifiedAuthorityPath` | Actual readable runtime location of its approved VerifiedAuthority JSON. |
| 4 / protected operator | `App__AQGreenV2__Commissioning__VerifiedAuthoritySha256` | Lowercase SHA256 of that exact authority file's bytes. |
| 4 / protected operator | `App__AQGreenV2__Commissioning__CleanStartEvidencePath` | Actual readable runtime location of the accepted CleanStartEvidence JSON. |
| 4 / protected operator | `App__AQGreenV2__Commissioning__CleanStartEvidenceSha256` | Lowercase SHA256 of that exact evidence file's bytes. |
| 5 / owner with protected operator | Install all six verified pins, select `App__AQGreenV2__Commissioning__Enabled=true`, and set `App__FinancialOperations__Enabled=true` for the authorized payment/replay procedure | Only after the complete evidence is valid, apply as one consistent configuration matching the Decision's configuration digest, including LIVE mode and financial enablement. HTTP business access still requires durable Normal. No root Customer is required at this stage. |
| 6 / protected operator | Yoco status, manifest/replay and Normal procedure below | Stop if any captured event or outstanding reference is unresolved. |
| 7 / genuine customer | `/signup` → email verification → `/login`, then `GET /api/services/app/Customer/GetMyCustomer` | After Normal release, ordinary registration/confirmation/resend supplies the actual Customer identity privately. Registration requires the existing Tenant1 `Abp.Account.IsSelfRegistrationEnabled` setting, active Tenant, default role and active Area. No test customer or guessed identity is accepted. Registration creates no participation, payment, cover or placement. |
| 8 / protected Host administrator | `POST /api/services/app/AQGreenCleanStartRoot/AuthorizeRoot` | Supply `tenantId: 1`, the actual registered `customerId`, and the real admission `reason`. The verified active customer must have registered on/after the effective boundary in an authorized Area. Both existing Host permissions remain mandatory. Returns an unpaid participation, not activation. |
| 9 / customer and authorized approver | `/member/programmes`, then `/admin/programme-participations` | Existing Yoco checkout and signed confirmation complete R1,200 once or two distinct R600 instalments; funeral inclusion follows full completion; separate authorized approval creates Active membership/placement. The first root requires the existing dedicated Host bootstrap approver; subsequent sponsored members use their assigned Area Administrator. |

**Taking effect:** use Render **Save and deploy** to apply environment changes to
the existing build. **Save only** does not change the running process. Group and
secret-file changes also need an applicable service deployment. No source rebuild
is needed solely to change settings. Commissioning inputs/operator selection and
worker registration are startup-bound; changing JSON or a Dashboard value alone
does not complete activation. Do not reapply `render.yaml`, migrations or a reset.

## Existing file-custody mechanism

### Post-reset chronology and actual authority

The historical reset boundary remains in the original `CleanStartEvidence.State`.
The V2 Decision explicitly binds that same instant as `cleanStartResetBoundaryUtc`;
it does not edit the receipt or reuse its reset-only permission as LIVE authority.
Required ordering is reset <= reset boundary <= actual owner authorization <=
prospective LIVE effective instant. V2 VerifiedAuthority records actual verification
on/after owner authorization and expires after both verification and effective time.
Verification may be later than the planned effective instant, but no authority is
usable before its real verification time and no writes before the effective instant.
Renewal preserves the Decision/effective epoch and uses genuine current verification.

`resetBoundaryAuthorizationEvidenceDigest` binds the original reset authority;
`financialBoundaryEvidenceDigest` binds separate genuine current LIVE authority.
Retirement permission is not financial permission. Historical state comparisons and
retired participation classification retain the reset boundary. Any participation
in the reset-to-commissioning gap, including a deleted one, is rejected.

### Private-process transport on the existing Render service

File transport below remains supported with all its original privacy checks.
For the new post-reset contract, existing protected Render environment configuration
can instead carry exact base64-encoded accepted UTF-8 JSON bytes. This needs no SSH,
new credentials, disk, instance, or world-readable runtime file.

| Existing selector | Exact value | Private payload setting |
|---|---|---|
| `App__AQGreenV2__Commissioning__DecisionPath` | `environment:App:AQGreenV2:Commissioning:DecisionJsonBase64` | `App__AQGreenV2__Commissioning__DecisionJsonBase64` |
| `App__AQGreenV2__Commissioning__VerifiedAuthorityPath` | `environment:App:AQGreenV2:Commissioning:VerifiedAuthorityJsonBase64` | `App__AQGreenV2__Commissioning__VerifiedAuthorityJsonBase64` |
| `App__AQGreenV2__Commissioning__CleanStartEvidencePath` | `environment:App:AQGreenV2:Commissioning:CleanStartEvidenceJsonBase64` | `App__AQGreenV2__Commissioning__CleanStartEvidenceJsonBase64` |
| `App__YocoCutover__ReconciliationPath` | `environment:App:YocoCutover:ReconciliationJsonBase64` | `App__YocoCutover__ReconciliationJsonBase64` |

All three D10 selectors use the same transport. Their existing `*Sha256` pins still
hash the exact decoded bytes, including whitespace/newlines; embedded Decision /
CleanStartEvidence digests remain the existing canonical digests, not a substituted
file hash. Duplicate, malformed, missing, oversized or mismatched data fails closed.
The private D10 transport accepts only the explicit new Decision/Authority versions.
Pins alone do not select authority. Never copy payloads to frontend settings, logs,
chat or Git; preserve original source evidence/acceptance in existing private custody.

Prepare all accepted payloads/pins and the exact intended configuration first.
Stage the complete future Render configuration without deploying intermediate
states. Apply deployment identifiers, all accepted evidence and financial/commissioning
selection together with one controlled existing-service deployment. An invalid
startup stays failed/held; do not bypass source, schema, custody or acceptance checks.

### Private file transport

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

The root-unbound Decision is valid only with explicit empty-active-network evidence:
all baseline participations are archived, no placements or legacy memberships,
and all existing conservation/exposure/provenance checks pass. An omitted policy,
unknown count, zero/guessed root ID, or arbitrary populated network is rejected.
After launch, the immutable post-boundary Host root admission permits ordinary
network growth and restart; the Decision is not rewritten to insert a customer.
Competing root selections are serialized per Tenant. Exact retries retain the same
participation; a different customer cannot replace the root. Existing accepted
positive root pins remain compatible but cannot be combined with the deferred policy.

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
