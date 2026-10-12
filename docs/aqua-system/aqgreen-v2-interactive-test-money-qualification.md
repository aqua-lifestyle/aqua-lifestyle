# AQGREEN V2 — INTERACTIVE TEST-MONEY MVP QUALIFICATION

**12 October 2026 · local engineering evidence · IMPLEMENTED and TESTED.** The
interactive AQGreen V2 slice ran in a disposable, loopback-only PostgreSQL demo.
It has not been committed, deployed, enabled in production, observed with real
users, or market-validated. No commercial or policy decision is confirmed here.

## Browser journey and reuse

The existing invitation, member programme, Area approval, weekly sales-review,
weekly-earnings and member progress pages now connect through a conspicuous
synthetic checkout. A first-time Guest opens a sponsor link, sees the R1,200 or
two R600 joining choice plus the separate R600 monthly obligation, accepts the
invitation, sees the exact server-recorded simulated amount, confirms it, sees
pending Area approval, and returns after an authorized admin approves. The
member then sees active V2 placement/progress. A host reviewer records 5/5/5
synthetic evidence, calculates the versioned R400 commission, and the member
sees that it is a **test calculation with no payout**.
The Guest identity is a disposable fixture: this proves first-time AQGreen
participation through the browser, not new account registration.

The existing joining/payment confirmation processor, atomic approval/placement,
progress evaluator, sales review, commission engine and ledger remain the
business mechanisms. The first-time attribution correction from the preceding
[backend qualification](aqgreen-v2-lean-mvp-report.md) remains intact. New work
is the guarded demo checkout adapter, owner-only confirmation endpoint, visible
checkout/banner, and accurate demo financial labels. Production V2 selection
remains disabled by default; V1 and Onyx rules were not migrated or redefined.

The exact banner is **TEST ENVIRONMENT — SIMULATED MONEY — NO REAL PAYMENT OR
EARNINGS**. Demo activation still requires authorized Area approval after the
test payment. Admin payout actions are hidden in demo mode, and the backend
rejects direct release and record-payment calls. The demo capability requires
the exact `AQGreenV2Demo` environment, explicit opt-in, a named PostgreSQL
database on loopback, and an HTTP loopback browser origin. The demo checkout
gateway has no provider client and accepts AQGreen joining only; the real Yoco
gateway separately rejects demo selection before its HTTP call.

## Files changed for this interactive phase

- Frontend: `app/layout.tsx`, `app/test/aqgreen-checkout/page.tsx`,
  `src/components/members/{aqgreen-v2-demo-banner,aqgreen-v2-test-checkout,member-programmes,programme-invitation-landing,programme-journey-overview}.tsx`,
  `src/components/admin/AdminWeeklyEarnings.tsx`,
  `src/providers/SystemHealth/contract.ts`, `src/shared/api/endpoints.ts`;
  component tests and `e2e-real/{aqgreen-v2-first-time,aqgreen-v2-demo}.real.spec.ts`.
- Backend: `Web.Host/AQGreenV2Demo/{AQGreenV2DemoConfiguration,AQGreenV2DemoCheckoutGateway,AQGreenV2DemoFixtureSeeder}.cs`,
  `Web.Host/Controllers/{AQGreenV2DemoCheckoutController,HealthController}.cs`,
  `Web.Host/Startup/Startup.cs`,
  `Application/{Admin/Commissions/AdminCommissionAppService,Payments/Yoco/YocoCheckoutGateway,ProgrammeParticipations/ClubMemberProgrammeParticipationAppService,ProgrammeParticipations/ClubMemberProgrammeProgressAppService}.cs`,
  `Core/Domain/Payments/HostedPaymentCheckout.cs`; focused tests in
  `test/AqualLifeStyle.Tests/{Payments,WebHost}`.
- Documentation: this report, the pilot worklog, and the discovery script's
  product-identity correction. Other local first-time joining and Agent 2
  research files remain their existing workstreams.

## Actual tests and results

