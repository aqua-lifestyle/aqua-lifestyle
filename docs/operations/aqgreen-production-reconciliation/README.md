# AQGreen production reconciliation inventory (read-only)

## Purpose

`inventory.sql` is a single read-only `SELECT` that reproduces, on PostgreSQL,
the P0001 guard predicate of migration
`20260809043240_AddAQGreenFuneralCoverEntitlements` (15 OR branches, C01–C15).

Its only job is to enumerate, per scanned `EntryParticipations` row, exactly
which guard condition(s) would trigger, plus the diagnostic evidence needed to
classify those rows. It must be executed **before any remediation is designed**
so that the deployment blocker (P0001) can be quantified from production data.

## READ ONLY

`inventory.sql` performs no DML, no DDL, no `VACUUM`, no locks beyond an MVCC
snapshot, and no writes of any kind. It reads `EntryParticipations`,
`Customers`, `MemberPayments`, `AQGreenMigrationBackup`, and
`EntryParticipationApprovalDecisions`. It does not reference
`AQGreenFuneralCoverEntitlements` (that table cannot exist in production — the
migration aborted before its `CreateTable`).

## Production prerequisite

- A read-capable production connection (read replica or read-only role preferred).
- Confirmation that `EntryParticipations` uses the column names used here
  (`JoiningPaymentAmount`, `JoiningInstallmentAmount`, `TermsVersion`,
  `TermsEffectiveFrom`, `JoiningPaymentId`, `RegistrationPaymentId`,
  `ActivationPaymentId`).
- No other tooling changes to the database.

## Expected output

One row per **scanned** participation: `JoiningPaymentAmount > 0.00` and
`IsDeleted = FALSE`. Each row carries:

- identity and status (`Status`/`StatusLabel`, `StartedAt`, `ActivatedAt`);
- all scan inputs (amount, instalment, currency, terms, payment refs);
- customer diagnostics (`HasLiveCustomer`, `CustomerExistsAnyTenant`,
  `CustomerActualTenantId`);
- qualifying-evidence flags and confirmed-payment aggregates;
- `C01..C15` boolean columns and a `TriggerConditions` array;
- `LinkedPaymentFacts` / `CustomerConfirmedJoiningPayments` JSONB evidence;
- `ApprovalDecisionEvidence` and legacy-boundary columns
  (`WasMigratedFromLegacy`, `LegacyOldTermsVersion`, `LegacyOldTermsEffectiveFrom`).

## C01–C15 meaning

| Condition | Meaning (any true = P0001 trigger) |
|---|---|
| C01 | No live customer (Id + TenantId match, not deleted) |
| C02 | `JoiningPaymentAmount <> 1200.00` |
| C03 | `Currency <> 'ZAR'` |
| C04 | `TermsEffectiveFrom < 2026-07-26` |
| C05 | `TermsVersion` not in the 3 modern versions |
| C06 | single-1200 terms with instalment `<> 0.00` |
| C07 | flexible terms with instalment `<> 600.00` |
| C08 | `StartedAt < TermsEffectiveFrom` and **no recognised legacy provenance** (see below) |
| C09 | `JoiningPaymentId` mixed with registration/activation refs |
| C10 | `RegistrationPaymentId = ActivationPaymentId` |
| C11 | Status 2/3/4 with no qualifying single and no qualifying pair payment |
| C12 | confirmed payment at `JoiningPaymentId` but not qualifying |
| C13 | confirmed payment at `RegistrationPaymentId` but not qualifying |
| C14 | confirmed payment at `ActivationPaymentId` but not qualifying |
| C15 | confirmed registration+activation pair but pair not qualifying |

Note: C15 never fires alone — it always co-fires with C10 or C13/C14.

### C08 legacy-recognition exception

Migration `20260726162000_AddAQGreenSingleJoiningPayment` rewrote pre-existing
legacy participations to modern terms (`2026-07-single-1200`, effective
2026-07-26) while persisting their original old-terms chronology in
`AQGreenMigrationBackup`. Such rows legitimately have
`StartedAt < TermsEffectiveFrom` because the start predates the rewrite.

