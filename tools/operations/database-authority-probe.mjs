#!/usr/bin/env node
// Catalog-query and proposed privilege primitives only, never a production target.
import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import { randomBytes } from 'node:crypto';
import { readFileSync } from 'node:fs';

const image = 'postgres:18-alpine';
const container = `aqua-catalog-${randomBytes(8).toString('hex')}`;
const roles = ['postgres', 'fixture_api', 'fixture_maintenance', 'fixture_worker'];
const passwords = Object.fromEntries(roles.map(role => [role, randomBytes(24).toString('hex')]));
const query = readFileSync(new URL('./database-authority-readonly.sql', import.meta.url), 'utf8');
let endpoint, started = false, phase = 'local-target', checks = 0, failed = false;

function docker(args, options = {}) {
  return execFileSync('docker', ['--host', endpoint, ...args], {
    encoding: 'utf8', maxBuffer: 16 * 1024 * 1024, timeout: 20_000,
    stdio: ['pipe', 'pipe', 'pipe'], ...options,
  });
}
function sql(statement, role = 'postgres', password = passwords[role]) {
  // Docker reads these synthetic credentials from the child environment, not argv.
  return docker(['exec', '-i', '--env', 'PGPASSWORD', container, 'psql', '-X', '-qAt',
    '--no-password', '--set=ON_ERROR_STOP=1', '--set=VERBOSITY=verbose',
    '-h', '127.0.0.1', '-U', role, '-d', 'postgres'], {
    input: statement, env: { ...process.env, PGPASSWORD: password },
  });
}
function denied(statement, expected = /\b42501\b/u, role = 'fixture_api', password = passwords[role]) {
  let caught;
  try { sql(statement, role, password); } catch (error) { caught = error; }
  assert.ok(caught, 'forbidden operation succeeded');
  assert.match(String(caught.stderr), expected);
}
function check(name, verify) {
  phase = name; verify(); checks++;
  console.log(`PASS: ${name}`);
}

