# Production privilege isolation evidence

Starting source:72e09f6ed992c5608e6106d4368801cd292d2785. Current assessment is
BLOCKED, not production verified. See the [write contract and persistent requirements](../../operations/production-privilege-isolation.md).

`render-readonly.json` retains only whitelisted GET status, selected target IDs,
configuration flags, credential presence, user-match boolean, managed-credential
count and allowlist shape. No secret values, database URLs, usernames, addresses,
private IP allowlist entries or raw provider payloads are retained. A provider-managed
user inventory does not enumerate PostgreSQL-created roles or their grants.

`catalog-readonly.json` records the bounded failure using the existing API credential
and authoritative external endpoint. TLS verify-full remained required. psql exit2 /
SSL connection closed unexpectedly returned no catalog facts. Three attempts were
bounded; no provider/network change or new production execution route followed. The
observed allowlist is not proof of the failure's cause. No application data SELECT,
DDL, DML, credential change, deployment or restricted operation was attempted live.

The repository catalog SQL was expanded after that failed transport attempt to include
column-only grants and other user schemas/definers; it is exercised on disposable
PostgreSQL18. Run it only through an existing approved catalog observation session:

```bash
PGOPTIONS='-c default_transaction_read_only=on -c statement_timeout=8000 -c lock_timeout=1000 -c idle_in_transaction_session_timeout=15000' \
PGSSLMODE=verify-full \
psql -X -qAt --no-password --set=ON_ERROR_STOP=1 \
  --file tools/operations/database-authority-readonly.sql
```

Credentials/target must come from the custodian's existing approved private
configuration, never pasted into command arguments, logs, source or a report. This
command does not provision access. The query reports metadata, not an independent
attestation, and cannot establish external IAM/egress, secret custody or every
privileged-function/extension route by itself.

Agent2 reproduced a branch-introduced audit defect at80cdc591: inherited
`search_path=public,pg_catalog` selected a synthetic row-reading
`public.current_setting(text)` function, leaking its marker and spoofing the audit.
The correction sets `search_path=pg_catalog,pg_temp` locally before inspection.
The existing owner/API catalog check now proves raw lookup is vulnerable in the
hostile fixture and that the actual corrected audit ignores it. Role configuration,
function and synthetic table are removed before the remaining runtime checks.

The extended internal-network runtime probe must execute23 checks, including six
new catalog/unsafe-policy rejection checks. Profile compatibility is direct SQL,
not an HTTP customer journey. Invalid synthetic Host password mutation is not proof
of successful administrator login. Outbox Sent insertion is a forged fixture row,
not a real provider receipt/delivery. All writes roll back; temporary grants are
revoked and the original business fingerprint must match before API startup.

First execution passed new counterexamples but failed later after a script edit while
Bash was reading it; it is incomplete and not counted as23 passes. The frozen current-source rerun passed all23 checks; its safe
[console evidence](disposable-runtime-probe.txt) is retained. The actual API and
maintenance images are carried forward from unchanged72e09f6 runtime source; final
CI rebuilds both from its exact commit. Mandatory209 cases and all retained mutation
negatives passed locally. Final exact-source hosted evidence is pending. Full original security,
application/member/Area PostgreSQL and container CI remain required. Final counts,
source/run/attempt and Agent2 review will be recorded on PR106 after publication.

The earlier72e09f6 evidence (209security,17runtime,156localfocused,78dedicatedPG and
both17-job complete CI runs) is carried forward for unchanged application behavior,
not relabeled as execution of this task's new audit/counterexamples. The previous
readiness-only grants are explicitly not a production API role recipe.
