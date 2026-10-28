# Yoco receive-only AQGreen cutover

Status: IMPLEMENTED; validation and human admission are recorded in the existing
[clean-start worklog](../exec-plans/active/aqgreen-v2-clean-start-20261027.md).
This procedure authorizes no production operation. Xolani-M operates; Aqua must
accept this amendment independently before a separately authorized cutover.
Existing owner-attested test classification and post-retirement conservation
acceptance retain their distinct meanings. Automatic payouts remain disabled.

## Two artifacts, one durable inbox

The compatibility receiver is built from the actual deployed revision
`1251fb82979e18be53d1d0e7f5c6fab2d0c50a58`, with only the receive-only patch. Its
separate immutable source pin is in `tools/operations/yoco-receiver54.json`.
Its Dockerfile compiles `YOCO_CUTOVER_COMPAT` into the entry assembly. The marker
forces receive-only operation: configuration cannot enable business processing,
replay or normal operation. It supports the exact original 54 migrations and the
same history plus the already-reviewed migration 55. It contains no new migration
and performs no DDL. Do not deploy the full V2 source before migration 55.

Verified events are stored in the existing PostgreSQL `AbpBackgroundJobs` table,
with job type `Aqua.YocoCutover.ManualHeldEventV1`; phase uses
`Aqua.YocoCutover.ControlV1`. These are manually held records with `NextTryTime`
at the maximum date, not executable ABP jobs. Original bytes, event identity,
payload SHA-256 and verified signature evidence are retained after replay. No
expiry or deletion is introduced. Existing restricted database custody, audit
and retention apply; never print or spool event bodies or provider references.

All receiver instances use the same transaction advisory lock. Event identity
and payload are deduplicated within that transaction; conflicting deliveries
are rejected. HTTP 200 follows the capture transaction's commit. A failed commit
returns 503, not success. Disconnect or response loss after commit is safe to
retry. Business processing is a later transaction; a failure preserves the inbox
and leaves the event held. The bounded store rejects unsupported history,
corrupt rows or more than 10,000 protocol rows rather than losing events. Each
request is limited to 64 KiB. Operator status/health monitoring and existing DB
capacity must remain available; storage exhaustion is a stop condition, never a
reason to acknowledge an uncommitted event. This implementation does not promise
unlimited provider retries or unlimited database storage.

## Activation and drain (separate production authorization)

1. Use the same existing Render service and database. Keep Render maintenance
   **off**, auto-deploy **off**, authenticated database TLS and existing ingress
   unchanged. Preserve Yoco signing credentials; never place them in arguments,
   source or chat. No additional service, disk, database or paid restore is used.
2. Approve the pinned compatibility receiver deployment and bounded verification.
   Configure `App__YocoCutover__Enabled=true` and
   `App__YocoCutover__OperatorUserId=<authenticated authorized host user id>`.
   Set `App__BackgroundJobs__Enabled`, `App__WeeklyCommissions__Enabled`,
   `App__EntryMonthlyObligations__Enabled`, `App__AutomaticPayouts__Enabled`,
   `AQGreenV2Demo__Enabled`, `AQGreenV2Pilot__Enabled`, `SEED_DEMO_DATA`,
   `App__FinancialOperations__Enabled` to `false`. Preserve the existing validated
   Bird configuration; no email worker or ABP job executes while the cutover
   protocol is enabled. Do not weaken normal production provider validation.
   Set `App__Recovery__Enabled=false`; the old recovery mode rejects webhook
   credentials and cannot serve as this receiver. Leave D10 commissioning held.
3. Deploy the **receiver source pin**, not the full V2 main commit, with the
   existing API Dockerfile/runtime target. Wait for Render to finish replacing
   every old instance and terminate/drain old workers and DB writer sessions.
   Until the old deployment is completely drained, the fence is not established.
   HTTP middleware alone cannot fence old binaries, direct SQL clients or another
   writer service. Stop unless all existing writers are accounted for and held.
