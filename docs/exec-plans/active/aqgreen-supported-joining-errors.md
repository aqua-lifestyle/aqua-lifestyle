> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

# Supported AQGreen joining and customer-facing errors

## Goal and authority

Implement the owner's focused joining/error correction and prepare a reviewed PR.
The owner's current instruction preserves Host-selected first-root admission,
invitation-based later admission, verified R1,200/two-R600 joining, and separate
membership approval. No deployment, commissioning, schema or provider changes are
authorized by this workstream. See [payment authority](../../aqua-system/04-payments-approval-and-yoco.md)
and [validation](../../development/validation.md).

## Git context

- Base: `b8d86e5e60997483423b273d5f426c08b411f573` (`origin/main`).
- Branch: `fix/aqgreen-supported-joining-errors`; isolated worktree.
- Original checkout has unrelated untracked work and remains untouched.

## Diagnosis

- OBSERVATION: owner reports the live "Start my own network" action followed by
  the independent-admission rejection, under the generic "Error" heading.
- REPOSITORY EVIDENCE: `JoinProgrammeDialog` posts `StartEntry` with
  `recruiterCustomerId: null` and no invitation before requesting checkout.
  `StartEntryAsync` rejects exactly that input when V2 joining is selected.
- ROOT CAUSE (PRE-EXISTING): the ordinary frontend offers an unsupported independent
  AQGreen admission, while the test/pilot presentation already requires invitation
  or separate privileged prospective-root admission. The backend rejection is an
  intended safeguard, not an authorization defect.
- REPOSITORY EVIDENCE: a Host-authorized root already has an awaiting-payment
  participation. It must use `CreateAQGreenJoiningCheckout`, not re-enter
  invitation-only `StartEntry`.
- REPOSITORY EVIDENCE: pending AQGreen checkout mapping currently returns `null`
  without a checkout URL, hiding a persisted PreparingCheckout state. The provider
  request occurs after that state commits. Therefore a failed response cannot
  justify a "no charge" claim or a competing checkout.
- Affected-customer scope was supplied privately and existing authorized Host
  reads were performed. Account-specific production facts are retained outside
  the repository. Current Host DTOs do not expose every registration/admission
  fact or bind every historical provider reference to the account; those limits
  are not converted into assumed email/Area verification or a no-charge claim.
- EXTERNAL EVIDENCE: anonymous live `/member/programmes` navigation redirects to
  authentication. No customer login or joining/payment operation was replayed.

## Scope and validation plan

1. Replace unsupported AQGreen independent admission with existing-process guidance.
2. Preserve existing authorized-root payment and invitation acceptance contracts.
3. Extend the existing ABP error normalizer and StatusMessage presentation, retaining
   diagnostic metadata and distinguishing uncertain payment outcomes.
4. Keep runtime backend sources byte-identical to the commissioned release. Its
   member read hides preparing checkout state; add a scoped browser review marker
   before checkout requests, surviving refresh, without interpreting it as provider
   evidence. Only authoritative payment advancement permits the next instalment.
5. Validate domain guards, payment-reference/idempotency/approval boundaries,
   component behavior, shared errors, and local browser interactions. Local test
   fixtures are not production customers or provider confirmation.
6. Review full diff, record current evidence and limitations, and open a focused PR.

## Current state

Implementation and focused review complete; reviewed draft PR prepared for handoff.
A proposed member-read mapping
correction was discarded before commit because changing the Application assembly
would invalidate the existing pinned activation. No production assembly, admission
guard, checkout implementation or commissioning source changes remain.
The supported private account reads are complete; missing email/Area and historical
provider-association evidence remain an operational follow-up before real payment.
No root, invitation, payment or membership state was changed in production.

## Evidence

- Current frontend lockfile installed with existing `node:24-alpine` tooling.
- Frontend production audit: PASS, zero vulnerabilities. The installation's
  development advisories do not establish a production-audit failure.
- Final full frontend: 590 passed across 134 files, zero failed/skipped, including
  the final permission-negative and composite-founder-error regressions.
  An earlier parallel run timed out in an unchanged administrative
  component; serial execution passed without changing timeouts or assertions.
- Node-24 production build, TypeScript and ESLint passed; one carried-forward
  pilot `window.location.assign` lint warning remains, with no lint errors.
- Desktop/mobile Chrome browser contracts: 12 passed, zero failed/skipped, against
  the production build and local mock API. Initial selector failures were corrected
  by distinguishing application alerts from Next's route announcer, retaining the
  substantive error, no-checkout and duplicate-request assertions.
- Release backend restore/build: PASS. Production backend source diff: empty.
- Existing joining application suite plus new independent/recruiter-only rejection
  regressions: 43 passed, zero failed/skipped. Includes invitation boundaries,
  full/two-instalment confirmation, duplicate preparation and separate approval.
- Full backend execution was attempted, not passed: a recovery-host startup timeout
  occurred in unchanged migration tests and the run exceeded the local 20-minute
  execution limit. Only its identified disposable leftover container was stopped.
  Complete hosted CI remains required; no tests or timeout assertions were removed.
- The separately selected Clean Start PostgreSQL scenario is opt-in and skipped
  with `REPRO_PG` unset. This is not counted as executed root/bootstrap evidence.
- API-project direct/transitive NuGet audit completed with the explicit existing
  configuration. It reports pre-existing runtime advisories in AutoMapper,
  Azure.Identity, Caching.Memory, Microsoft.Identity.Client, ImageSharp and .NET
  crypto/JSON dependencies. AngleSharp has a separate existing test-only advisory.
  Package/project/lock files are unchanged; this PR neither introduces nor remediates
  those advisories and does not claim a clean backend audit.

## Review and release limits

- The correction is in the frontend caller/presentation layer; no runtime backend
  guard, provider, schema, source pin or activation binding changes remain.
- No arbitrary independent AQGreen root action exists, including direct use of the
  former shared join dialog. Its legitimate Onyx path remains separately scoped.
- Missing joining amounts/schedules are not inferred as zero/full payment. Unknown
  outcomes remain held for status review. A reminder is not terminal provider proof
  and cannot be silently cleared from a null member-read checkout projection.
- First-root selection and later membership approval require their existing separate
  specific business authorization. No new approval operation or fake request button
  is introduced. The identified customer's eligibility is not inferred from browser
  choices, role labels or a successful production health check.
- Composite first-founder rejection is not mislabelled as an email-only problem:
  the existing guard also checks the effective registration boundary and other
  account requirements. Guidance requires eligibility review and does not promise
  that every registered account can be selected.
- The focused branch is explicitly disabled in the existing Vercel Git-deployment
  map, preventing a branch push/PR from requesting an automatic preview deployment.
  The existing main-only qualified release workflow is unchanged.
- Intended release is frontend-only after explicit authorization, retaining the
  existing commissioned API artifact. No merge or automatic deployment is requested.
- Implementation/browser verdict: supported frontend paths TESTED locally. Genuine
  provider/customer completion is not PRODUCTION VERIFIED. Full backend/hosted CI
  and operational readback limits prevent a blanket MERGE READY claim.

## Next action

Review the draft PR and complete required hosted/backend gates before authorizing
a frontend-only release. Resolve the separately recorded genuine-customer
eligibility/readback limits before any real joining payment or Host mutation.
