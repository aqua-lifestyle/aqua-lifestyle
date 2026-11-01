# Current scope note

## Retirement-blocker correction — 30 October 2026

Current owner direction accepts the existing V3 correction **for qualification
only**, not production execution. Timing-policy and test-data-classification
decisions need no repetition. Exact local bytes are identified by the existing
candidate-files.sha256 plus base fc955e8; independent acceptance/source admission
is still separate. The11:14 connection-only check documented in validation.md
established NAME_RESOLUTION_FAILED at that observation. The later existing-route
comparison supersedes the DNS-only blocker: the same external hostname now resolves,
but connections close before a verified session and the currently observed HTTPS
egress is outside the existing database allowlist. Reuse the existing approved
operator egress; no new access/security change is authorized by that comparison.
No V3 production assessment is authorized or claimed by those diagnostics.

**PROPOSED source amendment; local TESTED evidence is not independent acceptance.**
The accepted prospective reset/D10 path was not propagated to the historical
status screen. At `2026-10-30T11:00:00Z`, Johannesburg time is Friday **13:00**,
not Friday midnight. The old scope predicate necessarily fails its time-of-day
conjunct. Future boundary, no post-boundary participation and unused reset identity
are independent checks; the aggregate FAIL does not prove they passed.

The other mismatch is admission selection: the historical screen unconditionally
rejects `payments>0`, whereas the accepted retirement contract explicitly admits
retained payment evidence under `OWNER_ATTESTED_TEST_ONLY_V1`. That predicate
measures presence, not provider test/live mode, settled value or liability.
The last successful detailed observation (29 October 21:48 UTC, operational
comment 6509172606) passed all nine non-payment guards; it is carried-forward
evidence, not a current readback of the 30 October aggregate FAIL. The earlier
27 October diagnostic identified MemberPayments, Yoco receipts, joining checkouts
and participation payment references. No current subpredicate result is invented.

The minimal V3 amendment lets the launcher explicitly select the same two paths
already accepted for reset/D10:

- `--boundary-policy OWNER_AUTHORIZED_PROSPECTIVE_UTC_V1` together with
  `--boundary-authorization-digest` from the actual protected boundary authority;
- `--payment-admission-mode OWNER_ATTESTED_TEST_ONLY_V1` together with
  `--owner-test-only-attestation-digest` from the actual owner declaration.

Each pair is optional but must be complete, recognized and nonzero. Omitted pairs
retain canonical Friday and zero-payment admission. No nine non-payment guard,
population CTE, visibility/schema/source check, timeout or rollback control is
removed. V3 evidence names the chosen policies/authority digests, exact boundary
and reset identity, and `providerModeVerification: NOT_INDEPENDENTLY_VERIFIED`.
Old V2 receipts are rejected. A V3 financial/structural PASS means database
**admissibility under the disclosed selection**, never that payment presence or
external liabilities are zero. Actual conservation still occurs in the separately
authorized reset and requires Aqua's acceptance before migration 55.

Both business checks remain UNKNOWN in SQL by design. Classification is already
owner-attested, but must be bound to a freshly observed source fingerprint in the
protected record. External claims are not independently cleared by that declaration;
their records/obligations remain preserved and processing held. A database-only
`overall: PASS` remains impossible; final business acceptance belongs to the
protected authorization packet, not fabricated SQL output.

This amendment needs exact artifact qualification and independent acceptance before
production use. It does not request another owner classification or timing-policy
decision. Current artifact hashes/results are at the top of `validation.md`.
No replacement retirement window is proposed. The historical commands/windows
below are not current execution authority.

The following is the historical status-only preflight contract. Its accepted
production executions and the payment-only FAIL are retained as evidence. The
current owner-attested retirement branch is defined in
[the release package](README.md) and [owner decision](owner-attested-test-only-decision.md).
It does not weaken this screen or falsely turn preserved payment presence into
verified zero exposure. The launcher changes only its included state.sql hash;
current artifact identities are in [validation.md](validation.md). Prior windows
and approvals do not authorize executing the new reset or migration.

# Status-only clean-start preflight amendment

