> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

# D10 one-time activation lifecycle

## Goal and authority

The current owner's explicit **AQGreen V2 — Correct D10 Authorization Expiry
Lifecycle** instruction authorizes this focused correction and its protected
deployment/activation: commissioning authorization expires before first committed
activation, but a validly commissioned programme has no recurring authorization
expiry. All independent financial, Tenant, Area, payment, membership and runtime
integrity safeguards remain. No schema, infrastructure or credential changes.

Baseline: main `6fe686a67408cfd025b5e7e3770fa29de070a57e`, currently deployed held.
Branch `fix/d10-one-time-activation`; historical reset/conservation/schema evidence
is accepted and will not be reopened. This plan does not supply a new acceptance.

## Complete impact and design

- The sole current-time expiry predicate is `EnsureCurrent`, called by the shared
  runtime check used by startup, joining, approval, progress, sales and financial
  selectors. Correct that shared boundary, not individual callers.
- Reuse the existing transactionally locked cutover control JSON. A first Normal
  commit records a versioned D10 activation binding to the exact Decision and
  separately verified authority, original activation time and authorization limit.
  A later Normal/recovery transition retains that original activation.
- Read only actually committed control state in a separate current-read UOW;
  never use an in-flight object, process flag or cache as committed activation.
  This also avoids reacquiring the exclusive cutover lock from inside its own UOW.
- Validate complete ordered phase history, authorized operator, exact bindings,
  first Normal from Replay, timing and consistent receipts. Bare/corrupt/partial,
  future, late or foreign Normal cannot waive expiry, even before expiry.
- Before the first commit, retain expiry and effective-time checks; guard the new
  activation again at persistence/commit. After commit, validate the original
  timely activation while retaining configuration/source/schema/population checks.
- Restart rehydrates evidence and reads the durable receipt anew. Existing payment
  capture/replay, V1 processing, separate approval and held workers stay governed
  by their independent controls. No historical receipt is synthesized or upgraded.

## Verification plan

Focused model and cutover negatives; actual PostgreSQL rollback/concurrency;
expanded clean-start lifecycle covering preactivation expiry, committed activation,
operations after expiry, fresh-source/runtime startup and independent safeguard
failures. Full backend/host/container CI on the new source before deployment.
Inspect final diff and exact source pins, then publish only the required executor
successor binding. Actual protected deployment and genuine activation remain
separate evidence steps; missing owner/operator facts will not be invented.

## Current state / next action

IMPLEMENTED / TESTED locally: Release restore/build;196 focused component cases;
15 PostgreSQL cutover cases; complete isolated joining lifecycle after real wall-clock
expiry, including two new Production processes and rejection of a receipt-stripped
Normal process. Zero skips in the passing runs. Latest lifecycle also rejects
removal of the authority and retains configuration/payout guards after expiry.

Two earlier local lifecycle invocations failed because the local command omitted
the CI fixture's `ConnectionStrings__Default` and then `REPRO_MARKER_DIR`. Neither
is counted as passed. The complete correctly provisioned fresh-DB runs passed;
no assertion or fixture requirement was weakened. Backend restore/build retains
existing warnings. Dependency files are unchanged; the standalone advisory command
with an explicit solution failed parsing, and the documented command timed out.
No clean backend advisory claim is made; hosted full CI is still required.

All runtime expiry callers were traced: unused-authorization validation in the
shared runtime check, and strict creation of the first activation. Continuing
operations/startup retain the same independent checks. No production mutation or
new D10 acceptance has been performed. Next: full CI/review, one application PR
and the necessary exact-source executor binding, then protected deployment.
