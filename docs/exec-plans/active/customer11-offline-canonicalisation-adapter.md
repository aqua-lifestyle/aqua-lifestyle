# Customer-11 offline canonicalisation adapter — Stage 3B

> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

## Goal

Implement and validate a file-only transport into the existing domain canonicaliser,
then execute two identical offline invocations. Preserve UNKNOWN evidence. No writer,
database, Render, cutover, commit or push operation is authorised.

## Authoritative references

Owner chat instruction: OWNER AUTHORIZATION — AQGREEN CUSTOMER-11 STAGE 3B:
OFFLINE CANONICALISATION ADAPTER BUILD + VALIDATION. The preceding Stage 3A
inspection specifies the domain/interface distinction. The owner's bounded D03B/D09
component and effective-time decisions remain governing; no policy is reopened.

## Confirmed decisions

Only the existing six-person Tenant 1 / AQGreen component is admitted. The new input
hash is 74a8e8bedd14d151c119173e44671c7ec8fd8c03ebb75cbd0763974c6dd3e416;
the capture hash is b84119e66c886ac0086b9f531186d7345a007359df465ac945afe033787e2494.
Canonicalisation effective time remains 2026-10-02T05:37:10Z. Input/output acceptance,
writer rebinding and persisted storage-ID acceptance are later owner gates.

## Assumptions

No uninterrupted custody of the historical artifacts is asserted. This is new-lineage
candidate evidence. Historical UNKNOWN facts remain UNKNOWN.

## Current state

Implementation/validation in progress. Base branch and tree verified clean before edits.

## Evidence

Read-only source and input inspection completed. New execution evidence will be recorded
in the protected lineage workspace, outside source control. No production data is copied
into fixture source.

## Open questions

Owner acceptance of the resulting source and candidate output is pending.

## Completed work

Frozen base and exact manifest/capture hashes verified.

## Next action

Complete strict mapping, fixture validation, two runs and independent reconciliation.

## Git/branch context

Repository: aqua-lifestyle-b6. Branch: b6/nonprivileged-attempt-account-foundation.
Base HEAD: 417bbcc38b68ecf9fd0b1f83eb52bf5d72d47abd.
Base tree: ddf47091c55936476dcc40935e49ff76ad057dc9.
Only two new tool directories and the two owner-authorised documents may change.
No commit/push/PR authorised.

## Stage-3B result

IMPLEMENTED and TESTED locally; owner acceptance remains pending. Initial restore,
locked restore and Release build succeeded. Build reported three warnings in unchanged
Core files (two CS0628 and one CA2255), zero errors. Fixture validation: 63 passed,
0 failed, 0 skipped. SDK 8.0.131, .NET runtime 8.0.31, Ubuntu 24.04 / Linux.

Two offline invocations used identical arguments and executable bytes. Output files
are both 24,489 bytes, SHA-256
`a1bcf510b41fa97e449e0dcb938d2b492ad08f215f6d4e5d75ff117f8b6af11d`.
Independent native reconciliation: 25/25 checks passed; participant digests 6/6 passed;
domain digest recomputation passed. The initial independent jq checker had a syntax
error, corrected before its successful execution; no adapter rerun was needed.

Domain topology matches the historical accepted expectation. All six V1 levels remain
UNKNOWN/null and all impact comparisons are Ambiguous. The simulated V2 level is a
separate domain output, not invented historical V1 or financial evidence. Scope is
unchanged; six prospective writer-derived placement IDs differ because input hash
changes. None are accepted for persistence. Writer pins and all existing tracked
source remain unchanged. No database, Render or writer operation occurred.

Protected evidence directory: the new-lineage Stage-3B directory. It contains complete
source diff/inventory, build/test logs and TRX, artifact closure hashes, two output
files, native reconciliation, storage-ID consequences and the source-to-artifact
receipt. No production capture is included in repository fixtures or documentation.

Next action: owner review/acceptance of the exact source snapshot and new candidate
output. Commit and writer-rebinding authority remain separate and ungranted.
