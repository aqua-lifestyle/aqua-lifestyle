# General member Area authorization

> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

> The owner's subsequent independent security review of `cc2709c` found a customer
> import bypass and a restoration source-authority race. The PASS below records
> the earlier review only and does not establish acceptance. Follow
> [security remediation](../active/member-area-security-remediation.md) for the
> corrections, new regression evidence and fresh final-commit review.

## Goal

Correct general member reads/mutations using existing Tenant/Area assignment and
granular permission authority; preserve the original portal and business flows.
Use isolated synthetic fixtures. Prepare a separate PR without merge/deployment.

## Authoritative references

- Owner's 2026-10-16 member Area authorization correction instruction.
- [Tenant and Area boundaries](../../aqua-system/08-area-and-tenant-boundaries.md).
- [Validation](../../development/validation.md) and [review benchmark](../../review-benchmark.md).

## Owner instruction

Owner authorizes this scoped correction, isolated checks, independent Agent 2
review and a separate PR. PR #103 remains an independent payment-approval package.
Production member writes, providers, financial operations, commissions, monthly
automation, D10, merge and deployment are excluded.

## Assumptions

Host oversight uses the existing action permission plus `Aqua.Admin.AllTenants`.
Tenant administration requires persisted current assignments to active Areas.
Current Area projections do not assert historical ownership. The overlapping
customer-account and legacy list routes are included because they address the
same member records. Separate User-account security permissions and member
self-service ownership checks retain their existing contracts. General members
here means the existing legacy `Customer.MembershipId` contract, not a new
interpretation of programme participation.

## Current state

Baseline inspection confirms member listing/lookup are Tenant-only and mutations
reuse that lookup. The UI labels Tenant ID as Area and discards server totalCount
after downloading the first 100 rows. This is a PRE-EXISTING code-level weakness;
no real-member access or demonstrated production exposure is claimed.

## Implementation

- Shared the existing persisted active Area/administrator-assignment predicate.
  Member list, lookup, plan options and every mutation enforce current Tenant,
  current effective Area assignment and existing granular action permissions.
  Host member oversight additionally requires `Aqua.Admin.AllTenants`.
- Scoped overlapping customer-account member reads/writes and the legacy customer
  list. Non-member customer authority and User-account security are distinct.
- Reused existing PostgreSQL authority locks in caller-owned READ COMMITTED
  transactions. Extracted the existing Customer row lock for customer-account
  commands so a concurrent non-member promotion cannot bypass member authority.
  Re-read scope after lock waits; identity/customer/outbox intent roll back together.
- Preserved explicit Customer/User deletion guards across EF's composed query.
  Deleted Area names remain unavailable. Host plan reads/validation use the existing
  generic repository with explicit global-or-resource-Tenant predicates; the custom
  membership repository's hard current-Tenant filter remains unchanged elsewhere.
- Returned actual current Area ID/name. Applied valid membership and authorization
  predicates before count/paging, with deterministic ordering. The original portal
  uses server totals/pages and server search while labeling Tenant separately.
- Added fourteen guarded PostgreSQL HTTP/permission/concurrency regressions,
  explicit SQLite component lock doubles and seven focused portal tests. CI requires
  all fourteen PostgreSQL cases to pass without skips in a disposable database.

## Evidence

- PR #103 was observed OPEN/draft at `9f2316d`, with 16/16 successful checks. No
  changes are made to that branch. Its isolated payment qualification does not
  establish a provider-to-administrator production workflow.
- Release restore and solution build passed; build retains existing analyzer/XML
  and dependency-advisory warnings. No dependency versions changed.
- Final frontend full Vitest: 132 files / 556 tests passed, including seven member
  screen regressions. TypeScript passed. ESLint passed with one existing invitation
  warning. Final Node 24 production build passed (local webpack build with synthetic
  configuration). Post-mutation refresh uses the current page/search generation.
  Task-generated standalone build artifacts are archived outside source/Git.