try {
  assert.equal(process.argv.length, 2, 'no target arguments accepted');
  endpoint = process.env.DOCKER_HOST || execFileSync('docker',
    ['context', 'inspect', '--format', '{{.Endpoints.docker.Host}}'],
    { encoding: 'utf8', timeout: 10_000, stdio: ['ignore', 'pipe', 'pipe'] }).trim();
  assert.ok(endpoint.startsWith('unix://'), 'local Unix Docker socket required');
  // Existing local image only. No published port, external network, mount or data.
  const imageId = docker(['image', 'inspect', image, '--format', '{{.Id}}']).trim();
  phase = 'isolated-postgres-startup';
  started = true;
  docker(['run', '-d', '--name', container, '--network', 'none', '--read-only',
    '--tmpfs', '/var/lib/postgresql:rw,nosuid,nodev,mode=1777',
    '--tmpfs', '/var/run/postgresql:rw,nosuid,nodev,mode=1777',
    '--tmpfs', '/tmp:rw,nosuid,nodev', '--env', 'PGDATA=/var/lib/postgresql/18/docker',
    '--env', 'POSTGRES_INITDB_ARGS=--auth-host=scram-sha-256 --auth-local=trust',
    '--env', 'POSTGRES_PASSWORD', '--env', 'POSTGRES_HOST_AUTH_METHOD=scram-sha-256', imageId],
  { env: { ...process.env, POSTGRES_PASSWORD: passwords.postgres } });
  let ready = false;
  for (let attempt = 0; attempt < 45; attempt++) {
    const result = spawnSync('docker', ['--host', endpoint, 'exec', container,
      'pg_isready', '-h', '127.0.0.1', '-U', 'postgres', '-d', 'postgres'],
    { timeout: 2_000, stdio: 'ignore' });
    if (result.status === 0) { ready = true; break; }
    await new Promise(resolve => setTimeout(resolve, 500));
  }
  assert.ok(ready, 'disposable PostgreSQL unavailable');
  phase = 'synthetic-fixture';
  sql(`
CREATE ROLE fixture_owner NOLOGIN;
CREATE ROLE fixture_api LOGIN PASSWORD '${passwords.fixture_api}' NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOBYPASSRLS;
CREATE ROLE fixture_maintenance LOGIN PASSWORD '${passwords.fixture_maintenance}' NOINHERIT;
CREATE ROLE fixture_worker LOGIN PASSWORD '${passwords.fixture_worker}' NOINHERIT;
CREATE ROLE fixture_observed NOLOGIN;
GRANT fixture_owner TO fixture_maintenance;
GRANT fixture_observed TO fixture_api;
REVOKE CREATE,TEMPORARY ON DATABASE postgres FROM PUBLIC;
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
ALTER SCHEMA public OWNER TO fixture_owner;
GRANT USAGE ON SCHEMA public TO fixture_api,fixture_observed,fixture_worker;
ALTER ROLE fixture_api SET app.private_fixture_setting = 'NOT_FOR_CATALOG_OUTPUT';
SET ROLE fixture_owner;
CREATE TABLE public."Customers"("Id" integer PRIMARY KEY,"Name" text);
CREATE TABLE public."MemberPayments"("Id" integer PRIMARY KEY);
CREATE TABLE public."__EFMigrationsHistory"("MigrationId" text PRIMARY KEY);
CREATE SEQUENCE public.fixture_sequence;
CREATE FUNCTION public.fixture_worker_operation() RETURNS integer LANGUAGE sql
 SECURITY DEFINER SET search_path=pg_catalog AS 'SELECT 1 /* PRIVATE_FUNCTION_BODY */';
REVOKE EXECUTE ON FUNCTION public.fixture_worker_operation() FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.fixture_worker_operation() TO fixture_worker;
GRANT SELECT,UPDATE("Name") ON public."Customers" TO fixture_api;
GRANT USAGE ON SEQUENCE public.fixture_sequence TO fixture_api;
ALTER TABLE public."Customers" ENABLE ROW LEVEL SECURITY;
ALTER TABLE public."Customers" FORCE ROW LEVEL SECURITY;
CREATE POLICY fixture_scope ON public."Customers" USING (true) WITH CHECK(true);
INSERT INTO public."Customers" VALUES(1,'Synthetic');
RESET ROLE;
`);
  phase = 'catalog-readback';
  const raw = sql(query, 'fixture_api');
  const api = JSON.parse(raw), owner = JSON.parse(sql(query));
  check('catalog transaction is read-only on PostgreSQL18', () => {
    assert.equal(api.serverMajor, 18); assert.equal(api.transactionReadOnly, 'on');
    assert.ok(api.server.maxConnections > 0); assert.equal(owner.role.superuser, true);
  });
  check('current_user and session_user distinguish SET ROLE', () => {
    assert.equal(api.identity.currentUser, 'fixture_api');
    assert.equal(api.identity.sessionUser, 'fixture_api');
    assert.equal(api.identity.currentEqualsSession, true);
    const changed = JSON.parse(sql('SET ROLE fixture_observed;\n' + query, 'fixture_api'));
    assert.equal(changed.identity.currentUser, 'fixture_observed');
    assert.equal(changed.identity.sessionUser, 'fixture_api');
    assert.equal(changed.identity.currentEqualsSession, false);
    assert.notEqual(changed.identity.currentRoleOid, changed.identity.sessionRoleOid);
  });
  check('non-owner API has no schema or owner-role authority', () => {
    for (const key of ['superuser', 'createRole', 'createDatabase', 'replication', 'bypassRls'])
      assert.equal(api.role[key], false);
    assert.equal(api.database.canCreateSchema, false);
    assert.equal(api.database.canCreateTemporary, false);
    assert.equal(api.publicSchema.create, false);
    assert.equal(api.publicSchema.canSetOwnerRole, false);
    assert.equal(api.publicTables.ownedByCurrentRole, 0);
    denied('CREATE TABLE public.unauthorized(id int);');
    denied('SET ROLE fixture_owner;');
    denied('SET ROLE fixture_maintenance;');
  });
  check('roles and membership inventory retain owner and SET distinctions', () => {
    const schemaOwner = api.roles.find(row => row.name === 'fixture_owner');
    assert.equal(schemaOwner.login, false);
    const maintenance = api.roles.find(row => row.name === 'fixture_maintenance');
    assert.ok(api.memberships.some(row => row.memberOid === maintenance.oid &&
      row.roleOid === schemaOwner.oid && row.set === true && row.admin === false));
    assert.equal(api.schemas.find(row => row.name === 'public').ownerOid, schemaOwner.oid);
  });
  check('column-only profile privilege differs from full table UPDATE', () => {
    const customer = api.relations.find(row => row.name === 'Customers');
    assert.equal(customer.update, false); assert.equal(customer.updateAnyColumn, true);
    assert.equal(customer.maintain, false);
    sql('BEGIN; UPDATE public."Customers" SET "Name"=\'Synthetic revised\' WHERE "Id"=1; ROLLBACK;', 'fixture_api');
    assert.equal(sql('SELECT "Name" FROM public."Customers" WHERE "Id"=1;', 'fixture_api').trim(), 'Synthetic');
    denied('UPDATE public."Customers" SET "Id"=2 WHERE "Id"=1;');
  });
  check('RLS enabled FORCE and effective status are reported', () => {
    const customer = api.relations.find(row => row.name === 'Customers');
    assert.equal(customer.rlsApplicable, true); assert.equal(customer.rlsEnabled, true);
    assert.equal(customer.forceRlsEnabled, true); assert.equal(customer.rlsActive, true);
    assert.ok(api.policies.some(row => row.name === 'fixture_scope' && row.tableOid === customer.oid));
    // This permissive synthetic policy is not evidence of Tenant/Area isolation.
  });
  check('sequence USAGE differs from SELECT and UPDATE', () => {
    const sequence = api.sequences.find(row => row.name === 'fixture_sequence');
    assert.equal(sequence.usage, true); assert.equal(sequence.select, false);
    assert.equal(sequence.update, false);
    assert.equal(sql("SELECT nextval('public.fixture_sequence');", 'fixture_api').trim(), '1');
    denied("SELECT setval('public.fixture_sequence',99);");
    denied('SELECT last_value FROM public.fixture_sequence;');
  });
  check('reserved SECURITY DEFINER execution denied to API and allowed to worker', () => {
    const routine = api.routines.find(row => row.name === 'fixture_worker_operation');
    assert.equal(routine.securityDefiner, true); assert.equal(routine.execute, false);
    denied('SELECT public.fixture_worker_operation();');
    const worker = JSON.parse(sql(query, 'fixture_worker'));
    assert.equal(worker.routines.find(row => row.name === 'fixture_worker_operation').execute, true);
    assert.equal(sql('SELECT public.fixture_worker_operation();', 'fixture_worker').trim(), '1');
  });
  check('financial and migration-history writes denied to API', () => {
    assert.equal(api.relations.find(row => row.name === 'MemberPayments').insert, false);
    denied('INSERT INTO public."MemberPayments" VALUES(1);');
    denied('INSERT INTO public."__EFMigrationsHistory" VALUES(\'unauthorized\');');
    denied('ALTER TABLE public."Customers" ADD COLUMN unauthorized int;');
  });
  check('maintenance DDL succeeds only through its distinct login and owner role', () => {
    sql('BEGIN; SET ROLE fixture_owner; CREATE TABLE public.maintenance_only(id int); ROLLBACK;', 'fixture_maintenance');
    assert.equal(sql("SELECT to_regclass('public.maintenance_only') IS NULL;").trim(), 't');
  });
  check('API password cannot authenticate maintenance or worker login', () => {
    assert.equal(new Set(Object.values(passwords)).size, roles.length);
    // libpq startup errors do not expose SQLSTATE through psql VERBOSITY.
    denied('SELECT 1;', /password authentication failed for user "fixture_maintenance"/u,
      'fixture_maintenance', passwords.fixture_api);
    denied('SELECT 1;', /password authentication failed for user "fixture_worker"/u,
      'fixture_worker', passwords.fixture_api);
  });
  check('catalog excludes fixture secret settings and function bodies', () => {
    assert.doesNotMatch(raw, /NOT_FOR_CATALOG_OUTPUT|PRIVATE_FUNCTION_BODY/u);
    for (const password of Object.values(passwords)) assert.ok(!raw.includes(password));
    assert.ok(api.roleSettings.some(row => row.settingNames.includes('app.private_fixture_setting')));
  });
  check('fixture uses an unpublished network-none PostgreSQL container', () => {
    const config = JSON.parse(docker(['inspect', container]))[0];
    assert.equal(config.HostConfig.NetworkMode, 'none');
    assert.equal(config.HostConfig.ReadonlyRootfs, true);
    assert.deepEqual(config.HostConfig.PortBindings, {});
    assert.deepEqual(config.HostConfig.Binds ?? [], []);
  });
  assert.equal(checks, 13);
} catch {
  // Raw child errors may include SQL/passwords; disclose only a fixed phase label.
  failed = true;
  console.error(`FAIL: isolated catalog qualification phase=${phase}`);
} finally {
  if (started) {
    try { docker(['rm', '--force', container]); }
    catch { failed = true; console.error('FAIL: disposable catalog container cleanup'); }
  }
}
if (failed) process.exitCode = 1;
else console.log('PASS: 13 isolated catalog checks; production privileges=UNVERIFIED; no production connection');
