#!/usr/bin/env bash
# Synthetic PostgreSQL evidence only. No connection URL, external backup, secret,
# existing container or production target can be supplied to this fixture.
set -euo pipefail
export LC_ALL=C
[[ $# == 0 ]] || { echo 'FAIL: this fixture takes no target or backup arguments' >&2; exit 1; }
umask 077
fixture_dir=$(mktemp -d "${TMPDIR:-/tmp}/aqua-disposable-recovery.XXXXXXXX")
fixture_id=''
fixture_endpoint=${DOCKER_HOST:-$(docker context inspect --format '{{.Endpoints.docker.Host}}')}
[[ $fixture_endpoint == unix://* ]] || { echo 'FAIL: local Unix Docker socket required' >&2; rmdir "$fixture_dir"; exit 1; }
fixture_docker=(docker --host "$fixture_endpoint")
cleanup() {
  local result=$?
  if [[ -n $fixture_id ]]; then "${fixture_docker[@]}" rm -f "$fixture_id" >/dev/null 2>&1 || result=1; fi
  rm -rf -- "$fixture_dir"
  exit "$result"
}
trap cleanup EXIT
fixture_image=$("${fixture_docker[@]}" image inspect postgres:16-alpine --format '{{.Id}}')
fixture_id=$("${fixture_docker[@]}" run --detach --rm --network none --read-only \
  --user postgres --cap-drop ALL --security-opt no-new-privileges \
  --pids-limit 128 --memory 384m --cpus 1 \
  --tmpfs /var/lib/postgresql:rw,nosuid,nodev,mode=1777 \
  --tmpfs /var/run/postgresql:rw,nosuid,nodev,mode=1777 \
  --tmpfs /tmp:rw,nosuid,nodev,mode=1777 \
  --env PGDATA=/var/lib/postgresql/data --env POSTGRES_HOST_AUTH_METHOD=trust "$fixture_image")
[[ $fixture_id =~ ^[a-f0-9]{64}$ ]] || { echo 'FAIL: fixture container identity unavailable' >&2; exit 1; }
fixture_ready=false
for ((fixture_poll=0; fixture_poll<60; fixture_poll++)); do
  # The entrypoint's temporary init server must not qualify final readiness.
  if "${fixture_docker[@]}" exec "$fixture_id" sh -c 'test "$(cat /proc/1/comm)" = postgres && pg_isready -U postgres' >/dev/null 2>&1; then fixture_ready=true; break; fi
  sleep 1
done
[[ $fixture_ready == true ]] || { echo 'FAIL: disposable PostgreSQL did not become ready' >&2; exit 1; }
sql() { "${fixture_docker[@]}" exec -i "$fixture_id" psql -X -At -v ON_ERROR_STOP=1 -v VERBOSITY=verbose -U postgres -d "$1"; }
app_sql() { "${fixture_docker[@]}" exec -i "$fixture_id" psql -X -At -v ON_ERROR_STOP=1 -v VERBOSITY=verbose -U fixture_app -d "$1"; }
fixture_checks=0
passed() { fixture_checks=$((fixture_checks + 1)); printf 'PASS: %s\n' "$1"; }
denied() {
  local database=$1 statement=$2 label=$3
  if printf '%s\n' "$statement" | app_sql "$database" >"$fixture_dir/denial.log" 2>&1; then
    printf 'FAIL: forbidden fixture operation succeeded: %s\n' "$label" >&2; exit 1
  fi
  # A syntax/connection failure is not evidence that permissions stopped it.
  grep -Eq '42501' "$fixture_dir/denial.log" || { echo 'FAIL: expected insufficient_privilege SQLSTATE' >&2; exit 1; }
  passed "$label"
}
sql postgres >/dev/null <<'SQL'
CREATE ROLE fixture_app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOBYPASSRLS;
CREATE ROLE fixture_migrator NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOBYPASSRLS;
CREATE DATABASE fixture_source TEMPLATE template0;
CREATE DATABASE fixture_restored TEMPLATE template0;
CREATE DATABASE fixture_conflict TEMPLATE template0;
CREATE DATABASE fixture_corrupt TEMPLATE template0;
SQL
sql fixture_source >/dev/null <<'SQL'
REVOKE ALL ON DATABASE fixture_source FROM PUBLIC;
GRANT CONNECT ON DATABASE fixture_source TO fixture_app;
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
CREATE TABLE tenants(id integer PRIMARY KEY);
CREATE TABLE areas(tenant_id integer REFERENCES tenants, id integer, PRIMARY KEY(tenant_id,id));
CREATE TABLE members(id integer PRIMARY KEY, tenant_id integer, area_id integer, note text NOT NULL,
 FOREIGN KEY(tenant_id,area_id) REFERENCES areas(tenant_id,id));
CREATE TABLE restricted_state(id integer PRIMARY KEY, evidence text NOT NULL);
CREATE TABLE fixture_schema_history(version integer PRIMARY KEY);
INSERT INTO tenants VALUES(1),(2);
INSERT INTO areas VALUES(1,10),(1,20),(2,30);
INSERT INTO members VALUES(1,1,10,'synthetic-a'),(2,1,20,'synthetic-b'),(3,2,30,'synthetic-c');
INSERT INTO restricted_state VALUES(1,'synthetic-privileged-evidence');
INSERT INTO fixture_schema_history VALUES(1);
GRANT SELECT,UPDATE(note) ON members TO fixture_app;
SQL
[[ $(printf 'SELECT session_user;\n' | app_sql fixture_source) == fixture_app ]]
[[ $(printf 'SELECT count(*) FROM members;\n' | app_sql fixture_source) == 3 ]]
printf "UPDATE members SET note=note WHERE id=1;\n" | app_sql fixture_source >/dev/null
passed 'separate LOGIN identity performs only explicitly granted fixture DML'

for fixture_db in fixture_source; do
  denied "$fixture_db" 'CREATE SCHEMA unauthorized;' 'runtime identity cannot create schema'
  denied "$fixture_db" 'CREATE ROLE unauthorized;' 'runtime identity cannot create privileged identities'
  denied "$fixture_db" 'ALTER ROLE fixture_app SUPERUSER;' 'runtime identity cannot become superuser'
  denied "$fixture_db" 'SET ROLE fixture_migrator;' 'runtime identity cannot assume migration role'
  denied "$fixture_db" 'SET ROLE pg_read_all_data;' 'runtime identity cannot assume broad data role'
  denied "$fixture_db" 'DROP TABLE members;' 'runtime identity cannot perform owner DDL'
  denied "$fixture_db" 'SELECT * FROM restricted_state;' 'runtime identity cannot read privileged state'
  denied "$fixture_db" "UPDATE restricted_state SET evidence='forbidden';" 'runtime identity cannot mutate privileged state'
done

fixture_snapshot="SELECT jsonb_build_object('tenants',(SELECT jsonb_agg(t ORDER BY id) FROM tenants t),'areas',(SELECT jsonb_agg(t ORDER BY tenant_id,id) FROM areas t),'members',(SELECT jsonb_agg(t ORDER BY id) FROM members t),'restricted',(SELECT jsonb_agg(t ORDER BY id) FROM restricted_state t),'schema',(SELECT jsonb_agg(t ORDER BY version) FROM fixture_schema_history t))::text;"
printf '%s\n' "$fixture_snapshot" | sql fixture_source > "$fixture_dir/expected.json"
"${fixture_docker[@]}" exec "$fixture_id" pg_dump -U postgres -Fc -d fixture_source > "$fixture_dir/backup.dump"
printf "UPDATE members SET note='synthetic-after-backup' WHERE id=1;\n" | sql fixture_source >/dev/null
"${fixture_docker[@]}" exec -i "$fixture_id" pg_restore -U postgres --single-transaction --exit-on-error -d fixture_restored < "$fixture_dir/backup.dump"
printf '%s\n' "$fixture_snapshot" | sql fixture_restored > "$fixture_dir/restored.json"
cmp "$fixture_dir/expected.json" "$fixture_dir/restored.json"
printf '%s\n' "$fixture_snapshot" | sql fixture_source > "$fixture_dir/changed.json"
if cmp -s "$fixture_dir/expected.json" "$fixture_dir/changed.json"; then echo 'FAIL: snapshot sentinel was ineffective' >&2; exit 1; fi
passed 'custom-format backup restores the exact earlier synthetic data/schema-history snapshot'
[[ $(printf 'SELECT count(*) FROM members;\n' | app_sql fixture_restored) == 3 ]]
denied fixture_restored 'DROP TABLE members;' 'restored ownership does not grant runtime DDL'
denied fixture_restored 'SELECT * FROM restricted_state;' 'restored ACL denies privileged state'
# Constraint rejection is checked independently of application code.
if printf "INSERT INTO members VALUES(9,1,30,'invalid');\n" | sql fixture_restored > "$fixture_dir/constraint.log" 2>&1; then
  echo 'FAIL: restored Tenant/Area foreign key was lost' >&2; exit 1
fi
grep -Eq '23503' "$fixture_dir/constraint.log"
passed 'restored same-Tenant Area foreign key rejects a cross-Tenant reference'

printf "CREATE TABLE members(sentinel text); INSERT INTO members VALUES('preserve');\n" | sql fixture_conflict >/dev/null
if "${fixture_docker[@]}" exec -i "$fixture_id" pg_restore -U postgres --single-transaction --exit-on-error -d fixture_conflict \
    < "$fixture_dir/backup.dump" > "$fixture_dir/conflict.log" 2>&1; then
  echo 'FAIL: conflicting restore was admitted' >&2; exit 1
fi
grep -Eq 'already exists' "$fixture_dir/conflict.log"
[[ $(printf "SELECT string_agg(tablename,',' ORDER BY tablename) FROM pg_tables WHERE schemaname='public';\n" | sql fixture_conflict) == members ]]
[[ $(printf 'SELECT sentinel FROM members;\n' | sql fixture_conflict) == preserve ]]
passed 'conflicting restore fails and its transaction preserves only the pre-existing sentinel'
head -c 32 "$fixture_dir/backup.dump" > "$fixture_dir/truncated.dump"
if "${fixture_docker[@]}" exec -i "$fixture_id" pg_restore -U postgres --single-transaction --exit-on-error -d fixture_corrupt \
    < "$fixture_dir/truncated.dump" > "$fixture_dir/corrupt.log" 2>&1; then
  echo 'FAIL: truncated backup was accepted' >&2; exit 1
fi
grep -Eq 'could not read|end of file|too short|does not appear' "$fixture_dir/corrupt.log"
[[ $(printf "SELECT count(*) FROM pg_tables WHERE schemaname='public';\n" | sql fixture_corrupt) == 0 ]]
passed 'truncated archive fails without creating a partial restored schema'
printf 'PASS: %s disposable PostgreSQL checks; image=%s; production evidence=NONE\n' "$fixture_checks" "$fixture_image"
