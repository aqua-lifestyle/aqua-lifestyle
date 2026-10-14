# AQGreen V2 — administrator sales authorization qualification

**12 October 2026 · isolated MVP · no production enablement**

## Exact authorization change

The owner's current instruction confirms eligible administrators may review,
hold, confirm and reject weekly sales within existing authorized records.
The former host-only sales-review permission is now available to Host and
Tenant sides. Existing Admin/SystemAdmin role-seeding conventions grant the
eligible permission; custom delegation and explicit denials remain subject to
the permission model. Administrator role or a client claim alone is insufficient.

Every sales-review endpoint independently checks the authenticated user's
dedicated review permission. A Tenant caller must stay in the same Tenant and
have an effective, unrevoked, undeleted assignment to the member's active Area.
List filtering occurs before count/pagination. Detail/latest-week and
hold/confirm/reject apply the same record scope. A central Host caller requires
the separately granted `AllTenants` permission; it is never inferred from an
administrator role or given to Tenant callers as a bypass.

Writes authorize in the owned transaction, preserve the existing per-review
advisory lock and recheck scope before reading a finalized decision. The existing
customer/Area/assignment authority stabilizer locks those rows until commit for
Tenant review writes, including exact retries. Reviewer/time/evidence and final
history retain their existing immutable rules and transaction semantics.

The change supplies no commission amount input or threshold override. The system
still applies versioned 5/5/5 quantities; calculation, release and record-payment
permissions remain separate and Host-only. Production V2/write gates remain
disabled by default. The isolated demo's existing payout denial is preserved.

## Verification

The focused direct-HTTP suite uses actual JWT
authentication and PostgreSQL via a local Unix socket inside `bwrap --unshare-net`.
Tests cover Area and central success; cross-Area/Tenant, missing permission,
inactive/revoked Area and anonymous denial; scoped reads; holds/rejection;
duplicates and immutable audit; authority revocation during an in-flight review;
client-supplied financial/audit overrides; and existing seeded host/admin access.
Existing sales concurrency, permission, read/safety and UI component tests are
also selected. Joining, spillover and browser workflows are carried forward.

| Executed check | Result | Evidence boundary |
| --- | --- | --- |
| `AQGreenSalesReviewAuthorizationPostgreSqlTests` | **14 passed, 0 failed, 0 skipped** | Real JWT/direct HTTP against disposable PostgreSQL, under a denied network namespace using a Unix socket; includes hold creator/time and authority-lock race. |
| `AQGreenWeeklySalesEligibilityApplicationPostgreSqlTests` | **8 passed** | Existing held/confirmed/rejected state, conflict, rollback and concurrent exact/conflicting confirmations in disposable PostgreSQL. |
| Existing sales safety/read tests | **11 passed** | Application authorization, production-disabled gate, audit/input contract, scoped denial and host read compatibility. |
| `AquaRolePermissionsTests` | **50 passed** | Permission registration, eligible review on both sides and continued Host-only financial mutation permissions. |
| Existing admin review/sidebar/participation UI components | **19 passed** | Permission-based navigation and review controls; no frontend source change. |
| Solution restore, Release build and `git diff --check` | **passed** | Restore was up to date; final solution build succeeded with the existing test-only AngleSharp advisory warning. |

The combined backend regression selection reported **69 passed, 0 failed,
0 skipped**. No full backend suite, new browser workflow, CI, deployment or
production test is claimed. The prior browser joining/spillover/unreleased-ledger
evidence was preserved and not rerun. The package advisory review completed
and still reports pre-existing direct/transitive vulnerabilities; no package
or dependency change was introduced, and no clean dependency-audit claim is made.

Initial runs revealed fixture assumptions rather than accepted product behavior:
the seeded tenant admin already had an Area assignment; custom test users without
linked customers disrupted repeated seed startup; the existing domain-conflict
HTTP envelope returns 500; and the commission calculation route is
`CalculateLatestClosedWeek`. Those fixtures/contracts were corrected before
current execution. No assertion was weakened to allow unauthorized review.

## Acceptance