| Evidence | Result | Limit |
| --- | --- | --- |
| Release backend build | Passed, zero errors; pre-existing warnings | Compilation only. |
| Focused .NET demo guard, selectors and real-gateway isolation | 11 passed, 0 failed | Unit/component behavior. |
| Frontend type-check, focused ESLint, focused Vitest | Passed; 22 Vitest tests | Relevant UI/components, not all frontend tests. |
| Fresh disposable PostgreSQL migration and guarded host health | Passed; demo capability present | Local demo only. |
| Playwright first-time browser flow | 1 passed, 0 failed on final fresh database | Includes invalid code, wrong amount, duplicate confirmation, other member's checkout denial, guest admin denial, pending versus active, re-login after security-stamp change, V2 0/5 progress. |
| Playwright sales/commission browser flow | 1 passed, 0 failed on same final database | Includes initial Held for evidence, 5/5/5 review, R400 versioned calculation, member display, hidden payout controls, direct release/payment endpoint rejection and unchanged ledger. |
| Final PostgreSQL inspection | 1 synthetic R1,200 checkout, 1 attribution, 1 placement, 1 unreleased R400 record; 0 released/paid AQGreen ledgers | Synthetic records, not commerce or provider settlement. |
| Final clean-run server log and gateway tests | No provider endpoint/request marker in the final server log; real gateway rejected demo mode before HTTP in unit test | Log inspection is bounded to this local run. |

Earlier attempts found real defects and test drift: the initial demo gateway DI
registration was overridden by `AddHttpClient`, causing **one attempted Yoco API
request**, which returned HTTP 401; no payment was created or charged. The host
was stopped, DI registration was corrected, and the real gateway gained a
defense-in-depth demo guard before the final clean run. Earlier local setup or
test attempts also failed on password length, loopback origin, HTTPS checkout
invariant, proxyability, incomplete unit of work on rejection, pending-progress
selection, stale selectors, and a fixed calendar date. Those are fixed; the
passing results above refer to the final source and fresh data. The original
provider-request attempt must not be described as a run with no provider contact.

The preceding backend slice separately passed PostgreSQL concurrent-join,
attribution, placement and ledger tests, recorded in the prior report. Those
results are **carried forward**, not newly rerun for this browser phase.
Invitation expiration was **not tested**: no current expiry field or approved
expiry rule exists. It requires an owner product decision before implementation.
The browser proof uses the full R1,200 joining choice; the two-instalment browser
branch has component/backend support but was not separately exercised end to
end in this run. Anonymous invitation preview and account sign-up were not
separately exercised by Playwright; the consented pilot should observe that
entry point if it is included in the assigned task.

## User learning, usability and commercial acceptance

**Actual consented observation: NOT RUN.** No participant was recruited or
interviewed. Playwright actions are automated and cannot establish comprehension,
value, demand or an unassisted completion rate. An approved pilot should reuse
the [non-leading discovery and task scripts](aqgreen-v2-lean-mvp-discovery.md),
obtain consent, use synthetic accounts, record assistance/wrong turns and
interpretations separately from test results, and retain only minimized notes.
The post-approval session renewal is a specific comprehension/usability point
to observe. There are no observed user defects yet.

Carry Agent 2's [independent BV-01–BV-05 findings](aqgreen-v2-scripture-governed-mvp-business-validation.md)
into acceptance: obtain an owner-approved SKU/use card before a product-value
exercise; never call synthetic 5/5/5 review verified outside commerce; present
joining and monthly costs separately from conditional gross commission; make no
external insurer-cover claim; and do not invent due, refund or remedy policy.
The screen can test workflow comprehension now. It cannot validate product value
while the current product identity is unresolved.

Owner/product decisions still needed: current SKU and intended use; invitation
expiry policy; monthly due/first-liability and remedies; exact sales evidence
standard; commercial funding and return treatment; and the scope of any actual
participant pilot. Qualified South African counsel must review the actual
offer, compensation funding, consumer disclosures, cancellation/refunds,
insurance representation and data handling before commercial activation.
No rule or legal status is confirmed by this report.

## Minimum next step and recommendation

**PROPOSED CONTINUE** the isolated, consent-based usability pilot after owner
approval of outreach and the relevant SKU card for product-value questions.
Use the prepared scripts and capture actual observations; change unclear
screens based on recorded behavior. **STOP commercial activation** until genuine
goods/services demand independent of recruitment, sustainable funding and
competent legal review have evidence. No production V2 activation, payout,
loan, Onyx expansion, B6 migration or B7 cutover follows from this qualification.

Progress remains: **genuine member value + correct business operation +
truthful financial evidence + validated learning**. This run supports the
middle two as a synthetic technical exercise only.