4. On the authenticated host-operator API, POST
   `/api/operations/yoco-cutover/phase` with `{"phase":"ReceiveOnly"}`.
   GET `/api/operations/yoco-cutover/status` must show `ReceiveOnly`, no migration
   55, and `receiverOnlyArtifact=true`. The response header must be
   `X-Aqua-Yoco-Receiver: Schema54`. Verify joining/checkout/approval/commission
   writes return 503. Operations require the configured **host** user and the
   existing `Pages.Tenants` permission; a Tenant session or another host user is
   denied. Use bearer authentication through the existing restricted channel.
5. Perform only the separately authorized bounded delivery/drain verification.
   Confirm valid signed webhook receipt is durable and duplicate receipt exact;
   invalid signature is rejected. No synthetic live payment or provider mutation
   is implied. No reset is permitted while any old writer remains. Record the
   live receipt/fence evidence on PR #9 without financial identifiers or secrets.

A configuration reload cannot silently disable an enabled fence or change its
operator. Background execution and external checkout creation also fail closed.
A receiver restart requires the mode to remain enabled. Old accepted payment
relationships are never deleted or overwritten by capture.

## Coordinated execution and release

After the owner authorizes the exact window and pins:

1. Apply the already-approved bounded recovery safeguard; retain its existing
   stop/undo limitations and zero-additional-charge requirement.
2. Establish and verify the receiver fence and drain above. The receiver continues
   taking durable callbacks throughout retirement and migration; short database
   lock failures return non-2xx for provider retry. Do not knowingly discard events.
3. Execute the unchanged, approved test-participation retirement procedure.
4. Run conservation verification. **Aqua must accept the actual conservation
   results before migration 55**. On mismatch, stop with the fence held and use
   only the approved pre-55 bounded undo while its conditions still hold.
5. Apply the retained reviewed migration 55 exactly once; verify schema/history.
   Do not downgrade it after committed new-schema/financial use. Use the existing
   forward-only held remediation procedure on subsequent failure.
6. Record the actual receiver deployment ID/source in the existing protected
   executor baseline/readback admission, using the existing pin-refresh review
   and main-CI gates. Do not fabricate the not-yet-created deployment ID or leave
   the legacy Render SHA as the claimed current source. The full V2 candidate
   must descend from the pinned receiver source; the migration delta is still
   exactly migration 55. This is a source/evidence pin update, not permission to
   weaken the executor or adopt an unrelated deployment.
7. Deploy the accepted full V2 Render artifact with receive-only still enabled,
   then the accepted Vercel artifact. Verify the runtime and keep the phase held.
   Receiver and full artifact use the same inbox/phase protocol during overlap.
8. Complete the existing D10 native/evidence commissioning gates, using the real
   authorized effective boundary. Keep payouts and workers disabled. D10's
   first-root API remains subject to its original owner-selected customer,
   Tenant, Area, permissions, idempotency and payment-before-activation rules.
9. Obtain authoritative Yoco reconciliation and accepted replay evidence below.
   After every held event is resolved and D10 permits writes, release `Normal`.
   Commission the authorized first root and perform controlled live joining,
   payment, Area approval, placement and commission validation under their
   separate operational authorization. Provider payment is not activation.
10. Deactivate receive-only with the checked `Normal` phase, retaining the durable
    capture protocol. Do not toggle the protocol off during a rolling deployment:
    an old receiver could otherwise enqueue after a new instance's startup check.
    Startup rejects configuration disablement after phase initialization. Workers
    and payouts remain disabled; any later protocol removal or retained-inbox
    archival needs its own reviewed drain/retention procedure. The bounded inbox
    limit is an explicit stop condition, not permission to discard evidence.
    A late cutover-era event during overlap requires reconciliation and closes
    the business fence again.

## Controlled reconciliation and replay

GET `/api/operations/yoco-cutover/events` is a restricted operator response with
inbox IDs, event IDs, hashes and statuses, not raw payment bodies. Access matching
checkout/payment facts and merchant evidence only through the separately approved
restricted records channel; do not publish or export them to ordinary logs.