`IMPLEMENTED` and `TESTED` for the isolated administrator-review workflow.
The former host-only acceptance gap is closed. No technical authorization
blocker remains in the exercised isolated paths. Agent 2's newer
[independent checklist](aqgreen-v2-independent-operational-mvp-acceptance-checklist.md)
was inspected after this evidence completed: its pending-authorization-evidence
item is now answered by the results above. Its separate scope-label defect was
closed in the final alignment below, without widening Area permissions.

### Final administrator review alignment — READY FOR OWNER WALKTHROUGH

The weekly earnings selector, placeholder, validation message and ledger column
now say **Tenant**. The selector still loads `AdminTenant/GetAll`, and the focused
component test asserts the exact calculation request `{ programme: 0, tenantId: 3 }`.
The shared selector's defaults for other screens were preserved. The saved
browser test's selector was updated; the broader browser workflow was not rerun.

Focused current execution: **7/7** weekly earnings component tests and **9/9**
persisted permission-mapping tests passed, plus TypeScript, scoped ESLint and
diff checks. The permission tests use disposable SQLite seed data, not production
or PostgreSQL authorization evidence. The previously accepted 14/14 direct-HTTP
PostgreSQL authorization checks remain carried-forward evidence; their backend
implementation was not changed in this alignment.

Tenant `Admin` and `SystemAdmin` are the intended Area-administrator roles in
the current model. Both receive the review grant, and neither receives the
Host-only `AllTenants` or financial-mutation grants from Tenant seeding. Host
`Admin` retains its explicit review and central-scope grants. Business roles
`AreaLeader`, `Facilitator`, `Member` and `Guest` gain no administrator review
authority from their names alone. Explicit review denials survive reseeding
without duplicate permission rows. **No role-mapping discrepancy was found.**
Custom administrator delegation remains explicit; Area assignments and central
permissions continue to govern record access. This verdict is for an isolated
owner walkthrough; it does not record user observation, a pilot launch or
production readiness.

Alignment files: `AdminAreaSelectionField.tsx`, `AdminWeeklyEarnings.tsx`, its
component test, the existing demo browser-test selector, and
`AQGreenAdministratorSalesPermissionMappingTests.cs`. No production
authorization implementation, role seed mapping or broader scope was changed.

The earlier R400 browser result is an unreleased calculation, not by itself a
positive financial-hold demonstration. The earlier controlled R150 financial
hold is separate, carried-forward evidence. Actual user observation and external
pilot launch retain their separate owner-authorization boundary; production
enablement and financial operations remain outside this decision.

## Files in this change

- `AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Core/Authorization/AqualLifeStyleAuthorizationProvider.cs`
- `AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Application/Admin/Commissions/AdminAQGreenWeeklySalesEligibilityAppService.cs`
- `AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Application/Admin/Commissions/DisabledAQGreenWeeklySalesReviewGate.cs`
- `AqualLifeStyle/9.4.2/aspnet-core/test/AqualLifeStyle.Web.Tests/Controllers/AQGreenSalesReviewAuthorizationPostgreSqlTests.cs`
- `AqualLifeStyle/9.4.2/aspnet-core/test/AqualLifeStyle.Tests/Application/AdminAQGreenWeeklySalesEligibilityReadTests.cs`
- `AqualLifeStyle/9.4.2/aspnet-core/test/AqualLifeStyle.Tests/Application/AdminAQGreenWeeklySalesEligibilitySafetyTests.cs`
- `AqualLifeStyle/9.4.2/aspnet-core/test/AqualLifeStyle.Tests/Authorization/AquaRolePermissionsTests.cs`
- Current decision register, V2 specification, administrative playbook,
  operational acceptance report, system index and the scoped execution log.

Proverbs 11:1 motivates truthful quantities and financial representation;
2 Corinthians 8:20–21 motivates accountable delegation and recorded review
authority. These are engineering applications; they do not create global
administrator rights or financial entitlement.

No production, real provider, customer record, outreach, deployment, B6/B7 or
Git publication occurred. Task-owned PostgreSQL, network, socket and temporary
secrets were removed. This is technical qualification, not real user learning.
