# Branch Cleanup Summary

## Outcome

The branch cleanup now includes the previously uncommitted frontend and backend work. The review aligned the implemented workflows with the documented customer, membership, order-intent, facilitator, area-leader, RBAC, and tenant-isolation rules.

## What Changed

- Restricted Guest and Member access to self-service operations and added negative authorization tests.
- Enforced tenant-aware membership reads and customer ownership for customer, membership, and order operations.
- Provisioned a Customer profile for newly registered accounts and repaired eligible legacy accounts idempotently.
- Added live customer and facilitator workflows, including membership selection and current-customer order placement.
- Centralized role normalization and safe role-based redirects.
- Centralized order status presentation, hydration readiness, and safe API error messages.
- Removed 616 lines of unreferenced frontend components.
- Made fixed-password tenant demo scenarios opt-in with `AQUA_SEED_DEMO_DATA=true`; normal startup does not create or refresh those accounts.
- Added an optional monitoring transport through `NEXT_PUBLIC_MONITORING_ENDPOINT`, application error reporting, and Core Web Vitals reporting.
- Added reproducible `type-check`, `test:coverage`, and noninteractive Next.js 16 `analyze` commands.
- Upgraded Vitest to the first release that fixes its critical advisory and pinned the compatible security-fixed Vite 6 release.

## Atomic Commits

- `edbcc8c` `docs: add branch cleanup diagnostic report`
- `f3de32a` `fix(security): enforce least-privilege customer access`
- `befdb52` `feat(auth): route users by business role`
- `97ec71a` `refactor: centralize order status presentation`
- `15f129d` `fix(hydration): centralize client readiness snapshot`
- `4424058` `feat(dashboard): connect customer and facilitator workflows`
- `3b599d9` `refactor(api): centralize safe request errors`
- `b7f921d` `fix(seed): harden tenant account provisioning`
- `8e23d54` `fix(api): allow cold backend startup`
- `4b3d52c` `chore(quality): add reproducible verification commands`
- `f39a755` `chore: remove unreferenced frontend code`
- `ca34501` `feat(observability): report errors and web vitals`

## Verification

| Check | Result |
| --- | --- |
| `npm run type-check` | Pass, zero TypeScript errors |
| `npm run lint` | Pass, zero ESLint errors or warnings |
| `npm test` | Pass, 69 files / 205 tests |
| `npm run build` | Pass, 35 static pages generated |
| `npm run analyze` | Pass, report written to `.next/diagnostics/analyze` |
| ASP.NET solution tests | Pass, 262 application + 1 web test |
| Production dependency audit | No high or critical findings |

## Measured Residual Gaps

- Whole-frontend coverage remains below the requested 80% target. The final reproducible measurement is 60.07% statements, 70.17% branches, 51.4% functions, and 60.07% lines (up from the 57.8% statement/line baseline). The largest gaps are legacy provider implementations, route composition files, memberships/order-intents screens, and admin/area-leader UI modules. This is now measurable with `npm run test:coverage`, but it is not presented as complete.
- `npm audit --omit=dev` reports two moderate findings for the PostCSS copy vendored by Next.js 16.2.10. npm proposes an invalid downgrade to Next.js 9.3.3, so no forced downgrade was applied.
- The ASP.NET solution still emits its existing XML-documentation, nullable-context, obsolete AutoMapper, and missing MVC static-library warnings. Tests pass, but the repository-wide warning backlog is not zero.
- Browser-console hydration checks and end-to-end manual role journeys were not automated in this branch. Production SSR generation and component tests pass, but a real-browser E2E suite remains desirable.
- A monitoring collector must be configured through `NEXT_PUBLIC_MONITORING_ENDPOINT`; without one, errors still reach the browser console and telemetry is intentionally not transmitted.

## Principles Applied

- DRY: shared role routing, order status, API errors, hydration state, and tenant demo-account creation.
- Separation of concerns: authorization in application services, account repair in seeding infrastructure, and monitoring behind one transport boundary.
- KISS: opt-in demo data uses one explicit environment switch and secure-by-default behavior.
- SOLID: focused helpers and policy boundaries replaced repeated component and seed logic without introducing framework abstractions.
