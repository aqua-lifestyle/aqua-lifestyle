> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

# Original B6 PostgreSQL 18 compatibility — version 1

Owner authorization: **“AGENT 2 — UNBLOCK ORIGINAL B6 FOR POSTGRESQL 18.”** Only
non-production correction and focused synthetic validation; no production access,
data extraction, privileged bootstrap, migration, financial change, V2 activation,
push or merge. PB1/C04 is parked.

Branch `fix/b6-pg18-schema-compat`, separate worktree
`aqua-lifestyle-b6-pg18-compat`, from original published B6
`438852eca9f91ef973802156ebbd31c3fa3940ed`. Other worktrees are preserved.
One bounded existing-release check found no qualified compatible release.

## Actual schema evidence

The database custodian supplied schema-only results in this task: server number
**180004**, **54** ordered migration rows, all ProductVersion **8.0.8**, ending at
`20261005194901_AddIsolatedAQGreenPilotEvidence`; **241** column rows across the
17 extraction tables, including nullable CommissionDecisionRulesVersion and the
additional IsTestMoney column. No credentials or application rows were supplied.
This is owner/custodian readback, not an agent production connection. The last
migration already exists in the selected EF source. No migration semantics or
production schema changes are required by the observed mapping.

## Bounded implementation

Distinct package V4 / runtime V3 selects PG18.4 and the evidenced existing schema;
old V3 bytes/receipts stay bound to their historical Git release. Before application
rows or raw output, the same read-only transaction checks the exact migration
inventory and required source column types/nullability against the executing EF
model. Zero-row SELECTs stabilize required relations against DDL. Search-path lookup
is pinned. Sanitization/BASE verification enforce the selected full history rather
than accepting a substituted final head. The existing owned runtime uses digest-
pinned PostgreSQL18.4 for SOURCE and ANALYSIS; no provider/version downgrade.

Operational qualification remains separate: local SDK8.0.132/runtime8.0.32 is
development evidence, not the frozen operational SDK8.0.130/runtime8.0.30. Existing
Gate-Q/A, dependency, custody, independent review and source authorization are not
bypassed or self-issued.

## Component checkpoint and source freeze

Locked restore and Release build passed. Initial pure profile execution identified
the intentionally changed V4 allowlist digest; the pin was recomputed by the
existing canonical .NET encoder (only the package version field changed). An
initial integration attempt correctly rejected an unprotected /tmp parent; the
test now creates its own0700 root, without weakening SecureFiles.

Current focused results at
`/tmp/opencode/b6-pg18-focused-20261021-c/pg18-compat.trx`: **11 passed / 0 failed /
0 skipped**. Ten are the new profile/provider checks; one is the unchanged existing
lock/bootstrap contract test (including its existing215-case unprivileged synthetic
native fixture, not privileged bootstrap qualification). Actual PG number180004,
54 migrations and241 catalog columns are asserted. The real restricted reader
round-trip, two deterministic sanitizations/BASEs, prohibited-output sentinel
checks, source table fingerprint equality, missing-history/type/nullability/column
denials before raw output, and V3/incorrect-version/head/digest rejection passed.
Existing three Core build warnings remain; the new test adds no suppressed warning.

Freeze the source in a normal local commit before the existing artifact suite:
its operational-provenance negative deliberately requires a genuinely clean Git
source, not a forged clean-status response. The next existing, non-privileged
synthetic package invocation is:

```text
B6_TEST_RESULTS_DIR=/tmp/opencode/b6-pg18-package-20261021-a B6_TEST_FILTER='FullyQualifiedName~B6ArtifactContractTests|FullyQualifiedName~B6Pg18CompatibilityTests' bash docs/operations/aqgreen-v2-b6/snapshot-package/test-package.sh
```

That command retains its TRX outside Git and exercises two complete existing
inventory/analysis/impact/final verification runs. It does not authorize real-data
handoff or constitute independent acceptance. Actual output, not this planned
invocation, must establish the final result. Future operational release still needs
new-source Gate-Q/A/build/custody/independent acceptance and specific data authority.
