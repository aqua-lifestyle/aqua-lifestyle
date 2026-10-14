# AQGREEN V2 — LEAN MVP WORKING-SLICE AND VALIDATED LEARNING REPORT

**Phase note:** This report records the earlier backend-only qualification.
The later [interactive browser qualification](aqgreen-v2-interactive-test-money-qualification.md)
supersedes its statements about a missing browser checkout and unrun browser
tests. Neither phase contains actual customer observations.

**Local, isolated engineering report.** This document is implementation and
experimental evidence, not a business-policy confirmation, legal opinion,
production authorization, customer interview, or claim of market demand.
Status: **IMPLEMENTED** locally and **TESTED** at the backend/PostgreSQL
boundary. No commit, PR, merge, release, production enablement or actual
customer observation occurred.

## Progress standard and governing distinctions

The requested standard is **genuine member value + correct business operation +
truthful financial evidence + validated learning**. A passing synthetic workflow
addresses operation and some financial controls. It does not establish genuine
value, actual sales, sustainable economics, or customer learning.

**Scriptural instruction, in context:** [Colossians 1:16–17](https://www.biblegateway.com/passage/?search=Colossians+1%3A15-20&version=KJV)
places all things under Christ's lordship. [Proverbs 18:13,17](https://www.biblegateway.com/passage/?search=Proverbs+18%3A13-17&version=KJV)
calls for hearing and testing another account before judgment;
[Proverbs 21:5](https://www.biblegateway.com/passage/?search=Proverbs+21%3A5&version=KJV)
commends diligent planning;
[Proverbs 11:1](https://www.biblegateway.com/passage/?search=Proverbs+11%3A1&version=KJV)
rejects dishonest measures;
[Philippians 2:1–8](https://www.biblegateway.com/passage/?search=Philippians+2%3A1-8&version=KJV)
calls believers to attend to others' interests in Christlike humility;
[Proverbs 27:23–27](https://www.biblegateway.com/passage/?search=Proverbs+27%3A23-27&version=KJV)
commends knowing the state of entrusted resources; and
[2 Corinthians 8:16–24](https://www.biblegateway.com/passage/?search=2+Corinthians+8%3A16-24&version=KJV)
describes accountable stewardship of a collection before God and people.

**Derived engineering requirements:** listen without leading, challenge a
favorable internal account, plan a small complete experiment, keep honest
financial records, protect members from misleading income claims, review costs
and obligations, and make money handling auditable. These are applications of
the texts, not a claim that Scripture specifies software architecture or the
AQGreen compensation formula.

**Established business facts:** prospective V2 requires immutable recruitment
attribution and confirmation, verified joining payment, Area approval and
atomic placement before activation. Recruitment, placement, structural level,
weekly 5/5/5 sales and versioned commission are separate facts under the
[V2 specification](aqgreen-network-placement-specification.md). Legacy V1
remains authoritative outside the explicit disposable selection. D03B/D09/D10
and terminal sponsor D14B remain unresolved. No agent report resolves them.

## Working stages and exact gap

The existing code supports authorized prospective root bootstrap, invitation
resolution, test checkout reconciliation, payment-to-pending transition,
authorized V2 approval/placement, B4 structural evaluation, member progress,
B5.3 independent 5/5/5 review, B5.4 versioned ledger and historical replay.
Existing demo/browser tests exercise later member/admin views; the older
continuous E2E directly seeded child attribution and included unrelated
loan/Onyx graduation. These were repository evidence, not a first-time join
proof.

**Root cause:** `StartEntryAsync` accepted an invitation but did not persist V2
recruitment attribution or the member's confirmation; approval therefore
correctly failed closed on missing evidence. The local change carries the
resolver's immutable invitation and sponsor participation IDs through joining
and records one attribution and one confirmation in the joining transaction.
It requires an existing sponsor placement scope and rejects independent or
recruiter-ID-only joining when the isolated V2 path is selected. A retry checks
the original evidence rather than reconstructing it. Normal production joining
selection stays disabled; the dedicated demo boundary is opt-in and
non-production only. The test checkout uses synthetic money and a local fake
gateway. No live provider confirmation is claimed.

## Experiments and observations

The [discovery and observed-task scripts](aqgreen-v2-lean-mvp-discovery.md)
ask about recent actual purchases, use, alternatives, refunds and evidence,
then ask members/admins to perform neutral tasks in a disposable environment.
They record wrong turns and assisted completion; they preclude invented
interview findings. The three leading assumptions are (1) goods/services
are valuable to buyers without recruitment rewards, (2) people can complete
and understand the actual workflow without developer help, and (3) real net
commerce supports all fulfilment costs and compensation. All remain hypotheses.

**Actual behavioural pilot:** NOT RUN. No customer interview or human observed
task was provided or conducted. Automated test identities and sales references
are synthetic; they cannot measure willingness to buy, repeat demand,
customer understanding, seller behavior or operator workload.

## Commercial and legal boundary

The software's requirement for a 5/5/5 review is stricter than recruitment
alone, but entered evidence references do not by themselves prove external
customer purchases, delivery, non-refund, independence from recruitment, or
available commercial margin. A held/earned test ledger is a model decision,
not a verified source-of-funds record. No product demand or commission
sustainability finding follows from synthetic quantities.

For competent South African legal review before commercial activation, ask:

1. Under [Consumer Protection Act section 43](https://www.gov.za/sites/default/files/32186_467.pdf),
   do the actual compensation sources and promotional emphasis make income
   primarily recruitment-derived, even if each participant submits 5/5/5 sales?
   What independently verified non-member sales and funding records rebut that
   concern? The [National Consumer Commission](https://thencc.org.za/spot-pyramid-schemes-from-a-distance/)
   flags recruitment compensation and unrealistic returns in its public guidance.
2. Under CPA section 41 and related consumer-information rules, do invitation,
   product, joining fee, expected income, status, hold and refund statements
   accurately explain risk, obligations and when money may be released?
3. Which CPA and Electronic Communications and Transactions Act cancellation,
   return/refund, direct-marketing and plain-language duties apply to each
   consumer transaction and enrolment channel? Who owns evidence and remedies?
4. What additional legal/sector rules govern any bundled funeral-cover benefit
   and membership commitment? This report does not infer authorization for it.

**Owner/business confirmation needed:** source and economic treatment of joining
fees; independent customer sale definition and acceptable sales proof;
allocation of margins across all commission recipients; refund/chargeback
effects; break-even and no-new-joiner scenarios; disclosure wording; D14B
terminal sponsor handling for affected prospective cases. Retain 5/5/5 as the
approved test rule until an authorized versioned change. Do not label any of
these `CONFIRMED` from tests or this report.

## Minimum work before a safe isolated human pilot

- For a self-directed browser pilot, wire a loopback-only checkout simulator
  and completion screen, conspicuously marked TEST. The fake checkout adapter
  used by the passing Web test is test-assembly-only; no member-facing test
  checkout has been verified. Keep synthetic roles/records and prohibit
  payout/release. Re-run the current proof and negative cases after that build;
  observe checkout return, refresh, return-later and separate sessions.
- Bring a small consented set of real people to the prepared tasks. Capture
  de-identified actions, refusals and problems in a separate observation
  register. Do not claim customer learning until this has occurred.
- For any economic experiment, use real redacted independent-customer order,
  delivery, refund and cost evidence with lawful handling. Do not import this
  evidence into test ledgers as if the synthetic calculations paid it.
- Obtain qualified legal/business review before any commercial invitation,
  payment, payout, production V2 enablement, migration or cutover.

## Recommendation

**PROPOSED:** continue the isolated working-slice and human discovery pilot
after the technical validation reported below; change any unclear steps or
unsupported sales claims revealed by observation; stop commercial activation
until genuine value, economics and legal questions have independent evidence.
The recommendation does not authorize external outreach or production action.

## Validation record

| Evidence on current local implementation | Actual result | Limit |
| --- | --- | --- |
| Release restore and affected backend test-project build | Passed, zero errors; existing warnings/advisory output | Compile evidence only. |
| Fresh PostgreSQL 16.15 six-member test-money working slice | Passed 1/1; direct service retry passed 1/1; final wrong-amount-specific assertion rerun passed 1/1 | Test adapter and seeded identities; no live provider or browser. |
| Separate PostgreSQL missing-history/missing-scope cases | Passed 1/1 | Does not settle unresolved historical policy. |
| Durable PostgreSQL inspection | Six each of participation, attribution, confirmation and placement; zero loans/Onyx; zero released or paid ledgers | Disposable synthetic records. |
| Broad Web project | Passed 93, skipped 2 PostgreSQL-only MVP tests (executed separately), failed 0 | Began before the last test-fixture edit; relevant final direct PostgreSQL run passed. |
| Focused V1 joining, attribution and demo-selector compatibility | Passed 53/53, failed 0, skipped 0 on final source/test build | Focused component coverage; no broad Application-suite claim. |
| Broad Application suite | Cancelled after more than 20 minutes in unrelated PostgreSQL migration fixtures; no final result | The Web-project portion passed 93/skipped 2 before cancellation; suite-wide pass is not claimed. |
| Entity Framework model comparison | Unavailable: installed `dotnet-ef` 10.0.9 versus project EF Core 8.0.8 | No schema/model changed; disposable PG startup applied existing migrations. |
| Production dependency advisory audit | Completed with pre-existing findings; no package changes | A clean security audit is not claimed. |
| First-time browser, provider acceptance, observed tasks and real commerce | Not run | Cannot establish human completion, commercial viability or demand. |

The first three focused attempts failed on test setup (missing harness unit of
work for invitation reading, required dummy account password hash, and a
versioned due-policy fixture). The defects were corrected and retained in
logs; assertions and failure categories were not weakened. Final TRX, run
logs, test-money snapshot and exact Git state are recorded in the
[execution worklog](../exec-plans/completed/aqgreen-v2-lean-mvp.md).

Independent verdict: **IMPLEMENTATION** meets the isolated backend working
slice; **WORKFLOW** is partially complete for actual actors until the browser
test checkout and human task observation occur; **EVIDENCE** is PostgreSQL
integration, component tests and inspection, with no external-user, provider,
deployment or production evidence.