**PROPOSED — technical review and reauthorization required before production use.**
The owner authorized preparing a smaller preflight. That instruction and the
previous raw-count preflight authorization do not authorize this changed contract.
No production assessment was executed while preparing it. Existing AQGreen/B6,
reset, migration, D10, payout and deployment controls remain intact.

## Executable scope and output

`tools/operations/aqgreen-clean-start-preflight.mjs` runs the reviewed
`preflight.sql` and `preflight-checks.sql`. It verifies all three SQL hashes,
including the unchanged `state.sql` baseline, then executes a frozen in-memory
copy. It creates no result files and never emits provider/client diagnostics.

Every check result is **PASS, FAIL or UNKNOWN**. Counts and source rows are
processed only inside PostgreSQL. The reduced receipt contains status tokens,
known resource/database/Tenant identifiers, UTC observation time, script hashes,
and non-identifying database/schema/source/snapshot fingerprints. It contains no
names, customer identifiers, transaction references, amounts or sensitive counts.
Only this reduced receipt may be retained in the existing owner decision/audit
record or reported through the approved non-identifying conversation channel.
There is no separate raw-results destination requirement for this screen.

A `databaseScreen: PASS` proves only the database predicates in that snapshot.
`testDataClassification` and `externalBusinessObligations` remain **UNKNOWN**;
database zeros cannot certify genuine membership rights, off-system receipts,
Yoco settlements/refunds/disputes, unpaid commissions or other external claims.
Consequently `overall` remains UNKNOWN until separate authoritative business
review and independent acceptance. Known database exposure yields FAIL. Neither
PASS on the database screen nor exit status can authorize a reset or release.
Exit codes are 1 for UNKNOWN and 2 for FAIL; overall PASS/exit 0 is reserved and
cannot be granted by this database-only screen.

## Preserved checks and bounded execution

The canonical population/exposure CTEs are identical to `state.sql`; a regression
check enforces that equality. Scope remains whole Tenant 1 before the canonical
boundary, including deleted/inactive lifecycle exceptions. Existing payment,
checkout, real commission, loan, graduation-funding, monthly-obligation, payout,
orphan, invalid lifecycle, membership and immutable-placement checks remain.
The screen additionally requires the exact reviewed 54-ID migration prefix,
no migration-55 table, no future-boundary participation or reused reset identity,
and complete visibility of the 16 relevant relations. Active row-security filters
make coverage/scope/negative-exposure results UNKNOWN, not valid-looking zeros.

The session starts with default read-only enforcement and executes one
REPEATABLE READ READ ONLY transaction: 30-second statements, 3-second lock waits,
45-second idle timeout, pg_catalog search path and final ROLLBACK. No receipt is
emitted until rollback succeeds. Missing permissions, malformed/stale output,
connection/certificate failure, altered SQL or an expired window produce UNKNOWN.
There is one provider lookup and at most one PostgreSQL invocation, with no retry,
connection fallback or TLS downgrade. A maximum 15-minute explicitly approved UTC
window bounds child timeouts; the SQL client has a maximum 120-second lifetime.
Xolani-M remains the operator and cancellation owner.

## Logical source identity amendment and exact limitation

The launcher uses the existing authenticated Render CLI to retrieve connection
information for `dpg-d9f0jne1a83c73d8m780-a`. It requires that resource ID and one
unambiguous external connection for the exact Frankfurt hostname, port 5432 and
`aqualifestyle`. It ignores URI-supplied TLS options, clears inherited libpq
routing/options, and enforces system-CA `verify-full` plus SNI. Credentials remain
in memory/environment, never command arguments or output. SQL then checks actual
server TLS, database, authenticated session user, absence of role substitution,
and read-only repeatable-read execution. Tenant 1 is fixed in the SQL.

Proposed identity mode: **RENDER_RESOURCE_TLS_DATABASE_SESSION_V1**. Together these
controls establish the logical provider resource/database for this bounded
read-only observation. They are not an assertion of native-fingerprint equality:
they cannot establish physical-cluster continuity across a restore/replacement
under the same logical endpoint. Owner/reviewer acceptance must explicitly cover
this narrower preflight equivalence. Native binding and protected before-images
in the unchanged reset/restore/D10 contracts are not waived by this amendment.

