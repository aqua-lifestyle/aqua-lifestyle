# AQGREEN V2 — OPERATIONAL MVP ACCEPTANCE REPORT

**12 October 2026 · local isolated candidate · no production authorization**

This report records technical evidence for the isolated, synthetic-money
membership workflow. It is not a business-policy confirmation, commercial
launch decision, real-payment receipt, or actual user observation. No external
participant was contacted. The prior uncommitted working slice and Agent 2's
independent [existing-business analysis](aqgreen-v2-existing-business-operations-and-mvp-alignment.md)
were preserved.

Under Christ's lordship, Proverbs 11:1 calls for honest measures, Proverbs
18:17 for examined claims, Proverbs 21:5 for diligent work, 1 Corinthians 4:2
for faithful stewardship, and 2 Corinthians 8:20–21 for accountable handling
of entrusted funds. Exact test amounts, independent PostgreSQL/browser checks,
immutable review records and a payout hold are **engineering applications** of
those principles; the passages do not themselves set a checkout amount,
commission rate, reviewer scope or legal conclusion.

## Operational workflow and corrections

The existing browser and backend, with a host-authorized sales reviewer, now support a valid sponsor invitation,
anonymous account signup and email confirmation, preserved sponsor attribution,
AQGreen participation, either approved joining schedule, synthetic checkout,
pending Area approval, authorized activation with deterministic V2 placement,
member progress, host-admin manual weekly sales confirmation, versioned
commission calculation, and an unreleased test ledger. Onyx, retail
e-commerce, inventory, loans, real payout, V1 migration and B7 remain outside
this slice.

This phase corrected two implementation gaps. Demo registration now removes any
previous real Yoco gateway registration and installs only the no-network test
gateway; startup also refuses to serve a demo if the resolved gateway is real.
The real gateway itself rejects demo-mode requests before HTTP. Account email
verification now retains the URL-fragment token across React Strict Mode's
repeated effect while removing it from browser history. A tenant-admin-only,
synthetic-address-only test mailbox exposes an encrypted outbox verification
link in memory exclusively in the validated demo environment. It rejects other
tenant/session scopes and remains unavailable when the strict demo guard fails.

## Test-money isolation and joining evidence

The .NET gateway/registration suite passed **13/13** under `bwrap --unshare-net`.
It covers accidental real gateway registration and a demo flag/environment
mismatch. The API and migrated PostgreSQL test database also ran together on a
Docker `--internal` network whose route table contained only its own subnet,
with no default route. The real provider was never deliberately called in this
phase. Browser tests used only the synthetic checkout endpoint; wrong amounts
returned HTTP 400 and exact duplicate confirmations were idempotent. This is
technical provider isolation in the tested disposable topology, not evidence
about any production deployment.
An earlier browser qualification had reached the real Yoco gateway and received
HTTP 401. This phase did not repeat that request; the corrected registration,
runtime guard and network-denied execution address that specific regression.

The first-time, two-instalment browser run confirmed two distinct R600 synthetic
payments for one R1,200 joining obligation. After the first payment, the member
view displayed 50%, “Two instalments” and R600 remaining, without activation.
After the second, it displayed awaiting Area approval. The approved member had
one immutable recruiter attribution, one approval decision, and a V2 placement
at path `113` in the final replay (`111` in an earlier run); the original
recruiter differed from the placement parent,
demonstrating spillover without attribution substitution. A separate existing
full R1,200 browser path passed in the same isolated stack and covers the other
approved schedule, invalid code, wrong amount, duplicate checkout, another
member's checkout denial, pending approval and authorized activation. No deadline,
remedy, amount, or eligibility rule was invented.

## Placement, sales and commission evidence

The fresh PostgreSQL allocator/sales/commission test selection passed **43/43**.
The canonical allocator asserted first-level paths `1`–`5`, sixth through
tenth `11`–`15`, eleventh `21`, and a 16-writer concurrent canonical prefix
without duplicate slots. The active member's `111` placement is a separate
browser-to-database observation in the disposable stack.
Four more PostgreSQL application-path approval tests passed, covering exact
retry, concurrent approve/approve, concurrent same-scope placements and denial
to a sponsor-Area administrator lacking the participant Area assignment.

The browser host-admin sales scenario passed: the reviewer manually confirmed
5 Spray, 5 one-litre and 5 five-litre units with one technical evidence
reference. PostgreSQL recorded the closed-week period, reviewer identity and
time, `ManualReview` source, confirmed/met decision and
`AQGreenWeeklySalesEligibilityV1` rule version. This is staff confirmation,
not external transaction verification. The member then saw two test
commission components. Release and record-payment API attempts failed, and
the ledger's `ReleasedAt` and `PaidAt` remained null.

The R150 and R400 results are consistent scenarios under the recorded rates:
Level 1 has five qualifying members at R30 = **R150**; a complete Level 2
adds 25 at R10 = **R250**, for **R400 cumulative**. The earlier lean test
asserts Level 1 only; the full 31-member fixture and browser scenario assert
Levels 1 and 2. The disposable browser ledger used financial terms version
`aqgreen-v2-demo-commission`, decision version
`AQGreenWeeklyCommissionDecisionV1`, and components R150/R250, total R400.
The tests use generated `continuous-primary-*` terms versions with the same
150/250/1250 component amounts. These are synthetic calculations, not income
or funds available for payout.

