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

## 2026-11-14 protected-CI review and release verdict (NO-GO)

- Final protected CI for 7ff1d25…: run 43571825810 (pull_request, attempt 1),
  conclusion success, completed 2026-11-14T07:52:28Z. All 17 jobs succeeded,
  including every backend application/web shard, the PostgreSQL
  transactional-invitation regressions and the frontend quality job
  ("Test Files 134 passed (134)", "found 0 vulnerabilities",
  "Compiled successfully").
- Actual diff re-verified from the PR patch: 17 files (+911/−238); backend
  runtime sources, .github/workflows and frontend package/lock files remain
  byte-identical to commissioned b8d86e5… (git diff --exit-code clean). The only
  backend change is the test-project regression
  V2Joining_IndependentOrRecruiterOnlyRequestCannotCreateParticipationCheckoutOr
  Payment. Admission/D10/Yoco/Tenant/Area/approval safeguards unchanged.
- New release blocker found by a temporary local counterexample
  (/tmp/opencode/pr131-invitation-replay.spec.cjs, 1 passed, no production
  calls): replayed invitation after one confirmed R600 instalment. Backend
  StartEntry is idempotent for an existing participation
  (ClubMemberProgrammeParticipationAppService.cs:247-258), so the landing page
  reaches the schedule step with joiningSchedule=1 and 600 paid. The landing
  keeps the default "Pay R1,200 once" radio and posts
  CreateAQGreenJoiningCheckout {schedule: 0}; the domain forbids a schedule
  change after a verified payment (EntryParticipation.cs:214-218) and rejects
  it. The page writes the part-paid checkout-review marker BEFORE the checkout
  succeeds (programme-invitation-landing.tsx:124-130) and nothing clears or
  advances the marker (use-checkout-review.ts allows only unpaid→part-paid), so
  My programmes then blocks the legitimate second-instalment action
  (member-programmes.tsx:174,249). The user is left without an in-app path to
  pay the second R600. Pre-PR behaviour recovered via My programmes; the marker
  introduces a regression on the invitation path and defeats the PR's
  "prevent unsupported checkout" property.
- Release-path constraints verified in the qualified executor: the only release
  modes are full and the pinned schema54-member-reads-v1 pair (deploy.yml:20-27;
  member-release.json pins frontend 53341b57…, receiver 4eba3799…). The partial
  mode additionally requires the live Render deploy already at the qualified
  sha (release-control.mjs:1458-1459). Neither mode retains the commissioned
  Render deployment unchanged for a new sha; full mode deploys Render at the
  merge sha (backend sources identical, but still a fresh Render deployment).
- validateRun requires the qualifying CI to be a main-push run of
  .github/workflows/ci.yml named "Continuous integration" at the merge sha
  (release-control.mjs:83-86). The PR's green pull_request run cannot qualify a
  release; a fresh main CI run after merge is required.
- Verdict for this review: NO-GO for release as-is. The invitation-path
  regression must be fixed and re-verified first (a frontend-only fix remains
  compatible with the pinned backend). Customer-specific checkout
  reconciliation stays unresolved independently: existing Host DTOs do not bind
  active checkouts to the identified customer by ID and the historical provider
  charge outcome is not established, so no real payment is recommended and no
  Tenant-wide record is attributed.

## 2026-11-14 instalment-regression fix (PR #131 update)

Per the owner's "finish PR #131 without expanding scope" instruction, the proven
invitation-path regression was fixed inside the PR (frontend only):

- `use-checkout-review.ts`: the reminder lifecycle now takes authoritative
  `CheckoutReviewEvidence` from the caller's server reads. A marker is retired
  only on proven settlement: the first-stage (`unpaid`) reminder retires when the
  record shows the first joining instalment confirmed (`joiningPaidAmount > 0`);
  any reminder retires when the joining total is confirmed (statusCode
  PaymentConfirmedAwaitingApproval/Active). Ambiguous states (e.g. the deployed
  member read hiding PreparingCheckout without a URL) are never cleared by a
  refresh alone, preserving the established fail-closed doctrine.
- `programme-invitation-landing.tsx`: the AQGreen confirm is now two-phase. The
  confirm button runs idempotent StartEntry only; the authoritative record then
  selects the view: a part-paid record offers the legitimate second instalment
  ("Complete joining: R{nextPaymentAmount}") posting the recorded schedule 1; a
  paid-0 record with a persisted schedule continues that recorded plan; only a
  fresh record (paid 0, no schedule) auto-posts the visitor's selected schedule;
  complete or inconsistent records show status guidance without amounts.
  Schedule radios no longer render once a participation exists, so a replay can
  never default to R1,200 once or submit a schedule change.
- `member-programmes.tsx`: passes the same server-read evidence to the hook, so
  a confirmed first instalment retires a settled reminder and keeps an
  unresolved second-stage reminder fail-closed.
- Coverage: 5 new hook tests, 5 new landing tests, 2 new member-programmes
  tests, 7 new e2e cases (replayed invitation second R600, recorded-but-unpaid
  plan, completed replay, settled reminder retired through the invitation,
  second payment after re-sign-in, unresolved reminder surviving re-sign-in
  without duplicate requests). All 601 unit tests, all 24 aqgreen e2e cases and
  the member/auth/pilot/read-cutover e2e specs pass locally (Chrome); lint
  clean except the pre-existing pilot `location.assign` warnings.
