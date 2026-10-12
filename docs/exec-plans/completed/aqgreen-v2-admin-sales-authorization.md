# AQGreen V2 administrator sales authorization

## Authority and scope

Owner decision, 12 October 2026: authenticated administrators may review AQGreen
weekly sales within existing authorization. Tenant administrators act in their
authorized Areas; central access requires a separate broader grant. This
supersedes the prior host-only reviewer restriction. It grants no commission
calculation, amount override, release, payout or production enablement.

Baseline: `feature/admin-ui-remediation`, HEAD `e9bd8e9`, with the prior local
MVP work and Agent 2 documents preserved. No staged changes. Apply
`verify-stateful-change`, `verify-workflow` and `validate-evidence` proportionately.

## Execution

1. Reuse the review permission, active Area assignments, Tenant filters and
   existing host `AllTenants` grant across list/detail/latest/begin/confirm/reject.
2. Preserve transactional review locking, exact retries, reviewer/time,
   immutable evidence, rules and financial holds.
3. Exercise authenticated direct HTTP and PostgreSQL tests: Area/central
   success, scope/permission denial, duplicates and prior host compatibility.
4. Update current acceptance/operations records and inspect the final diff.

## Evidence

Inspection: review permission is currently Host-only; service rejects Tenant
sessions; Admin/SystemAdmin seed grants already include all eligible permissions.
Other administrative services scope Tenant callers through active same-Tenant
AreaAdminAssignments. The implementation now uses that same scope across
list/detail/latest/hold/confirm/reject, with authority row locks on Tenant
writes and exact retries. Central review still needs explicit AllTenants.

Final current-source execution: 14/14 JWT/direct HTTP PostgreSQL tests passed
inside `bwrap --unshare-net` via a task-owned PostgreSQL Unix socket. Eight
existing sales PostgreSQL tests, 11 application safety/read tests and 50
permission tests passed (combined selection 69/69). Nineteen relevant admin UI
component tests passed without frontend source edits. Release build and diff
check passed. Advisory review completed with pre-existing dependency findings;
no dependency change or clean-audit claim. Initial failures were corrected
fixture/route/error-contract assumptions, not permitted fallback behavior.
Joining/spillover/browser tests were carried forward as instructed.

Acceptance: IMPLEMENTED and TESTED for isolated admin sales authorization;
the prior host-only blocker is closed. No staging/publication/production,
real provider, payment or payout operation was performed.

Solution restore was up to date and the final solution Release build succeeded
with one pre-existing test dependency warning. The final 14/14 HTTP rerun
included creation of a new Held review with authenticated creator/time.
Task-owned PostgreSQL, internal network, Unix socket directory and temporary
secrets were removed. Final scoped report and Agent 2 playbook were updated.

Agent 2's subsequent independent checklist was read. Its authorization evidence
gap is closed by this execution. The existing earnings selector's Tenant-as-Area
wording was verified and reported as a separate remaining walkthrough defect;
no broader UI workflow was reopened. The earlier R400 unreleased calculation
was distinguished from the separately tested R150 positive financial hold.

## Final administrator review alignment

Owner accepted the preceding local engineering evidence and requested only
Tenant labels and administrator role mapping checks. Weekly earnings now uses
Tenant for its selector/placeholder/validation/column and still submits exactly
programme + tenantId. Other shared-selector defaults are preserved. The stored
browser selector was aligned without rerunning joining/spillover/browser work.

Passed: 7 weekly earnings component tests; 9 SQLite persisted role-mapping
tests; TypeScript, focused ESLint and final diff checks. Tenant Admin/SystemAdmin
receive review but no Host-only central/financial grants; Host Admin retains
explicit grants; other business roles are not promoted into administrator
authority. Explicit denial/reseed behavior is preserved. No mapping discrepancy
was found. The backend Area/Tenant authorization implementation was unchanged;
accepted PostgreSQL evidence is carried forward.

Verdict: READY FOR OWNER WALKTHROUGH in isolated infrastructure. No broad test
campaign, production access, deployment or Git publication occurred.