C08 therefore fires **only** when the row has no coherent legacy provenance:

```sql
StartedAt < TermsEffectiveFrom
AND NOT EXISTS (
    SELECT 1 FROM "AQGreenMigrationBackup" legacy_backup
    WHERE legacy_backup."ParticipationId" = participation."Id"
      AND legacy_backup."OldTermsEffectiveFrom" IS NOT NULL
      AND participation."StartedAt" >= legacy_backup."OldTermsEffectiveFrom"
)
```

A backup row is recognised only when its old-terms effective date is present
and is **no later than** `StartedAt`. Rows without a backup row, with a `NULL`
`OldTermsEffectiveFrom`, or whose `StartedAt` predates their own old terms
remain C08 contradictions and fail closed.

## Classification of results

### Healthy scanned rows

Rows scanned because `JoiningPaymentAmount > 0` but with
`TriggerConditions = {}` do **NOT** require reconciliation. An ordinary
Status 0/1 participant awaiting legitimate payment belongs here.

### P0001-triggering rows

Only rows with one or more C01–C15 conditions are affected by the deployment
blocker. Classify triggered rows **only** as one of:

- `LEGACY BOUNDARY CANDIDATE`
- `WRONG PAYMENT FACTS`
- `TRUE CONTRADICTION`
- `REQUIRES MANUAL CLASSIFICATION`

Do **not** automatically classify a row as legitimate legacy merely because
`StartedAt < 2026-07-26`. Require persisted supporting evidence such as
`AQGreenMigrationBackup` (`WasMigratedFromLegacy = TRUE`) or equivalent
authoritative history.

## How to export results securely

```bash
psql "$READONLY_DATABASE_URL" -f inventory.sql -o aqgreen-inventory.csv
```

- Export with `-o` (or `\copy ... TO STDOUT`) so the result never lands in a
  public log.
- Transfer the file out-of-band (e.g., encrypted artifact store); do not paste
  results into chat or issue comments.
- The query already excludes customer PII (`Name`, `Email`,
  `ClubMemberNumber`). Keep it that way when summarizing.

## Prohibitions

- **No DML.** Do not insert, update, or delete production rows while running
  or interpreting this inventory.
- **No DDL.** Do not create, alter, or drop tables, indexes, or constraints.
- **No fabricated payments, approvals, or timestamps.** Do not invent payment
  history or rewrite `ConfirmedAt`/`StartedAt`/`TermsEffectiveFrom` to make
  rows pass the guard.
- **Do not change `__EFMigrationsHistory`.** Do not delete, edit, or add
  migration rows to bypass P0001.
- **Do not weaken the migration guard further.** The C08 legacy-recognition
  exception in `20260809043240_AddAQGreenFuneralCoverEntitlements` is the only
  approved narrowing, and only for rows with a coherent `AQGreenMigrationBackup`
  provenance. Do not extend it to any other condition or to rows lacking that
  persisted evidence.

## What results must be returned before remediation is designed

1. The full exported output of `inventory.sql` (anonymised if shared).
2. Counts: total scanned, healthy (`TriggerConditions = {}`), and triggered.
3. Per-triggered-row: the exact `TriggerConditions`, class (one of the four
   above), and the evidence used for that class.
4. Any row that cannot be classified from persisted evidence must be marked
   `REQUIRES MANUAL CLASSIFICATION` — it is not "legacy" without evidence.

## Validation evidence

The inventory was validated against PostgreSQL 16 on scratch data:

- 131/131 checks passed (previous 107/107 baseline plus the four legacy
  recognition scenarios);
- the healthy baseline did not raise the migration guard;
- every C01–C15 class caused the real migration guard to raise with the exact
  P0001 message;
- the two recognised-legacy scenarios (L1 unpaid awaiting joining, L2 active
  with a qualifying joining payment — both with a coherent
  `AQGreenMigrationBackup` row) did **not** raise;
- the two fail-closed legacy scenarios (L3 backup with `NULL`
  `OldTermsEffectiveFrom`, L4 `StartedAt` predating the old-terms date) did
  raise with the exact P0001 message;