The operator compares pending/delayed references with authoritative existing
Yoco merchant/business records, including relevant final status, amount,
currency, mode, refunds, settlements, disputes and obligations. Application test
flags and an owner test-data attestation are not provider verification. An absent,
unmatched, disputed or unverifiable reference stays **Held**; never mark it paid
or discard its receipt to open the fence. A review of only received events does
not prove there are no outstanding provider events: reconcile the existing
outstanding checkout/payment references as well before authorizing release.

For events approved for replay, produce the exact
`Aqua.YocoCutover.ReconciliationV1` contract defined in `YocoCutoverService.cs`:
operator ID, truthful `OPERATOR_ATTESTED_AUTHORITATIVE_YOCO_RECORDS` authority,
Aqua acceptance and owner-operation references, UTC acceptance/expiry (maximum
24 hours), and unique matched event entries. Release also requires explicit
`OutstandingReferencesReconciled=true`, `UnresolvedObligationsRemain=false` and
`CapturedEventSetSha256` for the complete current inbox. The canonical digest is
`YocoCutoverService.EventSetHash`: SHA-256 of UTF-8 System.Text.Json serialization
of all `{EventId,PayloadHash}` objects ordered by ordinal EventId. A callback that
changes that set invalidates the release manifest. Phase transitions retain
operator, UTC time and release-manifest digest in the durable control record. Each entry pins event ID, raw hash,
Tenant, payment ID, provider checkout ID, amount, currency, mode, event type,
`Disposition=MATCHED`, authoritative protected record reference and evidence
SHA-256. These fields are restricted evidence, not chat output. The reference
and hash do not themselves prove review: Xolani-M verifies actual source access
and Aqua's authenticated acceptance of the exact manifest before activation.

Use the existing protected runtime secret-file mechanism, outside the checkout,
without symlinks, mode 0600 file/0700 parent. Pin its absolute path and SHA-256 in
`App__YocoCutover__ReconciliationPath` and
`App__YocoCutover__ReconciliationSha256`. Retain the source evidence and accepted
manifest in existing approved custody; an ephemeral runtime copy is not archival
retention. Do not create a new paid disk or store payment evidence in GitHub.

POST phase `Replay` only on the full artifact after 55. POST
`/api/operations/yoco-cutover/replay` with `{"id":<inbox row id>}` for each accepted
event. Replay rechecks the actual checkout's Tenant/reference/purpose/amount/
currency/mode, uses the existing payment locks/receipt idempotency and commits
business effects together with the replay status. Original raw evidence remains.
Verified test events retain their existing no-business-effect behavior.
Retired financial-baseline changes, allocation exceptions, missing checkout
relationships and other unresolved cases remain held. They cannot be silently
made compatible with the immutable conservation/D10 baseline. Resolve such a
case through the existing independent acceptance process before release.

POST phase `Normal` is rejected with pending/held events or missing D10 write
admission. Normal callback capture still commits first, then dispatches under the
same lock; a processing failure holds the event and fences later business writes.
Activating `ReceiveOnly` drains patched HTTP business leases before committing;
a three-second lock wait failure means activation did not pass, not that the
fence is established. Keep the receiver reachable on every stop path. Retain the
inbox and all original financial/audit records; never replay through an old
unfenced backend, delete held rows, weaken a passing reset guard or enable payouts.

## Validation scope

The focused tests use the existing PostgreSQL 18.4 fixture, real signatures,
transaction rollback/commit boundaries, concurrent delivery/replay, source
integrity, controlled reconciliation and HTTP/direct-call fencing. The separate
compatibility-host test uses the actual published receiver in Production mode,
the existing synthetic PostgreSQL/Redis infrastructure, abrupt restart, 54→55
compatibility and forced-mode rejection. CI builds the pinned receiver before
that mandatory test. Synthetic tests do not prove production delivery, merchant
reconciliation, live drain, conservation acceptance or operator authorization.
