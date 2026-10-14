> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

# AQGreen V2 administrator playbook — Agent 2

## Goal and authority

Deliver practical joining, manual weekly sales, duplicate allocation, exceptions,
authority and audit procedures. Latest owner brief makes every authenticated
administrator eligible to review sales; it does not grant cross-Tenant/Area record
access or payout/correction authority. Current 5/5/5 and joining rules persist.
Agent 1 owns code/tests; no retail, inventory, production or B7 work.

## Baseline and evidence

HEAD `e9bd8e96d55d03466492ffd40dd657802ae36d4b`, branch
`feature/admin-ui-remediation`, concurrent unstaged/untracked work preserved.
System index and prior authority/operating documents inspected. Current review
service still rejects tenant admins; DTOs lack held/confirmed reason fields.
Read domain finalization, UI, commission action and consumer, selected test source,
Agent 1's latest operational worklog and newly present signup/instalment spec.
New test source is not a passing result; no tests or production observation here.

Contextual Scripture from prior research retained; read 1 Corinthians 4:1–5 for
stewardship in its apostolic context. Proposed applications must have observable
controls, not claims that Scripture prescribes implementation or success.

## Proposed smallest operating mechanism

One access-controlled manual register with review-case log and sale-line allocation
entries, a reservation before final confirmation, and a single active writer to
serialize allocations. All eligible admins may review; the register writer is an
on-duty coordination task, not a host-only reviewer role. Cross-Area coordination
must use authorized access, not permission inference. Companion log supplies
reasons not currently accepted by the app. No platform or new dependency.

## Completed work and evidence

Delivered `docs/aqua-system/aqgreen-v2-mvp-administrative-operations-playbook.md`:
joining/sales numbered procedures, one shared register with sale-line reservations,
exception table, all-admin eligibility versus record/financial authority, minimum
audit fields, scoped implementation classifications and five owner decisions.
Added a later-owner-decision note to Agent 2's previous operations comparison.

Acceptance criteria explicitly distinguish manual confirmation from independent
verification, held evidence from payout holds, and duplicate decisions from
duplicate sale allocation. Companion log handles held/confirmed reasons absent
from current DTOs; no in-app reason capability is invented. Single-writer manual
coordination is proposed and untested, not a new restricted reviewer role.

Read back playbook, checked source/authority links and instruction consistency.
`git diff --check` passed; no-index whitespace checks for both documents emitted
no diagnostics. HEAD unchanged; staged names empty at inspection. No application
test rerun, customer-data/production access, real payment, code edit, staging or
publication. Agent 1's ongoing operational tests are not claimed as passing here.

## Handoff / next action

Agent 1: remove obsolete host-only review eligibility across all read/write paths,
retain Tenant/Area record checks and demonstrate relevant access/negative cases;
clarify manual-review/pre-checkout wording. Owner: settle record scope, actual
joining receipt channels, evidence standard, register/reason-log adoption and
late/correction authority. No retail platform, B6/firewall or B7 expansion.
