# AQGREEN V2 — INDEPENDENT REVIEW OF AGENT-EXECUTED WALKTHROUGH

**Agent 2 · 12 October 2026 · Recorded-evidence verdict: A. ALL 17 CASES PASSED — READY FOR OWNER ACCEPTANCE REVIEW.**

This verdict endorses the bounded engineering evidence in [Agent 1's results](aqgreen-v2-agent-executed-operational-mvp-walkthrough-results.md), including the follow-up execution prompted by this review. Agent 2 independently inspected source and retained evidence; Agent 2 did **not** independently operate the browser, query the running database or rerun suites. No human usability, commercial demand, externally verified sales, owner acceptance, production qualification or real-money authorization is established.

## Candidate and evidence examined

Reviewed branch `feature/admin-ui-remediation`, HEAD `e9bd8e96d55d03466492ffd40dd657802ae36d4b` plus the preserved local candidate. Recomputed the [source manifest](../verification/aqgreen-v2-agent-walkthrough-20261012/source-manifest.sha256): all **1,739** files matched, with SHA-256 `72219b17d8cd5a55f9c0b542190caa969bb5193131fa2924fdefcdff54d1e26c`. Inspected the candidate/artifact identifiers, runtime record, all **129** journal entries, the 17-row final case-results record, manual register, auxiliary-check record and focused Vitest log in the [evidence directory](../verification/aqgreen-v2-agent-walkthrough-20261012/).

The journal preserves failures and subsequent retests. Sequence and timestamps are ordered; an initial scan found no retained JWT, Bearer token or provider-key patterns. This is a bounded evidence inspection, not a comprehensive secret audit. DLL hashes are supplied artifact identifiers, not independently rehashed artifacts in this review.

Runtime evidence supports disposable PostgreSQL, `AQGreenV2Demo`, no default API/frontend network route and a browser namespace without a network route. The inspected synthetic adapter has no provider client; the real gateway rejects demo configuration before sending HTTP. Provider delivery, settlement and signed real Yoco callbacks were not exercised or claimed. Next dev on Node 26, the task-copy loopback-origin override and lack of a Node 24/production-build run are correctly disclosed.

## Case-by-case assessment

“Supported” below means Agent 1's newly recorded execution supports the checklist's isolated software/procedure outcome. It is not an Agent 2 runtime PASS or a human observation.

| Case | Recorded result | Independent evidence assessment |
| --- | --- | --- |
| J1 | PASS | Supported: signup/email verification before placement, valid invitation attribution, immutable retry/conflicting sponsor denial, invalid/self/wrong-programme rejection and mailbox denial; #20–22,35,55,65,77,86,99,114–115,119. No invented invitation expiry is claimed. |
| J2 | PASS | Supported: R1,200 synthetic checkout, pending approval without placement, later completed-joining denial, provider/amount cardinalities; #26,117,124. |
| J3 | PASS | Supported: first R600 remains non-Active, second distinct checkout/payment completes R1,200, matching retry does not duplicate; #36,52,124. Navigation-read error remains recorded. |
| J4 | PASS | Supported: wrong amount rejected, exact replay returns already-confirmed, other member denied, durable totals unchanged; #24,27,52,124. This is not signed-webhook authenticity evidence. |
| J5 | PASS | Supported: early approval, ordinary member, wrong Area and wrong Tenant requests fail without activation/placement; #30,36,109–110. Existing business-error HTTP 500 responses are denial evidence, not successful mutations. |
| J6 | PASS | Supported: browser approvals and fresh member view, exact retry, seven Active records each with one placement/approval/attribution/confirmation and actor/time; #32,54,62,74,82,89,101,119. Current walkthrough does not independently establish every crash/concurrency scenario. |
| P1 | PASS | Supported: actual empty sponsor branch before six browser joining/approval flows; paths `1111`–`1115`, then `11111`; original sponsor retained despite changed placement parent; #33,54,62,74,82,89,101,116. |
| P2 | PASS | Supported: six recruits versus Level 1 complete and Level 2 1/25; new placements excluded from old closed ledger/evidence; #90,102,116. |
| S1 | PASS | Supported after explicit list follow-up: own Area filtering, cross-Tenant list denial, target/detail/write denials, explicit central scope and financial separation, Host compatibility; #46–53,104–105,118,126. |
| S2 | PASS | Supported after fresh full-procedure retest: reservations precede confirmation, 5/5/5 Met, immutable audit/reference, refresh/exact retry and single allocation; #63–64,111–112,127–128. Original root reservation omission is preserved rather than rewritten. |
| S3 | PASS | Supported: draft corrected to supported 5/5/4, NotMet, final conflict refused, 15/0/0 evaluator NotMet; #66–69 and runtime evaluator result. |
| S4 | PASS | Supported: authorized other-Area admin rejects its own case with reason/actor/time, null authoritative quantities/result, conflicting confirmation denied; #71–72. |
| S5 | PASS | Supported within stated limit: Held evidence blocks calculation, older case retains original week, manual reason/custody recorded; #58–61. No completed correction/appeal policy or late-cycle recalculation is established. |
| S6 | PASS | Supported as a **manual operator procedure**: reserve/finalize 5/5/4, refuse renamed duplicate across members/weeks, retain Held application decisions; #66,68,106–107,122–125 and manual register. No automated global sale reuse detector or multi-human writer coordination is proved. |
| C1 | PASS | Supported: browser Tenant calculation, R400=R150+R250, separate R150, rule versions, retry zero new rows and member no-payout label; #78,80–85,88,90–91. Closed-cutoff fixtures are distinct from newly joined members. |
| C2 | PASS | Supported: positive R150 with persisted Held enum, original overdue-at-cutoff obligation, reason, null release/payment dates and browser On hold; #120–121. R400 Earned/unreleased is correctly distinguished. |
| C3 | PASS | Supported: otherwise financially permissioned actor denied both backend mutations for both positive records, no release/payment changes; #93–98, runtime counts, explicit grants at #129. Hidden buttons alone are not the asserted safeguard. |

## Independent findings and corrections

Two evidence/procedure gaps were identified during this review and closed by **new bounded execution**, without changing implementation:

1. **S1 list-access summary lacked explicit negative list evidence.** Initial retained summaries established targets/writes/details, while the report also claimed scoped lists. New #126 explicitly records Tenant 2 `GetAll` denial, own-Area filtering and authorized central Tenant 2 listing.
2. **Original S2 root 5/5/5 had no contemporaneous sale-line reservation record.** It established application confirmation but not the complete manual procedure. Agent 1 recorded this omission and used a different existing synthetic member: #127 reserves unique supported lines before #128's browser confirmation/retry and final allocation. This does not retrospectively establish the original root's reservation.

The latter retest occurred after the closed ledger was calculated. It is a separately identified application/procedure demonstration, **not** evidence that the original R400 was backed by that later sale or that a closed commission cycle was reopened. #129 records all 37 ledger rows unchanged in the compared immutable/status/payment fields. The application can retain later reviews without changing that ledger; owner authority for handling late corrections remains unresolved and must not be inferred from this test.

Independently inspected the two frontend fixes and regression definitions: POST plus disabled pre-hydration login submission addresses the original native-GET failure; removing the prematurely set target latch permits Strict Mode's replacement effect while retaining cancellation. Both changes are narrow and do not alter authorization or financial rules. The retained Vitest log shows **21/21** passes. Type/lint success is in the auxiliary record; three no-JavaScript checks are expressly attributed to Agent 1's tool stdout, with no retained separate execution log. No rerun is claimed here.

Sales service source continues to enforce dedicated review permission, same-Tenant participation/customer matching, active Area assignments, and Host `AllTenants` grants. Mutation authorization is rechecked under existing locks. The financial service keeps the demo release/mark-paid denial. No demonstrated critical bypass or arithmetic defect emerged in this review.

## Remaining limits and owner decisions

Manual evidence supports synthetic administrative quantities, not independently verified retail commerce. Alias search, unit reservation, one-writer serialization, missing-evidence reasons and late-case custody are companion-register controls; application enforcement covers authorized access, final decisions, fixed thresholds, audit and held/unreleased states. Owner decisions remain for actual register custody, access, evidence standards, retention and exception handling before human/real-money operation. No commercial rule was confirmed by this reviewer.

The remaining clarity debt—“verified quantities” for manual review and the missing-participation message mentioning placement before checkout—is disclosed. Supported Node/runtime and production-build qualification, provider acceptance and real operational legal/commercial review remain separate limits. There were no human participants: the original checklist's human observation fields correctly remain NOT RUN.

Scriptural principles require truthful measures, examination and accountable stewardship; they are distinct from the source and execution evidence. This independent review changes only this document. No implementation, earlier Agent 2 documents, production services, deployments, Git staging/publication or business decisions were changed. **Engineering evidence: TESTED in the disclosed isolated runtime; final owner acceptance remains outstanding.**
