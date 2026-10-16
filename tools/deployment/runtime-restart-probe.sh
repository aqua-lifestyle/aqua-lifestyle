#!/usr/bin/env bash
# Actual images + actual application schema, entirely disposable and network-isolated.
# No target URL, existing DB, provider credential, backup or configuration is accepted.
set -euo pipefail
export LC_ALL=C
[[ $# == 0 ]] || { echo 'FAIL: runtime probe accepts no arguments' >&2; exit 1; }
umask 077
probe_dir=$(mktemp -d "${TMPDIR:-/tmp}/aqua-runtime-authority.XXXXXXXX")
probe_suffix=${probe_dir##*.}
probe_endpoint=${DOCKER_HOST:-$(docker context inspect --format '{{.Endpoints.docker.Host}}')}
[[ $probe_endpoint == unix://* ]] || { echo 'FAIL: local Unix Docker socket required' >&2; rmdir "$probe_dir"; exit 1; }
probe_docker=(docker --host "$probe_endpoint")
probe_network="aqua-runtime-${probe_suffix}"
probe_pg="aqua-runtime-pg-${probe_suffix}"
probe_redis="aqua-runtime-redis-${probe_suffix}"
probe_api="aqua-runtime-api-${probe_suffix}"
probe_migration="aqua-runtime-migration-${probe_suffix}"
probe_phase=initialization
probe_checks=0
cleanup() {
  local result=$?
  if [[ $result != 0 ]]; then
    printf 'FAIL: isolated runtime qualification phase=%s\n' "$probe_phase" >&2
    # Raw process/configuration logs remain private; never dump env files or secrets.
    "${probe_docker[@]}" logs "$probe_api" >"$probe_dir/api-private.log" 2>&1 || true
    # Extract only fixed diagnostic categories, never exception/configuration text.
    for probe_code in 'permission denied' 'Read-only file system' 'Production configuration is incomplete' \
      'Database release precondition failed' 'Bird__DeliveryNotBeforeUtc' 'Exception' 'not configured'; do
      if grep -Fq "$probe_code" "$probe_dir/api-private.log"; then printf 'Diagnostic category: %s\n' "$probe_code" >&2; fi
    done
  fi
  for probe_container in "$probe_api" "$probe_migration" "$probe_redis" "$probe_pg"; do
    "${probe_docker[@]}" rm -f "$probe_container" >/dev/null 2>&1 || true
  done
  "${probe_docker[@]}" network rm "$probe_network" >/dev/null 2>&1 || true
  rm -rf -- "$probe_dir"
  exit "$result"
}
trap cleanup EXIT
probe_api_image=$("${probe_docker[@]}" image inspect aqualifestyle-runtime-api:qualification --format '{{.Id}}')
probe_migrator_image=$("${probe_docker[@]}" image inspect aqualifestyle-runtime-migrator:qualification --format '{{.Id}}')
probe_pg_image=$("${probe_docker[@]}" image inspect postgres:18-alpine --format '{{.Id}}')
probe_redis_image=$("${probe_docker[@]}" image inspect redis:7-alpine --format '{{.Id}}')
passed() { probe_checks=$((probe_checks + 1)); printf 'PASS: %s\n' "$1"; }
admin_sql() { "${probe_docker[@]}" exec -i "$probe_pg" psql -X -At -v ON_ERROR_STOP=1 -v VERBOSITY=verbose -U postgres -d aqua_runtime; }
api_sql() { "${probe_docker[@]}" exec -i --env "PGPASSWORD=$probe_api_password" "$probe_pg" psql -X -At -v ON_ERROR_STOP=1 -v VERBOSITY=verbose -h 127.0.0.1 -U aqua_runtime_api -d aqua_runtime; }
denied() {
  if printf '%s\n' "$1" | api_sql >"$probe_dir/denied.log" 2>&1; then echo "FAIL: forbidden operation succeeded: $2" >&2; exit 1; fi
  grep -q '42501' "$probe_dir/denied.log" || { echo 'FAIL: denial was not insufficient_privilege SQLSTATE42501' >&2; exit 1; }
  passed "$2"
}
fingerprint() {
  admin_sql <<'SQL'
SELECT format('SELECT %L || '':'' || md5(COALESCE(string_agg(h, '','' ORDER BY h),'''')) FROM (SELECT md5(row_to_json(t)::text) h FROM %I t) rows;', table_name, table_name)
FROM information_schema.tables WHERE table_schema='public' AND table_type='BASE TABLE'
AND table_name NOT IN ('DataProtectionKeys','AbpAuditLogs','AbpUserLoginAttempts') ORDER BY table_name
\gexec
SQL
}
probe_phase=isolated_services
# Compose interpolates even inactive profiles. Ordinary API configuration must
# not load a migration identity, including when it is entirely absent from env.
probe_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)
env -i PATH="$PATH" POSTGRES_PASSWORD=synthetic-only API_DATABASE_CONNECTION_STRING=synthetic-api-only \
  JWT_SECURITY_KEY=synthetic-only DATA_PROTECTION_CERTIFICATE_BASE64=synthetic-only \
  DATA_PROTECTION_CERTIFICATE_PASSWORD=synthetic-only YOCO_SECRET_KEY=synthetic-only NEXTAUTH_SECRET=synthetic-only \
  docker --host "$probe_endpoint" compose --env-file /dev/null -f "$probe_root/docker-compose.yml" config --services >"$probe_dir/compose-services"
if grep -qx migrate "$probe_dir/compose-services"; then echo 'FAIL: ordinary Compose includes maintenance' >&2; exit 1; fi
passed 'ordinary API configuration neither includes maintenance nor requires migration credentials'
"${probe_docker[@]}" network create --internal "$probe_network" >/dev/null
[[ $("${probe_docker[@]}" network inspect "$probe_network" --format '{{.Internal}}') == true ]]
probe_owner_password=$(openssl rand -hex 24)
probe_api_password=$(openssl rand -hex 24)
printf 'POSTGRES_DB=aqua_runtime\nPOSTGRES_PASSWORD=%s\nPOSTGRES_HOST_AUTH_METHOD=scram-sha-256\nPGDATA=/var/lib/postgresql/18/docker\n' "$probe_owner_password" >"$probe_dir/pg.env"
"${probe_docker[@]}" run -d --name "$probe_pg" --network "$probe_network" --read-only \
  --tmpfs /var/lib/postgresql:rw,nosuid,nodev,mode=1777 --tmpfs /var/run/postgresql:rw,nosuid,nodev,mode=1777 \
  --tmpfs /tmp:rw,nosuid,nodev --env-file "$probe_dir/pg.env" "$probe_pg_image" >/dev/null
"${probe_docker[@]}" run -d --name "$probe_redis" --network "$probe_network" --read-only \
  --tmpfs /data:rw,nosuid,nodev "$probe_redis_image" redis-server --save '' --appendonly no >/dev/null
probe_ready=false
for ((probe_poll=0; probe_poll<60; probe_poll++)); do
  if "${probe_docker[@]}" exec "$probe_pg" sh -c 'test "$(cat /proc/1/comm)" = postgres && pg_isready -U postgres -d aqua_runtime' >/dev/null 2>&1; then probe_ready=true; break; fi
  sleep 1
done
[[ $probe_ready == true ]]
passed 'fresh PostgreSQL18/Redis run on an internal network with no provider access or production target'

probe_phase=separate_migration
printf 'ASPNETCORE_ENVIRONMENT=Production\nConnectionStrings__Default=Host=%s;Database=aqua_runtime;Username=postgres;Password=%s\nAQUA_INITIAL_ADMIN_PASSWORD=SyntheticRuntimeFixture1!\nSEED_DEMO_DATA=false\n' "$probe_pg" "$probe_owner_password" >"$probe_dir/migrator.env"
"${probe_docker[@]}" run --name "$probe_migration" --network "$probe_network" --env-file "$probe_dir/migrator.env" \
  "$probe_migrator_image" -q >"$probe_dir/migration-private.log" 2>&1
passed 'separate maintenance image initializes only the disposable application schema with its own identity'
probe_phase=runtime_grants
printf "CREATE ROLE aqua_runtime_api LOGIN PASSWORD '%s' NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOBYPASSRLS;\n" "$probe_api_password" | admin_sql >/dev/null
admin_sql >/dev/null <<'SQL'
REVOKE CREATE,TEMPORARY ON DATABASE aqua_runtime FROM PUBLIC;
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
GRANT CONNECT ON DATABASE aqua_runtime TO aqua_runtime_api;
GRANT USAGE ON SCHEMA public TO aqua_runtime_api;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO aqua_runtime_api;
GRANT INSERT,UPDATE,DELETE ON "AbpAuditLogs","AbpUserLoginAttempts","DataProtectionKeys" TO aqua_runtime_api;
GRANT INSERT ON "TransactionalEmailOutboxMessages" TO aqua_runtime_api;
GRANT USAGE,SELECT ON ALL SEQUENCES IN SCHEMA public TO aqua_runtime_api;
INSERT INTO "TransactionalEmailOutboxMessages"
("Id","TenantId","NotificationType","IdempotencyKey","Recipient","Subject","HtmlBody","TextBody","Status","AttemptCount","NextAttemptAt","CreationTime","IsDeleted")
VALUES ('11111111-1111-1111-1111-111111111111',NULL,'Synthetic','runtime-held','synthetic@example.test','Synthetic','held-body','held-body',0,0,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,FALSE);
SQL
[[ $(printf 'SELECT session_user;\n' | api_sql) == aqua_runtime_api ]]
passed 'ordinary API uses a distinct authenticated, non-owner database identity'
denied 'CREATE TABLE public.unauthorized(id int);' 'API role cannot create schema objects'
denied 'ALTER TABLE "Customers" ADD COLUMN unauthorized int;' 'API role cannot perform migration DDL'
denied 'CREATE ROLE unauthorized;' 'API role cannot create database identities'
denied 'SET ROLE postgres;' 'API role cannot assume migration/owner identity'
denied 'DELETE FROM "__EFMigrationsHistory";' 'API role cannot rewrite migration history'
denied 'DELETE FROM "MemberPayments";' 'API role cannot mutate payment records'
denied 'DELETE FROM "EntryWeeklyCommissions";' 'API role cannot mutate AQGreen commission records'
denied 'UPDATE "AbpUsers" SET "IsActive"="IsActive";' 'API role cannot mutate administrator identities in this fixture'
denied 'UPDATE "TransactionalEmailOutboxMessages" SET "AttemptCount"="AttemptCount"+1;' 'enqueue-only API role cannot claim outbox delivery'
fingerprint >"$probe_dir/before"

probe_phase=api_configuration
openssl req -x509 -newkey rsa:2048 -nodes -keyout "$probe_dir/key.pem" -out "$probe_dir/cert.pem" \
  -days 1 -subj '/CN=Aqua isolated runtime' >"$probe_dir/cert-private.log" 2>&1
openssl pkcs12 -export -inkey "$probe_dir/key.pem" -in "$probe_dir/cert.pem" -out "$probe_dir/cert.pfx" \
  -passout pass:synthetic-only >>"$probe_dir/cert-private.log" 2>&1
cat >"$probe_dir/api.env" <<EOF
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__Default=Host=$probe_pg;Database=aqua_runtime;Username=aqua_runtime_api;Password=$probe_api_password
Redis__Configuration=$probe_redis:6379
App__Recovery__Enabled=false
App__BackgroundJobs__Enabled=false
App__FinancialOperations__Enabled=false
App__WeeklyCommissions__Enabled=false
App__EntryMonthlyObligations__Enabled=false
AQGreenV2Demo__Enabled=false
AQGreenV2Pilot__Enabled=false
App__AQGreenV2__Commissioning__Enabled=false
SEED_DEMO_DATA=false
App__ServerRootAddress=https://api.example.test
App__ClientRootAddress=https://app.example.test
App__CorsOrigins=https://app.example.test
Authentication__JwtBearer__SecurityKey=synthetic-runtime-key-with-at-least-sixty-four-characters-never-use-live
DataProtection__CertificatePassword=synthetic-only
Bird__Enabled=true
Bird__ApiKey=bk_eu1_synthetic-no-provider-access
Bird__FromEmail=synthetic@example.test
Bird__FromName=Synthetic
Bird__ReplyToEmail=synthetic@example.test
Bird__DeliveryNotBeforeUtc=1970-01-01T00:00:00Z
Yoco__Mode=test
Yoco__SecretKey=sk_test_synthetic-no-provider-access
Yoco__WebhookSecret=whsec_c3ludGhldGljLW9ubHktdGhpcnR5LXR3by1ieXRlcyE=
EOF
printf 'DataProtection__CertificateBase64=%s\n' "$(base64 -w0 "$probe_dir/cert.pfx")" >>"$probe_dir/api.env"
start_api() {
  "${probe_docker[@]}" run -d --name "$probe_api" --network "$probe_network" --read-only \
    --tmpfs /tmp:rw,nosuid,nodev --cap-drop ALL --security-opt no-new-privileges \
    --env-file "$probe_dir/api.env" "$probe_api_image" >/dev/null
}
wait_healthy() {
  local ready=false
  for ((probe_poll=0; probe_poll<60; probe_poll++)); do
    if "${probe_docker[@]}" exec "$probe_api" dotnet AqualLifeStyle.Web.Host.dll --health-check >/dev/null 2>&1; then ready=true; break; fi
    [[ $("${probe_docker[@]}" inspect "$probe_api" --format '{{.State.Running}}') == true ]] || break
    sleep 1
  done
  [[ $ready == true ]]
}
check_held_state() {
  # Cross the outbox worker's30s interval. Fast startup alone is insufficient proof.
  sleep 32
  fingerprint >"$probe_dir/after"
  cmp "$probe_dir/before" "$probe_dir/after"
  [[ $(printf 'SELECT "Status" || '"'"':'"'"' || "AttemptCount" FROM "TransactionalEmailOutboxMessages" WHERE "IdempotencyKey"='"'"'runtime-held'"'"';\n' | admin_sql) == 0:0 ]]
}
probe_phase=first_start
start_api
wait_healthy
"${probe_docker[@]}" exec "$probe_api" sh -c 'test ! -e /app/AqualLifeStyle.Migrator.dll && test ! -d /app/migrator'
check_held_state
passed 'normal Production API starts without recovery and preserves held outbox/business state beyond the dispatch interval'
probe_phase=container_restart
"${probe_docker[@]}" restart "$probe_api" >/dev/null
wait_healthy
check_held_state
passed 'actual container restart grants no financial, seeding, repair or delivery authority'
probe_phase=repeated_deployment
"${probe_docker[@]}" rm -f "$probe_api" >/dev/null
start_api
wait_healthy
check_held_state
passed 'fresh container from the same API image preserves the same restricted runtime authority'
"${probe_docker[@]}" rm -f "$probe_api" >/dev/null
probe_phase=missing_required_configuration
sed '/^Bird__DeliveryNotBeforeUtc=/d' "$probe_dir/api.env" >"$probe_dir/held.env"
mv "$probe_dir/held.env" "$probe_dir/api.env"
start_api
for ((probe_poll=0; probe_poll<45; probe_poll++)); do
  [[ $("${probe_docker[@]}" inspect "$probe_api" --format '{{.State.Running}}') == true ]] || break
  sleep 1
done
[[ $("${probe_docker[@]}" inspect "$probe_api" --format '{{.State.Running}}') == false ]]
probe_exit=$("${probe_docker[@]}" wait "$probe_api")
[[ $probe_exit == 1 ]]
fingerprint >"$probe_dir/after"
cmp "$probe_dir/before" "$probe_dir/after"
passed 'missing required delivery configuration stops startup without business writes'
printf 'PASS: %s isolated runtime checks; production privilege isolation=UNVERIFIED; no live deployment\n' "$probe_checks"
