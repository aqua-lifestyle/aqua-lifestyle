# First-account bootstrap correction

> **NOT BUSINESS AUTHORITY.** Version 1 execution state and evidence only.

Base: application `db4e6a667ecea228f903e209941eadc7f3002bb3` (completed deployed
release). Owner requests completion using existing functionality and correction
only of demonstrated blockers. Spillover/Levels are explicitly closed; no changes
or new test executions are being performed in that workstream.

## Proven cause and smallest correction

The new regression `CleanStartAcceptsAnAccountBeforeRootSelectionWithFinancialWorkHeld`
ran against the existing middleware and account service, with Tenant registration
enabled and commissioning/payment execution held. It failed: HTTP503 instead of
reaching registration. D10 requires a real root Customer ID, and Normal requires
effective D10 authority. Existing configuration cannot create the missing first
account through a fenced HTTP/application-service path.

The correction reuses the existing Account service through an exact nonfinancial
allowlist during pending CLEAN_START with every financial/worker hold intact. The
existing Tenant1 self-registration setting, Area/default-role validation, token
verification, outbox, throttles and HTTP rate limiter are retained. No new Render
switch is introduced. The separate scope grants no Yoco dispatch/business privilege
and cannot call other application services. It closes when commissioning/payment
execution is selected. Normal operation retains its original business-lease gate.

This changes access guards only, not registration, Bird, Yoco, funeral, approval,
placement, attribution, progress or commission business implementations. The
existing isolated production-host regression now requires invalid registration to
reach HTTP400 DTO validation, retaining its assertions of no business effects.

## Configuration and inherited-file findings

`AppConfigurations` loads base JSON, Production JSON, then process environment;
development secrets/.env are not Production inputs. Render groups supply process
values and secret-file mounts; service values take precedence. No bundled D10 pins
or delivery cutoff exist. Commissioning=false requires the six pins blank, so they
must not be added piecemeal and deployed while the selection is held.

Repository inspection cannot establish live inherited values or file availability.
The owner-facing guide distinguishes known defaults, carried-forward held release
evidence and unverified private values. It gives actual value sources and the
ordered existing Render/operator operations, not invented authority documents,
hashes, IDs or timestamps. The existing evidence reader enforces non-root readable
private files, exact hashes and domain authority. Actual Render mount permissions
and documents remain an operator execution capability, not a proven code defect.

Guide: `docs/operations/aqgreen-first-customer-activation.md`.

## Evidence

- Before correction: Release build passed; the single first-account regression
  failed with HTTP503. No production operation occurred.
- Corrected Release restore/build passed. Focused first-account, Tenant/token,
  direct-service, commissioning/recovery and cutover boundaries: 173 passed,
  zero failed/skipped. After the session-Tenant and demo-seed guard refinements,
  the final account/bootstrap and unchanged registration suite passed 43 cases,
  zero failed/skipped; the final Release build passed with zero errors.
- Isolated PostgreSQL18.4/Redis Production-host boundary: one passed, zero
  failed/skipped. This is a disposable host test, not production activation.
- Mandatory local release-safety classifier: 240 passed at base HEAD with edits.
- Existing source/CI `42001784909/1` evidence was reused, not re-executed, for the
  closed placement work: 43 allocator/structural PG cases passed; the dedicated
  CLEAN_START root+six-invitation/spillover/retry test passed; relevant PG
  approval/progress cases passed. Default web-shard opt-in skips were not counted
  as PostgreSQL execution. These synthetic/provider-double facts are not genuine
  customer acceptance.

## Completion boundary

IMPLEMENTED and focused TESTED; no new deployment or operational enablement
is claimed here. Protected full CI/review and exact-source release requirements apply
to this necessary correction. The original deployed release and deployment checks
are carried forward. Current operator access/evidence files and genuine customer
acceptance remain unavailable to this agent; no outcomes may be fabricated.
