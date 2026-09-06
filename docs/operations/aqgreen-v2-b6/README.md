# AQGreen V2 B6 read-only inventory

This directory contains analysis-only tooling. It does not itself establish
business authority and does not authorise production access, D10, a migration, a
cutover, or any production write.

The owner-confirmed D03B/D09 rules and the separate production-access gate are
described in
[`representative-snapshot-specification.md`](representative-snapshot-specification.md).
Confirmation of those rules does not authorise production access or migration.

Run `legacy-inventory.sql` only through the .NET runner against its verified,
package-owned disposable analysis database. The supported entry point is
`snapshot-package/run-analysis.sh`; credentials remain inside owner-only runtime
state and are not copied into shell commands.

Do not put a real connection string in shell history, repository files, reports, or
prompts. The script starts a repeatable-read, read-only transaction and ends with
`ROLLBACK`. It emits counts, stable fingerprints, and snapshot-key hashes. It
intentionally omits direct identifiers, payment references, correction reasons,
and administrator identities. Pseudonymous keys remain linkable within the bounded
dataset and therefore require controlled-data handling.

The C#/.NET analysis package first computes the three D09 populations and D03B components
from the verified snapshot. It constructs sponsor/root ancestry before applying
lifecycle exceptions, so an exceptional ancestor quarantines the complete affected
component without skipping or re-parenting descendants. It separately types
`SponsorEvidenceStatus`, `RootEvidenceStatus`, and
`LegacyUnknownAcquisition` provenance.

The shared C# `AQGreenLegacyCanonicalisationSimulator` accepts only a frozen,
already-classified scope. It requires exactly one explicit `AcceptedRoot`; a null
recruiter is only `RootCandidate`. `AcceptedRoot` is supplied through the reviewed
root-disposition contract and never inferred from independent start, recruiter
clearing, or unknown acquisition. A report with any anomaly produces no proposed
topology. Its manifest digest covers the accepted scope, effective time, versions,
ordering, sponsor/root evidence, parent, slot, path, position, sequence, and
source-evidence digests. These analysis-only values do not add a production
enum/schema value.

The operator-ready package, exact allowlist, schemas, disposable PostgreSQL test,
and runbooks are in [`snapshot-package/`](snapshot-package/README.md).

The repository has no separately persisted Onyx loan-eligibility decision. The
inventory therefore reports the current V1 structural-level distribution from
which eligibility was evaluated, all persisted loan agreements, and the
`AwaitingMemberAcceptance` agreement state separately as persisted loan offers. It
does not recreate a historical eligibility result from mutable current state.
