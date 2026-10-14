> **NOT BUSINESS AUTHORITY.** This worklog records execution state and evidence. It
> cannot confirm or supersede a business-policy decision.

# Existing platform provider transition

## Goal and authority

Owner direction: restore existing services with Bird transactional email and Yoco
Test Mode. Keep commission release, historical financial processing, weekly/monthly
automation and D10 disabled. Obtain focused independent review before production
changes. No replacement dashboard or infrastructure.

## Source and implementation

Baseline main `2b3bede7aee2d402a3f653107f37d9a572a0b023`; existing API
`d7947fe6074cbc7e7fa08cdc010a6e13c1aeb16f`. Worktree branch initially
`fix/recovery-permissioned-platform-reads`, renamed to
`fix/provider-test-operational-transition`; no safe-read bypass was added.

Initial independent review rejected configuration-only activation: test callbacks
could become ordinary payments, Bird could drain historical messages, and worker
flags did not restrict manual commissions. The focused correction adds a required
Bird delivery cutoff to selection and atomic claims, validates test callbacks
without applying business payments, and explicitly holds manual commissions and
framework background jobs. Existing Tenant/Area permissions and D10 gates remain.
No migrations or new financial ledger are introduced.

## Evidence and open prerequisites

- Secured provider settings decrypted in memory successfully; existing key formats
  match Test Mode/Bird. No secrets copied into Git or reports.
- Read-only Yoco webhook listing returned HTTP 200 with one subscription matching
  the existing API endpoint in test mode and none matching it in live mode. This
  does not prove the stored signing secret matches an actual callback.
- Agent's approved configured database host is unreachable; aggregate outbox/job
  inspection requires the designated Host Admin's existing approved terminal.
  Private metadata-only SQL is retained outside Git. No hostname/permission bypass.
- Approved internal test mailbox and Yoco test account are still required. No
  messages, checkouts, production writes or configuration changes performed.
- Release solution restore/build passed. Focused regression run: 139 passed,
  zero failed/skipped, including PostgreSQL candidate/atomic-claim cutoff checks.
  Additional Onyx/monthly/bootstrap boundary coverage was added; its first run
  passed 14 cases and exposed one new monthly assertion incorrectly assuming no
  pre-existing joining payments. That assertion now compares the unchanged
  original payment evidence and separately denies any new monthly payment;
  its final two-mode execution passed (2/2, zero failed/skipped). All cases with
  earlier failures now have passing executions; these are synthetic component and
  disposable PostgreSQL results, not production provider evidence.
- Focused independent correctness review: PASS for the current transition guards.
  The reviewer identified no remaining defect in the reviewed paths, and explicitly
  distinguished that result from missing operator evidence/provider delivery.
- Blueprint YAML parsed and eight financial/job/demo/pilot/commissioning/seed holds
  checked; migrator empty, automatic deployment off, Yoco Test Mode. Effective
  production settings were not changed. Read-back confirms the existing recovery
  deployment and provider isolation, and Vercel Git remains disconnected.
- NuGet advisory review completed: pre-existing runtime/transitive and test
  advisories remain. No package/project dependencies changed; this is not a clean
  dependency audit or a new exploitability qualification. Full release CI and
  container validation remain required before source integration/deployment.

## Next action and remaining limitation

The designated Host Admin must run the private aggregate-only outbox/job SQL using
the existing approved PostgreSQL terminal and return only counts, statuses/types,
Tenant IDs and timestamp bounds. No credentials or message bodies are needed.
The owner must approve the prospective delivery cutoff and designate controlled
test targets through secure custody. Until those inputs exist, no provider
activation or recovery exit is permitted. No additional production functions have
been restored by this local work; Bird delivery and a real signed test callback
remain unverified.

## Bounded production sequence (not yet executed)

1. Obtain aggregate outbox/job inventory, approve a new UTC delivery boundary,
   confirm test targets and secure provider custody. Do not delete/relabel old rows.
2. Complete guards, validation, focused review and normal source integration with
   Render automatic deployment off and Blueprint synchronization disabled.
3. Deploy the reviewed release first with recovery still enabled, providers absent,
   no pre-deploy command, automation/jobs/commissioning off. Verify exact identity,
   health, recovery header and blocked mutations; retain this working fallback.
4. Stage normal-start configuration using Save Only: recovery false, Yoco test and
   matching test credentials/webhook, Bird sender/key and approved delivery cutoff;
   retain all financial/job/demo/pilot/D10 holds, auth, database, Redis, Data
   Protection and HTTPS. Then deploy the same reviewed release. Never restart the
   unguarded prior release with provider credentials.
5. Verify original dashboard and authorized services, one approved Bird delivery,
   one approved test transaction and signed callback with no business payment,
   no historical delivery, no financial processing or activation. Stop on failure.

Emergency stop: suspend the existing Render API. To return to recovery, restore
recovery true and remove all effective Yoco settings and Bird API key, retain all
holds, and start only the reviewed guard release. Recovery is latched per process;
changing a flag is not proof of a safe transition. Respect D10 startup failure if
uncommissioned V2 placement state exists; do not repair or fabricate authority.
