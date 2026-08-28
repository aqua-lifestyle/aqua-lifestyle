# Execution plans and worklogs

Execution plans are durable, versioned memory for long-running repository work. They
are useful when work spans sessions or agents, has multiple dependent stages, or
needs a resumable evidence trail. They are not required for a trivial edit, a short
answer, or a task that can be completed and verified in one straightforward session.

Every plan must begin with:

> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

Use `active/` while work is ongoing and move the file to `completed/` when the goal,
validation, and handoff are complete. Use a stable descriptive filename; do not copy
conversation transcripts or sensitive data into it.

Start from [TEMPLATE.md](TEMPLATE.md). Each plan records:

- Goal
- Authoritative references
- Confirmed decisions
- Assumptions
- Current state
- Evidence
- Open questions
- Completed work
- Next action
- Git/branch context

`Confirmed decisions` means already-authorized decisions cited from their actual
authority. The plan cannot create confirmation. Label uncertain material as an
assumption or open question and use `PROPOSED` for recommendations.

To resume safely, read the applicable `AGENTS.md`, then the plan, cited authorities,
current Git/worktree state, and current diff. Revalidate stale or carried-forward
evidence before relying on it. Update the plan with material progress and the next
action, not a turn-by-turn diary.
