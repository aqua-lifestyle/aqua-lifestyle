# Member Area security review remediation

> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

## Goal and authority

Owner requests correction of two independent findings on PR #104, commit
`cc2709cb3fd141c0d62d230e3be21446c98b9f08`: customer import bypass and restoration
source-authority race. Continue on `feature/member-area-authorization`. No
production access, merge, deployment, financial/provider operations or commissioning.

## Initial state

Clean task worktree at reviewed commit. PR #104 is open. Its previous CI
checks all passed; independent security verdict is FAIL. Prior Agent 2 PASS is
superseded for acceptance by the owner's independent findings. Passing tests alone
did not establish complete authorization-path coverage.

## Plan

1. Reproduce import denial and restore-with-demotion lock behavior with executable
   isolated PostgreSQL/API regressions before changing product code.
2. Reuse persisted Tenant/Area authority and locks inside each import row's owned
   transaction. Audit every shared account-manager caller.
3. Stabilize removed-member source Area/grant before classification changes, with
   explicit removed-record handling and both revocation orderings/rollback tests.
4. Run focused PostgreSQL/component checks and final full CI inventory, inspect
   all skips and obtain fresh independent Agent 2 review at the final commit.
5. Update PR #104 and report actual evidence, remaining risks and final verdict.

## Root causes and caller audit

REPOSITORY EVIDENCE: import calls account manager without the service's member
record authority check. Restore checks source scope but locks only Customer;
post-write authority is conditional on requested resulting MembershipId.

The shared account manager has exactly three administrative call sites:
AdminCustomer Create, AdminCustomer Restore and CustomerImport ImportRow. The
existing profile updater's administrative callers are the account manager restore,
AdminCustomer Update and AdminMember mutations. Self-service registration and
MyAccount are distinct ownership-authorized paths and are not changed.

## Reproduction

EXPERIMENT RESULT: both new regressions failed before any product correction on
the reviewed code. Real CSV preview/import without the target Area grant returned
`ImportedRows=1`, contrary to the required zero. Restore-with-demotion paused in
the real profile-updater path allowed an Area update instead of PostgreSQL lock
timeout `55P03`. The owned disposable PostgreSQL container was removed afterward.

An earlier import setup had multiple active Areas and therefore failed at the Area
resolver before reaching the bypass. That result was inconclusive for import.
The discriminating fixture leaves exactly one active target Area and asserts the
specific sanitized authorization error after correction; assertions were not weakened.

## Correction

- Extracted the existing member-record guard and assignment predicate into a plain
  shared helper, retaining the original portal/service scope wrappers. No new role,
  permission, table or endpoint is introduced.
- The shared account manager requires a caller-owned READ COMMITTED transaction
  before writes and invokes the guard for new Customer state, removed candidates,
  original restoration state and restored target state. No external notification
  or setup link occurs before authority succeeds. Unauthorized identity/Customer
  writes are flushed only inside that transaction and roll back together.
- Each imported row owns an explicit RequiresNew READ COMMITTED transaction.
  Generic membership queries retain explicit target-Tenant checks for authorized
  Host imports instead of inheriting the custom repository's Host/global-only filter.
- The authorized Host import regression exposed ABP's parameterless permission
  lookup using the current target-Tenant UOW realm. The shared guard uses the
  explicit actor `UserIdentifier` overload so persisted Host `AllTenants` authority
  is evaluated in the actor's realm, without trusting token permission claims.
- Strict removed-only stabilization shares Customer→Area→grant lock mechanics;
  the live operation still rejects deleted Customers. Source Area/grant locks
  precede restoration changes and remain held through commit, including demotion.
- Added ten PostgreSQL/API regressions, bringing the dedicated suite to 24. They
  cover import permissions, Host/Area denial, revocation, row isolation and rollback,
  both restoration transaction orderings, flushed-write rollback and strict
  removed/live lock semantics.
- Per owner instruction, removed the Python block originally added by PR #104.
  CI uses its existing PowerShell/XML shell pattern; local runner uses Bash.
  No Python code is included in this PR's diff.

## Validation and next action

Current Release backend build passed with analyzer/XML/advisory warnings; package
versions are unchanged. Final focused PostgreSQL/API execution: **24 passed,
zero failed/skipped**. Relevant member/customer component and permission-attribute
execution: **30 passed, zero failed/skipped**. The disposable PostgreSQL container
was removed after execution. No production record was accessed.

Agent 2 independently confirmed the failing regressions and reviewed the complete
working diff, including Tenant/Host privilege boundaries, original restoration
classification and transaction order. Its preliminary source review has no
remaining material finding. This is not the final-commit verdict.

IMPLEMENTED and TESTED in isolated fixtures. Acceptance additionally requires the
full final-head CI inventory, inspection of skipped cases and a fresh independent
Agent 2 review of the final commit. Record those publication results in
[PR #104](https://github.com/aqua-lifestyle/aqua-lifestyle/pull/104), whose head and
checks identify the exact reviewed commit; do not infer acceptance from local tests
or the previous green CI run. No merge, deployment or commissioning is authorized.