- per-row `TriggerConditions` matched hand-computed expectations.

This proves the query reproduces the guard predicate. It does **not** prove
anything about actual production rows; production must be inventoried with
this tool before any conclusion is drawn.

## Repository provenance

- `inventory.sql` is byte-identical (sha256
  `9986a76abbc0b40157f30c8ce86b54d5f691e5a4b0681322a10cd99ce0b41a06`) to the
  query that passed the 131/131 validation checks, preserved from
  `/tmp/opencode/inventory/inventory.sql`.
- The scratch validation artifacts (`guard.sql`, `schema.sql`, `seed_all.sql`,
  `validate.sh`, `production_shape.sql`) were consumed during Phase 2 and are
  not committed.

## Production deployment result

### Simulation verified

On 2026-08-11, the corrected P0001 guard was executed against the current
production PostgreSQL 18.4 population inside an explicit read-only transaction.
The four in-scope participations comprised two healthy modern rows and two
backup-proven legacy rows. All C01-C15 counts were zero, and the guard completed
without raising P0001. This was a simulation only; it did not execute migration
DDL or DML.

### Production deployment verified

PR #73 merged the remediation as commit `925aabd` (merge commit `e818810`).
Render's normal pre-deploy migrator applied
`20260809043240_AddAQGreenFuneralCoverEntitlements` without raising P0001 and
continued through `20260809201814_AddCommissionTermsVersions`. The subsequent
current-main deployment at commit `e6d04e6` also applied
`20260811150251_SeparateAreaFromTenantBoundary`.

Before deployment verification, a PostgreSQL 18 custom-format logical backup
completed at `2026-08-11T17:40:20Z`. PostgreSQL 18.4 `pg_restore` accepted the
archive and restored it successfully into an isolated PostgreSQL 18.4 instance.
The restored database contained the expected 46 migration records, five AQGreen
participations, two legacy backup records, and payment data. The archive was
stored outside the repository with owner-only filesystem permissions; no
credentials or production export are committed. Its SHA-256 is
`8a7c495663e1b68c5175a6821046981d735d95c8e17fc183395e00d4705483b7`.

The first API update attempts were rejected by the fail-closed production
configuration validator because the configured Yoco mode and secret-key class
did not match. No compatibility or validation bypass was introduced. After the
owner aligned the existing non-secret mode setting with the configured key
class, the final Render deployment `dep-d9tm52qjnfac73c89vu0` completed at
`2026-08-11T17:58:28Z` through the normal build, pre-deploy migrator, and API
startup path.

Post-deployment verification recorded:

- deployed API build: `e6d04e67ac04d5f6ab128c25d7b84dcdb6206840`;
- production migration head:
  `20260811150251_SeparateAreaFromTenantBoundary`;
- P0001 migration history row and `AQGreenFuneralCoverEntitlements` schema:
  present;
- funeral-cover backfill: three live entitlements for three qualifying
  participations, with none missing;
- legacy integrity: both authoritative backup rows remain coherent, and the
  joining/payment history digests match the pre-deployment backup;
- API and database health: healthy;
- payment contract:
  `aqua-payments-2026-08-09-flexible-payment-approval`;
- required frontend capabilities: present (`aqgreen-flexible-joining-v1`,
  `programme-approval-queue-v1`, `direct-onyx-checkout-v1`);
- production frontend compatibility guard: satisfied without a bypass;
- Area baseline: Johannesburg (`JHB`) present, all 10 Default-tenant customers
  mapped, all five AQGreen participations Area-resolvable, and three active
  Area Admin assignments present;
- customer topology: 13 live `Customers` rows existed in total; 10 belonged to
  the Default tenant and were Area-mapped, while three were host-scoped
  (`TenantId IS NULL`) Customer entities outside the tenant Area baseline;
- weekly commission and monthly-obligation worker settings: unset, retaining
  their application defaults of disabled.

No manual database mutation, migration-history edit, reconciliation, approval
creation, payment-history change, or historical member change was performed.
