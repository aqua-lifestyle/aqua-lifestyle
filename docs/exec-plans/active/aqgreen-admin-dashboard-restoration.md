# Existing administration dashboard restoration

Workstream state: TESTED. This is an execution record, not business authority.

Owner direction: remove the temporary recovery workspace, discovery page, B6 proxy
and their fixtures; preserve login availability; restore the original administration
overview in verified recovery; integrate through PR #101 and publish one frontend
release. Render automatic deployment remains off. B6 discovery is excluded.

## Scope

- Baseline backend/main: `d7947fe6074cbc7e7fa08cdc010a6e13c1aeb16f`.
- Login correction preserved from `3d03bb77bcf4183d16302cba02f4c3bcd708bb1b`.
- Temporary interface from `0d50bfc829182410ea992581a8b6ab89dcb1d9cd` removed;
  previous publication evidence remains in restricted custody outside Git.
- Existing backend proxy forwards only the explicit recovery header. The existing
  capability hook admits recovery only on HTTP 503 plus that backend header.
- Only the exact administrator dashboard renders its existing layout/guard/sidebar
  and overview during recovery. Management destinations remain disabled;
  operational metrics and financial summaries remain unavailable, without zeros.
- Existing health GET and authentication remain usable. No discovery proxy or
  new dashboard, backend feature, credential change, or business mutation.

## Validation and release

Run the frontend suite, lint, types, dependency audit, production build and focused
browser journeys for original dashboard rendering, session reload, normal-mode
compatibility, anonymous/non-admin denial and backend suspension. Verify source,
production alias and platform isolation before publication and after promotion.

An authenticated synthetic browser proves the implementation journey. The owner's
production browser is not connected to the agent; report that observation as
unavailable unless the owner provides it. No production credentials or cookies
are requested, recovered or fabricated for testing.

PR #101 must describe this final changeset, with reviewed source and green CI before
merge. No release tags, Vercel Git reconnection, Render restart/auto-deploy enablement,
B6 execution or D10 commissioning. Keep publication receipts outside Git.

Local results: 551/551 frontend unit/component cases passed, zero skipped;
8/8 desktop/mobile browser journeys passed with `MOCK_APPLICATION_RECOVERY=1`.
Run the recovery journeys with:
`MOCK_APPLICATION_RECOVERY=1 npx playwright test e2e/admin-dashboard-recovery.spec.ts`.
The fixture-dependent file is excluded when that recovery fixture is not selected,
matching the existing prospective-pilot test convention. Recovery validation did
not skip any required case. Production build and TypeScript passed; lint has one
pre-existing invitation-navigation warning; production dependency audit is clear.

Review: no backend changes or authorization bypass; exact dashboard route only;
503 without the authoritative recovery header remains unknown/closed; original
AdminGuard remains active; permission-based management labels are retained but
non-interactive in recovery; no operational provider reads or cached financial
metrics are presented. Discarded routes return 404. Generic unknown-page hydration
is outside the tested administrator workflow; that page is not used as a workaround.
Production publication and owner-session observation are recorded separately.