Primary authority: [PostgreSQL TLS verification](https://www.postgresql.org/docs/18/libpq-ssl.html)
and [Render connection information](https://render.com/docs/postgresql-creating-connecting).
The 26 October successful target/TLS evidence is carried forward; no new live
connection success is claimed. A failing actual TLS route remains UNKNOWN.

## Exact reauthorization

Review and approve the new entry SQL, check SQL, launcher hashes and this contract;
`state.sql` remains the unchanged canonical baseline. Exact artifact hashes and
validation are recorded in [validation.md](validation.md). The prior entry SQL
hash `12f2e7cee40682e6bf1d031211842143d0d0a8de5dcf79d5333a06555a1152d6`
and the prior 05:45–06:00 window are not authorization for this changed invocation.
An expired window must be replaced by a new explicit UTC window.

From the repository root, only after review/acceptance and owner reauthorization:

```bash
node tools/operations/aqgreen-clean-start-preflight.mjs \
  --boundary-utc '2026-10-29T22:00:00Z' \
  --reset-id 'AQGREEN-CLEANSTART-20261027' \
  --window-start-utc 'OWNER_APPROVED_UTC_START' \
  --window-end-utc 'OWNER_APPROVED_UTC_END'
```

The boundary and reset identity are proposals for assessment scope, not D10 or
reset authority. Retain only the reduced receipt. Do not run the SQL directly
without the launcher: server TLS alone cannot prove client certificate/hostname
verification. Financial classification and Aqua's separate clean-start acceptance
remain required. This screen does not replace post-reset state/receipt verification
or authorize backup, reset, migration 55, deployment, D10 or live Yoco activity.

## Narrow diagnostic amendment after the authorized UNKNOWN attempt

The owner authorized a launcher-only diagnostic correction after the single
27 October 09:29:25.975 UTC attempt returned UNKNOWN. Its cause remains
undetermined; the earlier receipt did not retain the child exit status, failure
stage or completion time. This amendment does not establish that the deadline
caused that failure. The approved SQL hashes and every financial check remain
unchanged. Production use of the changed launcher requires Aqua's acceptance and
a fresh explicit owner execution window; the earlier window has expired.

The receipt adds `diagnostics.failureCategory`, an allowlisted execution `stage`,
`launcherExitStatus` and `processExitStatuses.provider/database`, plus
`evidence.finishedAtUtc` on unsuccessful attempts. A null child exit status means
no exit status was observed, including an unstarted or forcibly terminated child;
it never asserts process success. Null failureCategory means execution completed
and its receipt validated, even when the business result remains UNKNOWN.

| Failure category | Evidence represented |
|---|---|
| CONFIGURATION_OR_PREREQUISITES | Invalid invocation/artifacts, unavailable provider lookup or missing client/credentials |
| DATABASE_CONNECTION_OR_TLS | PostgreSQL client exit 2: connection failure or loss; no narrower TLS cause is claimed |
| DATABASE_IDENTITY_VERIFICATION | Rejected provider logical target or the fixed SQL source/read-only-session guard marker |
| SQL_EXECUTION | PostgreSQL ON_ERROR_STOP exit 3 without a recognized identity/timeout marker |
| RESULT_VALIDATION | Malformed provider/client JSON or rejected status-receipt shape/content |
| EXECUTION_TIMEOUT_OR_WINDOW_EXPIRY | Observed deadline expiry, ETIMEDOUT, or an exact known server timeout marker |
| UNDETERMINED | An abnormal database-client termination without evidence supporting a narrower category |

Client verbosity is `terse` with the C locale so fixed identity/timeout markers
can be recognized in memory. Provider/client error text, arbitrary error codes,
signals, counts and rows are never emitted or written to a result file. The
category describes the observed failed safeguard, not an inferred root cause.
There are no retries or weaker transport settings. Database-screen PASS/FAIL/
UNKNOWN and overall UNKNOWN pending external classification remain unchanged.

For the next authorized attempt, use a fresh 15-minute UTC window and begin in
its first minute, leaving the existing provider/client budgets inside that window.
Do not execute during this diagnostic correction or on the expired authorization.
