# Runtime authority evidence

Starting source:700fadb58ced50ae0d63319ad66db3f0674687ad, PR106. See the
[capability matrix and production requirements](../../operations/runtime-authority-qualification.md).
No production mutation, provider delivery, rollout or database session was performed.

## Discriminating negatives

Before application correction, RuntimeAuthorityTests executed17 cases:17 failed,
0 passed/0 skipped. The failures covered independently registered workers under
BackgroundJobs=false, financial workers under FinancialOperations=false, direct
outbox execution, direct Bird with a configured synthetic key, monitor recovery,
missing configuration, live Yoco checkout/notification and legacy referral awards.
Controlled HTTP handler accepted attempted delivery; no real provider was contacted.
Then an added direct-financial-service test failed against the partially corrected
source, proving that worker/webhook guards alone do not protect directly resolved
orchestrators. Calculators/scheduler/travel/payment-application entry points were
corrected at their own execution boundaries. Tests retain strict denial assertions.
The [compact before-correction observations](before-correction-results.json) retain
case identities, failed counters and raw local TRX hashes without process logs.

## Isolated execution and classification limits

Initial focused application128/128 and Bird gateway13/13 passed without skips.
After narrowing synthetic dispatch grants to the specific outbox tests, follow-up
69/69 passed. These intermediate counts predate the final direct-orchestrator change.
The subsequent direct-service correction passed133/133 without skips. The expanded
suite then passed151/151, including the real-calculator demo positive with global
financial authority held. Five additional negatives reproduced masking of existing
pilot authorization denials: held workers returned before the pilot check and the
scheduler threw the generic financial hold first. Ordering now preserves recovery
no-op, explicit pilot denial and ordinary execution holds. The final focused suite
passed156/156 with zero failures/skips after restoring that ordering. Complete hosted
CI results are reported on PR106 after execution.
Known local build/discovery
attempts with no matching direct-financial test did not count as executed evidence;
an explicit rebuild followed by test execution established its failing precondition.

The actual-image probe passed17 checks on the first corrected image: distinct
maintenance/API images; real application schema on disposable PostgreSQL18+Redis;
normal Production API recovery=false; held outbox/business fingerprints beyond30s
through start, actual restart and replacement; missing required cutoff stops startup.
The non-owner API role denied DDL, role escalation, history/financial/admin mutation
and outbox claim via SQLSTATE42501. It retained only read/audit/login/key-store and
enqueue grants. This proves bounded startup/held-state isolation, not full user
write workflows or production grants. Final runtime-source images were rebuilt and
the [17-check probe rerun passed](runtime-restart-probe.txt): API image
`sha256:a0b1e184f74d90d7dabb8fefcdc25e1abc7fede7a3c76a18c9e1d74505a010ca`,
maintenance image `sha256:1e30a1d95541a5b46e7fb8ddf62378bfdb837c681dfaf87712ed12555bbd423c`.
These local images predate the pilot-denial ordering follow-up; they are carried-
forward observations, not provider attestations. Hosted CI rebuilds final source and
retains its exact source/run/attempt artifact. Final runtime-source API image
`sha256:36b454eb90b2498ea1ec33f5062af1bf711f8f5d9875c4b6eaf5928b40ee3d1e`
was rebuilt; maintenance content ID was unchanged. Its probe rerun also passed17/17.

Initial probe failures were real failures: one synthetic webhook secret was shorter
than the valid minimum and startup rejected it. The fixture was corrected; startup
validation was not weakened. Agent2 found ordinary Compose still required migration
credentials during profile interpolation; maintenance was moved to a separate overlay.
The probe verifies base configuration without a migration credential.
Agent2 also found the direct calculator hold affected the existing disposable demo;
a fully validated demo composition capability preserves that simulation while global
financial authority stays held. Its new integration test initially used the wrong
test-container accessor, causing a compile failure before execution; the accessor
was corrected without changing application guards or assertions.

The first hosted run at1ed5c48 failed a PostgreSQL concurrency positive:39 passed,
one timed out waiting for two advisory-lock waiters. Its synthetic payment now
correctly stopped at the new financial hold before locking. The isolated test now
explicitly grants financial execution before racing payment and approval; no lock,
timing, transaction or business-outcome assertion was weakened. Other runtime holds
and production configuration remain unchanged. This failed run is not passing evidence.
The next hosted PG job passed the base40 and member/Area24, then D10 passed13 and
failed1: the prospective-AQGreen/Onyx case likewise lacked explicit synthetic
financial authority at its first payment. Agent2 confirmed the exact guard stack
from TRX. That case alone now grants test-local financial execution after its
PostgreSQL-mode assertion, preserving its D10 scope/error, no-write, provenance,
Tenant/Area and provider-call assertions. Final hosted evidence must pass all78
dedicated PostgreSQL cases with no skips; these earlier failures are retained.
The first attempted D10 fixture grant was attached to configuration that the D10
setup then replaced. Both d1e549a hosted runs therefore still failed13/1 in D10.
The corrected case grants on the returned D10 configuration after replacement and
asserts IoC resolves that exact instance. The complete14-case local D10 suite passed
on fresh PostgreSQL18 in an internal Docker network, with no provider access or
host credential mounts. This isolated test result does not qualify production grants.

209 explicit mandatory Node security cases now include the required container probe
workflow assertion; all208 previous identities are retained and AQUA-SEC-0209 is
appended. New script/Docker/Compose sources are explicitly pinned. Pin updates are
owner-authorized control changes within this task, not independent custody. The
171-case retained mutation fixture still rejects170 passed/one skipped despite raw
exit0; wrong source/attempt, forged/missing reports and changed gate/inventory are
fixture negatives, not proof of installed external enforcement.

## Actual production observations and unknowns

production-readonly.json contains only whitelisted fresh authenticated GET fields
from2026-10-16T19:18:23Z, with public API health. No provider secret values, raw
environment response, connection string, export URL or customer data retained.
The live1251fb8 API reports Healthy; declared normal/Bird/test-Yoco and held flags
are unchanged. The inspected runtime-related source matches starting source for
those paths. That comparison is not running-output or live-grant attestation.
Production database privileges, dispatcher/financial separation, provider IAM,
effective egress policy and verifier/credential custody remain UNKNOWN/BLOCKED.

The read-only NuGet direct/transitive advisory report found pre-existing advisories
in AutoMapper, caching, dynamic LINQ, JSON, cryptography and ImageSharp runtime
dependency graphs, plus test-only packages. Package versions are unchanged; broad
runtime reachability/exploitability was not established. This is disclosed security
debt, not a clean audit and not automatically test-only. No dependency warnings
were suppressed to pass validation.

Final commit, complete CI run/attempts, downloaded test/probe artifacts and fresh
exact-source Agent2 verdict are reported on PR106 after publication. Their result
does not elevate production readiness without separate live authority evidence.
