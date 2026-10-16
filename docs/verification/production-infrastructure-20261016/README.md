# Production infrastructure read-only evidence

Starting source: `9581655e4ba0ab22c8578a7536439eda1bbf6fbc`, PR106. Observations
2026-10-16, 18:05–18:13 UTC. This record is not deployment or policy authority.

- github-readonly.json: authenticated GETs of main, rulesets, Actions settings,
  environment/branch policy and secret inventories. No credential values returned.
- provider-readonly.json: authenticated Render/Vercel GETs and public HTTP probes,
  selected fields only. First-page deployment histories summarized, not complete.
- backup-and-public-inputs.json: export lists and public-variable decryptability.
  Empty Vercel values with decrypted=false mean unknown actual values. A false
  matchesExpected on that read is not evidence the configured value is incorrect.
  Empty export lists do not establish PITR absence or restore readiness.
- database-metadata.json: service DATABASE_URL host matched to the listed instance
  internally; no connection string/host/user retained. Provider metadata only;
  no database session, production records or role/grant query. Separate recovery GETs
  report PITR AVAILABLE, application window startsAt=2026-10-13T02:44:00Z; this is
  provider availability, not an exercised restore.
- main-ruleset.proposed.json and environment.proposed.json: **unapplied proposals**,
  not settings readbacks. Ruleset names all17 current contexts from release-control.
  Environment branch-policy creation and admin-bypass UI configuration are separate.
  These payloads alone do not establish independent control or protect secrets from
  candidate-authored workflows.

Provider inspection used existing authenticated local CLI sessions, GET only.
No token, secret value, raw environment response, export download URL, customer data,
provider setting change, deployment request or database mutation is included.
Role privileges, deploy-only credential scopes, retention guarantees, restoration,
protected external custody and production monitoring remain unverified.

See [configuration plan](../../operations/production-infrastructure-plan.md) for
current/required state, consequences, authorization, validation and recovery per action.
The previous source's complete hosted CI remains
[push38942198574](https://github.com/aqua-lifestyle/aqua-lifestyle/actions/runs/38942198574)
and [PR38942207293](https://github.com/aqua-lifestyle/aqua-lifestyle/actions/runs/38942207293),
attempt1, all17 jobs passed. Those are carried-forward evidence for unchanged code,
not final-documentation-commit CI or actual provider deployment evidence.

Fresh local execution before the documentation commit:208/208 mandatory cases passed,
zero skipped/failed; retained tampering fixture denied every negative including raw
exit0/170 passed/one skipped; disposable PostgreSQL recovery/privilege fixture15/15
passed. These tests execute unchanged controller code and isolated fixtures, not the
live providers or runtime role design. Full final-commit CI and review are reported
on PR106 after publication to avoid a self-referential commit record.
