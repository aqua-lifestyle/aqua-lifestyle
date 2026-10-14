# Minimum isolated hosting transition

**PREPARED ONLY — separate deployment authorization required.**

Use the accepted source-manifest
`5e133be584398b2db14a5c542e27860bd6f02adae0c66c1488ef30e7ee2573e0`.
See the [readiness report](../../aqua-system/aqgreen-v2-controlled-deployment-readiness.md)
for inspected controls, production-only blockers and evidence limits.

The fastest route uses a separate Docker host and SSH tunnel. Public HTTPS is a
separate origin correction: the accepted application requires plain HTTP loopback
client URLs. Do not change that guard or use Aqua's existing Render/Compose recipe.
No production resource, Yoco/Bird credential or member record is part of this plan.

1. **Owner approves the concrete execution scope:** selected isolated host/budget,
   private artifact transfer, disposable database/schema bootstrap, synthetic
   accounts/scopes, service start, approved viewers, stop operator and reset policy.
2. **Package the accepted source locally.** Verify the source archive and all2,105
   manifest entries. Use the carried-forward qualified offline .NET publish and
   Node24.21.0 locked production build processes. Assemble `bundle/api`,
   `bundle/migrator`, `bundle/web` (standalone, public, .next/static). Retain build
   logs and output hashes; preserve the font license. Import the qualified runtime
   base images privately. Image IDs alone do not identify application code.
3. **Prepare the separate host.** Put the bundle and this recipe under a dedicated
   directory. Copy `release.env.example` to `release.env`; choose dedicated bundle
   and private-directory paths. Generate private environment files from the three
   examples outside Git, mode0600. API/database passwords must agree. Keep the
   exact demo database/origin settings, blank provider settings and disabled
   workers. Reject remaining placeholders privately; do not print the resolved
   environment. Verify local image IDs, readable API/migrator files and web access
   for the Node user. Database volume must be new and empty.
4. **Validate/bootstrap/start — only after approval, on the isolated host:**

   ```bash
   docker compose --env-file release.env -f compose.plan.yaml config --quiet
   docker compose --env-file release.env -f compose.plan.yaml up -d postgres
   docker compose --env-file release.env -f compose.plan.yaml run --rm migrate
   ```

   Check migration success and dedicated database/schema; no imported Tenant
   connection strings. On failure, stop here. Then start:

   ```bash
   docker compose --env-file release.env -f compose.plan.yaml up -d api web
   ```

   Establish the separate synthetic Area, central and owner-review actors with
   existing fixture conventions. Central access needs explicit grants. Verify
   internal network/no external route, only loopback frontend publication, healthy
   API/key ring, disabled workers, no-network checkout adapter and demo banner.
5. **Run focused hosted acceptance before inviting viewers.** Repeat the accepted
   production smoke: browser signup/verification, invitation, full/two-instalment
   checkout, wrong amount/retry, pending/approval/placement/progress, scoped sales
   review and Held/rejected/met states, versioned system calculation, cutoff and
   duplicate protection, direct payout denials, login/session/logout, and controlled
   stop/restart persistence. Record actual actor/API/database outcomes. Carry
   forward unchanged detailed/concurrency cases; do not claim the old17 cases were
   newly rerun. No real-provider probe. Synthetic verification links stay private.
6. **Authorized viewer access:** confirm the approved host key, then use the owner-
   selected SSH alias:

   ```bash
   ssh -N -o ExitOnForwardFailure=yes -L 127.0.0.1:3000:127.0.0.1:3000 DEMO_SSH_ALIAS
   ```

   Browse `http://127.0.0.1:3000` using the acceptance browser. Before running across
   a closed-week boundary, disable only `AQGreenV2Demo__Fixture__Enabled` after the
   initial seed, or schedule an approved disposable reset. Keep Demo.Enabled true.
   Review later periods through the application; never backdate new placements.
7. **Stop/rollback:** close viewer tunnels; stop writers, then database:

   ```bash
   docker compose --env-file release.env -f compose.plan.yaml stop web api
   docker compose --env-file release.env -f compose.plan.yaml stop postgres
   ```

   Restart uses the same volume/keys/output bundle and does not run migrations.
   Keep failed evidence and synthetic volume until authorized reset. `down` without
   `-v` preserves data; no `down -v` without discard approval. For an update, keep
   prior outputs and a private restorable synthetic backup; restore to a separate
   empty isolated database if needed. Do not erase consumed history with EF Down.

This recipe passed **configuration validation only** on Docker Compose
v2.32.4-desktop.1 with placeholder files. It has not started a container, applied a
migration or proved remote host/tunnel behavior. Those outcomes must be recorded
when separately authorized execution occurs. Original acceptance and local
qualification remain distinct from hosted acceptance and production readiness.