The R400 browser calculation was unreleased; that observation does not itself
prove a positive financial hold reason. The earlier lean PostgreSQL R150 hold
case is distinct carried-forward evidence. Agent 2's subsequent checklist also
identified a UI scope-label issue, now closed by the final admin alignment:
weekly earnings labels its `tenantId` target **Tenant**. Seven focused UI tests
and nine persisted role-mapping checks passed; no role-mapping discrepancy or
remaining blocker was found for the isolated owner walkthrough. See the
[current alignment verdict](aqgreen-v2-admin-sales-authorization.md#final-administrator-review-alignment--ready-for-owner-walkthrough).

## Validation record and limits

| Execution in this phase | Result | What it proves |
| --- | --- | --- |
| Gateway/registration tests under `bwrap --unshare-net` | 13 passed | Fail-closed demo selection and no provider network capability in that test namespace. |
| PostgreSQL allocator, sales and commission selection | 43 passed | Durable, concurrent allocator and financial-state mechanics in disposable PostgreSQL. |
| PostgreSQL approval/Area selection | 4 passed | Atomic retries/concurrency and one cross-Area denial on the application path. |
| React verification component, including Strict Mode | 5 passed | One-time token capture and confirmation presentation. |
| Playwright isolated browser: signup + two R600 instalments; R1200 guest; manual sales + ledger | 3 passed | The named browser journeys against an internal-network API/PostgreSQL stack. |
| Frontend TypeScript and focused ESLint; backend Release build; `git diff --check` | passed | Build/static checks of the local changes. |

A broad invitation/application test selection was started but cancelled after
its test host consumed CPU without completing; it supplies **no passing result**
for this report. Expired **programme** invitations were not exercised in the
browser; the tested invalid code was rejected. No Yoco or Bird provider test,
real participant observation, actual bank reconciliation, full suite, CI,
deployment or production check was executed. Previous reports remain
carried-forward evidence only.

## Owner and administrator walkthrough

Use only a freshly migrated, task-owned PostgreSQL database and API on an
internal network without an outbound route. Set the exact `AQGreenV2Demo`
environment and local opt-in; point the client to the local frontend and use
generated synthetic `.test` accounts. Confirm API health advertises the demo
capability and every relevant page displays **TEST ENVIRONMENT — SIMULATED
MONEY — NO REAL PAYMENT OR EARNINGS**.

1. Sign in as the synthetic sponsor and open **Invitations**. Copy the AQGreen
   invitation code; open it in a clean browser session. Check programme terms,
   the sponsor identity and the simulated-money warning.
2. Create a synthetic member account, verify its email via the guarded local
   test mailbox, and sign in. Choose either R1,200 once or two R600 payments.
   At checkout compare the exact amount before confirming. For instalments,
   observe the remaining R600 and repeat checkout with a distinct record.
3. Before admin approval, verify **Awaiting Area approval** and absence of
   active placement. Sign in as the authorized Area admin, review the payment
   evidence and approve once. Sign in again as the member after the role
   transition; compare sponsor attribution, placement and progress.
4. As the authorized admin, open **AQGreen weekly sales reviews**, inspect the
   synthetic evidence reference and enter 5/5/5 only when the test evidence
   warrants it. Record the manual review, then prepare weekly earnings. Compare
   R150 and R250 components with the R400 total and confirm that release and
   paid actions remain unavailable. Test shortfall/rejection separately; do
   not mark synthetic amounts as payable.

This walkthrough prepares a consent-based observation. Actual administrators
or customers, outreach, existing member records, provider access or pilot
launch require separate owner authorization. Automated outcomes above are not
market validation or evidence that a real participant completed the tasks.

## Remaining owner decisions and limits

**Closed by the final owner decision and focused implementation, 12 October
2026:** the former host-only sales-review acceptance gap is resolved.
The owner's direct instruction confirms eligible administrators review within
their authorized Areas, with separately granted central scope. The
[administrator sales authorization qualification](aqgreen-v2-admin-sales-authorization.md)
records the exact change, 14 real JWT/direct-HTTP PostgreSQL checks and 69
backend regressions plus 19 UI component checks. Tenant and Area isolation,
scope on retries, immutable review audit and financial holds are preserved.
No global access is inferred from an administrator role. Agent 2's
[administrative playbook](aqgreen-v2-mvp-administrative-operations-playbook.md)
has been aligned with that direct owner decision; coordination of cross-Area
duplicate claims remains an operating assignment, not a reason to reinstate
host-only eligibility.

Agent 2's analysis also identifies the operational questions that still need owner
answers before a real-money workflow: accepted sales documents and submission
channel; reviewer delegation, period cutoff, custody and retention; how current
bank/cash/manual joining receipts are independently verified and reconciled;
and how exceptions or disputed evidence are handled. If real member records
are ever considered, their sponsor/payment/approval history needs governed,
member-specific evidence and a separate migration decision. Competent South
African legal review remains necessary for the actual goods/service value,
compensation basis, health claims and consumer-law treatment before commercial
V2 activation. No product-sales platform or unapproved SKU is a prerequisite
for this isolated operational pilot.

**Status:** `IMPLEMENTED` and `TESTED` in local disposable infrastructure only.
There is no `MERGED`, `DEPLOYED`, `ENABLED` or `PRODUCTION VERIFIED` claim.
