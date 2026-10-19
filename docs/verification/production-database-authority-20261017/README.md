# Production database authority evidence

Read-only provider/transport observations on2026-10-17; no successful production
catalog readback. See the [assessment and access dependency](../../operations/production-database-authority.md).

| Record | Evidence and limit |
| --- | --- |
| [Render route](render-route-readonly.json) | Authenticated GET configuration; configured username equality, not effective SQL identity |
| [TLS only](tls-only.json) | One external connection through verified TLS1.3; no credentials, startup packet or SQL |
| [Catalog attempt](catalog-attempt.json) | One existing-API-credential attempt, psql exit2/generic SSL closure; no identity/catalog result. SQL hash identifies that attempted version, not an assertion of execution |
| [SSH attempt](ssh-attempt.json) | Existing key/strict known host; fixed client-presence command did not complete, no database connection |
| [Provider diagnostics](provider-diagnostics.json) | Sampled connection counts and complete FATAL-filter searches for stated windows; no raw logs. Not proof of effective connection limits, authentication or gateway behavior |
| [Catalog probe](catalog-probe.txt) | Fresh13-check PostgreSQL18/SCRAM/network-none fixture, synthetic data only; not production custody or full API qualification |

Readback is restricted to deliberately selected metadata; no passwords, connection
strings, public/private source addresses, raw role identifiers, member rows or provider
payloads are retained. Diagnostic flags mean observed textual categories, not proof
that an unreported phase succeeded. Prior-three-attempt evidence is carried forward
at [its original source](../production-privilege-isolation-20261017/catalog-readonly.json).

Reproduce only the disposable fixture from repository root, using a local Unix Docker
socket and Node24+. It accepts no target arguments or production connection:

```bash
docker pull postgres:18-alpine
node tools/operations/database-authority-probe.mjs
```

Fresh hosted CI and exact-final Agent2 review are recorded in the PR106 report after
the final commit exists; earlier commit CI must not be relabeled as current evidence.
