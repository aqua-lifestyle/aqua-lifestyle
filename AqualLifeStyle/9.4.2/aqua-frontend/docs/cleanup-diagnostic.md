# Branch Cleanup Diagnostic Report

**Branch:** `feature/ui-enhancement`

**Baseline date:** 2026-07-15

**Scope:** All committed branch work plus the existing uncommitted frontend and backend changes

## Validation Baseline

| Check | Result | Evidence |
| --- | --- | --- |
| Frontend TypeScript | Pass | `npx tsc --noEmit` exited with no errors. |
| Frontend lint | Pass | `npm run lint` exited with no warnings. |
| Frontend tests | Pass with runtime warnings | 66 files and 198 tests passed. Three navbar tests emitted React `act(...)` warnings. |
| Frontend production build | Pass | Next.js 16.2.10 compiled and prerendered 35 static routes after network access was allowed for Google Fonts. |
| Backend tests | Fail | Application suite: 256 passed, 2 failed. Web suite: 1 passed. |
| Coverage | Not configured | No `test:coverage` script or Vitest coverage provider is installed. |
| Bundle analysis | Not configured | No `analyze` script or bundle-analyzer configuration exists. |

The first sandboxed frontend build failed only because `next/font` could not reach Google Fonts. The same build passed when network access was available. The first sandboxed backend test run failed because MSBuild could not create IPC sockets; the unrestricted run reached the test suite and produced the results above.

## TypeScript and Lint Findings

- [x] No TypeScript errors.
- [x] No ESLint errors or warnings.
- [ ] Add explicit `type-check`, `test:coverage`, and `analyze` commands so the requested verification protocol is reproducible.
- [ ] Resolve asynchronous `AuthProvider` updates that are not awaited in three navbar tests.

## Hydration Findings

- The login tenant field reads persisted tenant state. The uncommitted implementation uses `useSyncExternalStore` to keep the server snapshot deterministic and is consistent with the bundled Next.js 16 hydration guidance.
- Several client-rendered dates use locale-sensitive formatting. These are deterministic in unit tests but require browser validation with server and browser locales intentionally different before claiming zero hydration warnings.
- Client-only APIs in authentication, tenant, clipboard, and dropdown code are called in effects or event handlers rather than during Server Component rendering.

## Duplication and Maintainability Findings

- Role normalization and role predicates are repeated across Admin, Area Leader, Facilitator, authentication, landing-page, and navigation modules.
- Role-to-dashboard routing and labels are independently encoded in the login form and landing page.
- Order status labels and badge tones are repeated in Area Leader and Member views.
- Provider error normalization is repeated across multiple providers and has already begun to drift (`AbpHttpError.details` versus the safe public message).
- The role predicates currently live in UI guard modules, causing non-UI consumers to depend on client guard components and their dependencies.
- Several new dashboard components contain long destructuring and conditional expressions that should be formatted for readability, but they have no current lint violations.

## Security and Tenant-Isolation Findings

- **High:** The uncommitted Guest-role seed grants `Pages.Customers`, `Pages.Memberships`, and `Pages.Products`. These are broad class-level legacy permissions, so a newly registered Guest can reach tenant-wide customer and membership reads rather than only self-service operations.
- **High:** `CustomerAppService.GetMyCustomerAsync` relies on the broad class-level customer permission instead of an explicit self-view permission.
- **Medium:** `MembershipAppService.GetActiveTiersAsync` uses the self-view permission but still inherits the broad class-level membership permission, defeating least privilege.
- **Medium:** The role-permission builder still combines `AquaRolePermissions` with legacy permission arrays, leaving multiple sources of truth as already identified in `docs/RBAC-Audit-Report.md`.
- **Pass:** The new order placement endpoint resolves the current customer using both `TenantId` and `UserId`, validates product and membership rules, and does not accept a caller-provided customer identifier.
- **Pass:** The membership repository includes host-owned membership tiers while limiting tenant-owned tiers to the active tenant.
- [ ] Add negative authorization tests proving Guest accounts cannot list customers or invoke privileged membership operations.

## Test Failures

1. `FacilitatorAppServiceTests.GetByCustomerAsync_ShouldReturnNull_WhenCustomerBelongsToDifferentTenant`
   - Fails before executing the ownership assertion because the new default Guest role does not have the legacy `Facilitators` permission.
   - The test must explicitly arrange the permission required by the service under test.
2. `EnquiryConversionFlowTests.ConvertEnquiry_SourcedByFacilitator_AttributesReferralsAndUpdatesNetwork`
   - Expects two referral records but finds four because newly seeded Facilitator demo activity is included in an unscoped count.
   - The test must scope assertions to the enquiry created by the test, and demo seeding should remain idempotent.

## Observability and Production Readiness

- The App Router has an `app/error.tsx` boundary that reports uncaught application errors to the browser console.
- Axios reports token refresh failures, but there is no structured logger or external error/telemetry transport.
- No analytics provider is selected. Introducing a vendor without product and privacy requirements would be premature; create a transport boundary before wiring a service.
- The backend build emits missing legacy MVC static-library warnings. The API and frontend build, but the MVC asset pipeline is not warning-free.
- Manual browser flows, hydration-console checks, Lighthouse, and external telemetry delivery are not automated in this repository.

## Business-Rule Cross-Check

- Order status values match the documented lifecycle: `Draft`, `Reserved`, `Cancelled`, `Completed`.
- A customer owns order intents; filtering member orders by `Customer.Id` rather than `User.Id` matches the domain model.
- Facilitator referral progress is based on direct referrals and remains a target workflow according to the business documents.
- Pricing and savings assumptions remain explicitly unvalidated in `docs/ValidationPlan.md`; cleanup must not silently hard-code unresolved business rules.

## Planned Cleanup Sequence

1. Fix the failing backend tests and least-privilege Guest authorization with negative coverage.
2. Consolidate role routing/predicates and order presentation helpers.
3. Remove provider error-normalization duplication and resolve React test warnings.
4. Add reproducible type-check, coverage, and bundle-analysis commands where the existing toolchain supports them.
5. Run final frontend and backend verification, then document residual manual/vendor-dependent checks.
