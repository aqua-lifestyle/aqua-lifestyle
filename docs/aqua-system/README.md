# Aqua system documentation

This dedicated folder contains the Aqua system documentation pack. It is intentionally separated from the repository's legacy business, development, and verification documents so that readers have one current entry point without erasing historical sources.

The pack explains Aqua at several levels without making one document carry every detail.

| If you are... | Start here |
| --- | --- |
| A client or business leader | [01 — Client system overview](01-client-system-overview.md) |
| Looking for the exact business rules | [02 — Business rules and workflows](02-business-rules-and-workflows.md) |
| Trying to understand commissions | [03 — Commission engine explained](03-commission-engine-explained.md) |
| Investigating payment, Yoco, or Area approval | [04 — Payments, approval, and Yoco](04-payments-approval-and-yoco.md) |
| Touching historical data, migrations, or legacy members | [05 — Data, history, migrations, and legacy members](05-data-history-migrations-and-legacy-members.md) |
| Deploying or enabling automation | [06 — Operations and enablement runbook](06-operations-and-enablement-runbook.md) |
| Checking evidence, decisions, risks, or readiness | [07 — Verification, decision, and risk register](07-verification-decision-and-risk-register.md) |
| Understanding Tenant versus business Area | [08 — Tenant and Area boundaries](08-area-and-tenant-boundaries.md) |
| Reviewing resolved AQGreen Placement V2 design semantics | [AQGreen Network Placement V2 specification](aqgreen-network-placement-specification.md) |
| Resolving decision authority or status | [07 — Business-decision authority convention](07-verification-decision-and-risk-register.md#p-business-decision-authority-convention) |
| Choosing repository validation | [Repository validation commands](../development/validation.md) |
| Performing an engineering review | [Review benchmark](../review-benchmark.md) and the repository [`review-change` skill](../../.agents/skills/review-change/SKILL.md) |
| Resuming a long-running agent workstream | [Execution plans and worklogs](../exec-plans/README.md) |

## Authority and status

The documents have different jobs:

1. Confirmed business-owner decisions are authoritative for business intent.
2. Document 02 records those rules precisely. An `UNRESOLVED` label means no rule may be invented.
3. Documents 03–06 explain the implementation and safe operation at the repository baseline named in document 07.
4. Document 07 is the evidence and contradiction register. It records what is implemented, tested, merged, deployed, enabled, or production verified; those terms are not interchangeable.
5. Document 01 explains the system to a non-technical reader. It does not override document 02.
6. Older repository documents and archived WIP remain useful evidence, but do not override a later confirmed decision or current code. Material conflicts are listed in document 07 rather than silently resolved.
7. Current repository behavior and historical decisions remain governed by AQGreen V1 unless separately evidenced. The [AQGreen Network Placement V2 specification](aqgreen-network-placement-specification.md) is authoritative only for resolved V2 design semantics; the document does not implement or enable V2. `AQG-V2-D03B`, `D09`, and `D10` migration and cutover work remains unresolved.

Status labels used throughout the pack are `BUSINESS DECISION`, `VERIFIED IMPLEMENTATION`, `ENGINEERING INFERENCE`, `HISTORICAL`, `SUPERSEDED`, `UNRESOLVED`, and `PLANNED / NOT ENABLED`.

Business-policy records use `UNRESOLVED`, `PROPOSED`, `CONFIRMED`, and `SUPERSEDED`.
`UNRESOLVED` means no confirmed policy currently exists; a `PROPOSED` candidate does
not change that. Only explicit owner authorization with the durable evidence defined
in document 07 can establish `CONFIRMED`. This lifecycle does not replace the
implementation/evidence status ladder.
