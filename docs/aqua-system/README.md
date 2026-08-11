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

## Authority and status

The documents have different jobs:

1. Confirmed business-owner decisions are authoritative for business intent.
2. Document 02 records those rules precisely. An `UNRESOLVED` label means no rule may be invented.
3. Documents 03–06 explain the implementation and safe operation at the repository baseline named in document 07.
4. Document 07 is the evidence and contradiction register. It records what is implemented, tested, merged, deployed, enabled, or production verified; those terms are not interchangeable.
5. Document 01 explains the system to a non-technical reader. It does not override document 02.
6. Older repository documents and archived WIP remain useful evidence, but do not override a later confirmed decision or current code. Material conflicts are listed in document 07 rather than silently resolved.

Status labels used throughout the pack are `BUSINESS DECISION`, `VERIFIED IMPLEMENTATION`, `ENGINEERING INFERENCE`, `HISTORICAL`, `SUPERSEDED`, `UNRESOLVED`, and `PLANNED / NOT ENABLED`.
