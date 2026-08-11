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
| C08 | `StartedAt < TermsEffectiveFrom` |
| C09 | `JoiningPaymentId` mixed with registration/activation refs |
| C10 | `RegistrationPaymentId = ActivationPaymentId` |
| C11 | Status 2/3/4 with no qualifying single and no qualifying pair payment |
| C12 | confirmed payment at `JoiningPaymentId` but not qualifying |
| C13 | confirmed payment at `RegistrationPaymentId` but not qualifying |
| C14 | confirmed payment at `ActivationPaymentId` but not qualifying |
| C15 | confirmed registration+activation pair but pair not qualifying |

Note: C15 never fires alone — it always co-fires with C10 or C13/C14.

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
- **Do not weaken the migration guard.** Do not modify
  `20260809043240_AddAQGreenFuneralCoverEntitlements` or any migration that
  contains the P0001 predicate.

## What results must be returned before remediation is designed

1. The full exported output of `inventory.sql` (anonymised if shared).
2. Counts: total scanned, healthy (`TriggerConditions = {}`), and triggered.
3. Per-triggered-row: the exact `TriggerConditions`, class (one of the four
   above), and the evidence used for that class.
4. Any row that cannot be classified from persisted evidence must be marked
   `REQUIRES MANUAL CLASSIFICATION` — it is not "legacy" without evidence.

## Validation evidence

The inventory was validated against PostgreSQL 16 on scratch data:

- 107/107 checks passed;
- the healthy baseline did not raise the migration guard;
- every C01–C15 class caused the real migration guard to raise with the exact
  P0001 message;
- aggregate: 21 seeded rows / 17 flagged matched;
- per-row `TriggerConditions` matched hand-computed expectations.

This proves the query reproduces the guard predicate. It does **not** prove
anything about actual production rows; production must be inventoried with
this tool before any conclusion is drawn.

## Repository provenance

- `inventory.sql` is byte-identical (sha256
  `cc050a0011039781a2f5ab1fba79e675d2a38fb9730057c8a978b5c153ba8960`) to the
  query that passed the 107/107 validation checks, preserved from
  `/tmp/opencode/inventory/inventory.sql`.
- Scratch validation artifacts (`guard.sql`, `schema.sql`, `seed_all.sql`,
  `validate.sh`) were consumed during Phase 2 and are not committed.
