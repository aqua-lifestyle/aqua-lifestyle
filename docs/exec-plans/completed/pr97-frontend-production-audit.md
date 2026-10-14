# PR #97 frontend production dependency audit

> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

## Goal

Resolve the frontend production audit on `feature/admin-ui-remediation` with the
smallest compatible dependency changes. This is the security evidence record
required for the long-running validation work; application and CI configuration
changes were unnecessary.

## Authoritative references

- Owner's frontend-only PR #97 repair instructions, including commit/push
  authorization and the prohibition on merging or investigating the backend timeout.
- [Repository validation](../../development/validation.md).
- [Next.js 16.3 release](https://nextjs.org/blog/next-16-3).
- [Next.js 16.3.6 security release](https://github.com/vercel/next.js/releases/tag/v16.3.6).
- [Axios 1.20.0 compatibility notes](https://github.com/axios/axios/pull/11141).
- [Sharp advisory](https://github.com/advisories/GHSA-rgj7-g3m4-5g8c).
- [baseline-browser-mapping advisory](https://github.com/advisories/GHSA-w5vr-8v7q-w6rv).

## Confirmed decisions

No business-policy decisions were made. Scope and publication authorization come
from the owner's instructions for this task.

## Assumptions

No production state is inferred from local tests. Deployment compatibility is
bounded by repository Node 24 configuration, Linux browser execution, and the
local Node 24 Alpine web-image build/runtime experiment.

## Current state and completed work

Local remediation is IMPLEMENTED and TESTED. Package files were byte-identical
to `origin/main` before repair: this exposure is PRE-EXISTING.

| Package | Before | After | Dependency relationship and rationale |
| --- | --- | --- | --- |
| Axios | 1.18.1 | 1.20.0 | Direct; lockfile refresh within unchanged `^1.18.1`. All twelve reported advisories are fixed at 1.20.0. |
| Next.js | 16.2.11 | 16.3.6 | Direct exact pin; minimum fix for the reported `next/og` RCE. Windows and AVIF advisories are fixed at 16.3.3. No major migration; React 19.2.4 and Node 24 meet unchanged peers/engines. |
| Sharp | 0.35.3 | 0.35.4 | Next.js optional production dependency; update existing override to match Next.js's new `^0.35.4` requirement. Bundled libvips/libheif and platform binaries move together. |
| baseline-browser-mapping | 2.10.42 | 2.11.0 | Next.js production dependency (`^2.9.19`), also shared with dev-only browserslist ancestry. Lockfile refresh satisfies both parents; no new declaration or override. |

Next.js's env/SWC packages and required `@swc/helpers` update with the framework.
Sharp's WASM runtime dependency requires `@emnapi/runtime` 1.11.3. The diff also
contains registry license/libc metadata on refreshed dependency entries. No
unrelated stack upgrade, React change, application change, or audit suppression.
The ESLint configuration remains on its existing version and passes validation.

Axios uses ordinary configuration objects, a tenant-header request interceptor,
refresh/error response handling, JSON/form-urlencoded requests, and default query
serialization. Inspection found no application custom adapters, custom Fetch
implementations, inherited config templates, HTTP/2 options, cancellation,
upload/download progress configuration, or custom response transformations.

## Evidence (2026-10-05 local execution)

- Initial environment: Node 26.6.0, npm 11.18.0; npm lockfile version 3.
  All remediation validation used Node 24.21.0 with npm 11.18.0.
- `npm audit --omit=dev` and its `--json` variant reproduced exit 1:
  four affected production packages (one moderate, two high, one critical).
- Clean `npm ci` passed. Post-fix `npm audit --omit=dev` and JSON audit passed
  with exit 0, empty `vulnerabilities`, and zero at every production severity.
  The unrestricted install audit still reports dev-inclusive findings; it is
  not the production gate and was not remediated in this scoped task.
- `npm run lint` and `npm run type-check` passed.
- `npm test`: 126 files / 492 tests passed, matching the supplied baseline.
  The first invocation was interrupted by the terminal's 120-second limit;
  the full rerun passed. React test `act` warnings remain visible.
- Production build passed with 65 generated pages; the E2E preparation build
  and Alpine web-image build also passed with the same page count.
- Complete existing Playwright suite, Chrome channel, one worker, retries 0:
  48 passed / 0 failed / 0 retried / 0 skipped. This includes phone/tablet/desktop
  viewport matrices, themes, dialogs, scrolling, focus, selections, permissions,
  and auth-navigation coverage against synthetic fixtures.
- Earlier two-worker admin run alongside the container build had three test
  timeouts and was interrupted at the terminal's 25-minute limit. All those
  cases passed in the full serial run; additional serial checks passed the
  desktop import case three times and both affected mobile cases once more.
  Resource contention is an INFERENCE, not a proven root cause.
- Local `docker/web/Dockerfile` build passed on Node 24 Alpine. In the resulting
  standalone image, Sharp 0.35.4/libheif 1.23.2 successfully encoded AVIF,
  decoded/resized it, and produced PNG using the musl binary.
- `git diff --check` passed. UI source, permission predicates, API payloads,
  authorization/Tenant/Area logic, finances, programme transitions, AQGreen
  placement, weekly sales, commissions, and backend/workflow files are unchanged.

## Open questions and next action

Publish the separate security follow-up and inspect PR #97's checks at its new
HEAD. Hosted evidence is available through the PR, not established by these local
results. The backend timeout remains a SEPARATE UNRESOLVED CI BLOCKER; container
CI depends on backend and frontend success. Do not merge unless all required
gates are green. No backend timeout investigation is authorized by this worklog.

## Git/branch context

- Repository: `aqua-lifestyle/aqua-lifestyle`; PR #97 open and mergeable at baseline.
- Branch: `feature/admin-ui-remediation`.
- Starting HEAD and remote branch: `0e92e347123b6584cba09dd2a39d174fff15ac4b`.
- `origin/main` and merge-base: `26803037d9923842db12de621ab6f87c3cd045cc`.
- Starting ahead/behind: remote branch 0/0; main 1/0. Main remained unchanged
  at the pre-publication fetch. No rebase was required.
- Security follow-up files: frontend `package.json`, `package-lock.json`, and
  this repository-required security validation worklog.
- Unrelated pre-existing untracked research files were inspected for ownership,
  preserved, and excluded from staging.