- Production npm dependency audit: zero vulnerabilities. NuGet advisory review
  reports existing runtime/transitive/test advisories; it is not a clean audit.
- Current member/customer component lifecycle assertions: 7/7 passed, zero skipped,
  including the final lock abstraction and generic plan repository correction.
- Full web suite before the final deletion/Host-plan corrections: 95 passed,
  73 intentional opt-in skips, zero failed. This carried-forward result is not the
  final PostgreSQL or final commit CI evidence.
- Full application suite: 1,364 passed, zero failed/skipped on the earlier build
  before the final Host-plan/deletion corrections. This is carried-forward broad
  evidence; current component and dedicated PostgreSQL execution cover the final
  backend changes. CI must rebuild and run the complete inventory at the PR head.
- Final synthetic PostgreSQL HTTP suite: 14 passed, zero failed/skipped, including
  assigned Area, same-Tenant other Area, cross-Tenant, granular permissions,
  authorized Host reads/options/tier/profile/suspension, wrong-Tenant plan denial,
  deletion exclusion, aliases, concurrent authority changes and durable rollback.
  The owned disposable database/container was removed after execution.
- All evidence remains isolated; no real member record was read or mutated.

## Investigated validation failures

- Initial PostgreSQL fixtures lacked a linked operator Customer. Corrected fixture
  prerequisites, explicit persisted Host TenantId, JWT and real session assertions;
  no authorization assertion was relaxed. DELETE tests use the API query binding.
- Host positives exposed the custom membership repository's explicit Host/global-
  only filter in captured SQL. It cannot be removed by IgnoreQueryFilters. The
  member service and internal plan validator now reuse generic repository queries
  with explicit global-or-resource-Tenant checks. Temporary SQL probes were removed.
  Wrong-Tenant plan rejection asserts the existing ABP UserFriendly HTTP 500
  contract, exact validation details and no writes, separately from forced runtime
  rollback evidence.
- Agent 2 identified EF IgnoreQueryFilters composition suppressing all global
  filters, including deletion filters. Added explicit deletion predicates and the
  fourteenth regression for deleted Customers and linked Users.
- A frontend rerun discovered dependency test files inside generated standalone
  output. Cancelled that contaminated attempt, archived task-owned build output,
  then ran the clean full source suite successfully, matching CI test-before-build.
- The first broad local backend attempt used the default Production environment,
  which intentionally suppresses seed data. An unchanged test reproduced the
  same missing-default-Tenant startup failure on unchanged PR #103. Cancelled that
  attempt and reran with `ASPNETCORE_ENVIRONMENT=Development`, matching CI. Updated
  local validation instructions; no startup/product seeding change was made.
- TypeScript initially rejected a missing required TextField name; corrected it
  and reran the affected checks successfully.

## Independent review

Agent 2 final independent verdict: PASS, no remaining material branch-owned
findings. It inspected the complete diff and new artifacts, parsed the fresh
fourteen-case PostgreSQL TRX and seven-case component TRX, and checked frontend
results/build completion. It classified broad pre-final backend evidence as carried
forward. The reviewer made no edits or production actions.

Review findings led to preserving all previous search fields, closing the
concurrent customer-promotion race, preserving deletion exclusion, correcting
Host plan access and preventing stale post-mutation page reloads.

## Current state and handoff

IMPLEMENTED and TESTED in isolated fixtures. Independent correctness review PASS.
Publish as a separate focused draft PR to main. Required PR CI remains a separate
head-specific check; do not infer MERGE READY from local or carried-forward tests.
The smallest next step is reviewer assessment of this PR and its complete CI,
followed by an explicitly authorized merge/deployment process. None is performed
by this task. No production business-operation commissioning is established.

## Git/branch context

- Branch: `feature/member-area-authorization`.
- Base: `1251fb82979e18be53d1d0e7f5c6fab2d0c50a58` (`origin/main`).
- Independent worktree; no files from the owner's other worktrees are included.
- Publication target: a separate draft PR from this branch to `main`; PR #103 is
  unchanged and remains the independent qualification package.
