import assert from 'node:assert/strict';
import { test } from 'node:test';
import { readFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { assess, resource } from './aqgreen-clean-start-preflight.mjs';

const time = Date.parse('2026-10-27T09:01:00Z');
const options = { boundary: '2026-10-29T22:00:00Z', resetId: 'SYNTHETIC-PREFLIGHT', start: '2026-10-27T09:00:00Z', end: '2026-10-27T09:15:00Z' };
const secret = 'SYNTHETIC-CREDENTIAL-NEVER-EMIT';
const host = `${resource}.frankfurt-postgres.render.com`;
const metadata = () => ({ id: resource, externalConnectionString: `postgresql://fixture_user:${secret}@${host}/aqualifestyle?sslmode=require` });
const sqlReceipt = () => ({ contract: 'AQGreenCleanStartScreenV2', databaseScreen: 'PASS', overall: 'UNKNOWN',
  checks: { transaction: 'PASS', visibility: 'PASS', schema: 'PASS', scope: 'PASS', financialAndStructuralExposure: 'PASS',
    testDataClassification: 'UNKNOWN', externalBusinessObligations: 'UNKNOWN' },
  sourceFingerprint: 'a'.repeat(64), databaseFingerprint: 'c'.repeat(64), schemaFingerprint: 'd'.repeat(64),
  snapshotFingerprint: 'b'.repeat(64), observedAtUtc: new Date(time).toISOString() });
function fixture({ provider = metadata(), sql = sqlReceipt(), providerCode = 0, sqlCode = 0,
  providerProcess = {}, databaseProcess = {}, now = () => time, read = readFileSync } = {}) {
  const calls = [];
  const result = assess(options, { now, read, environment: { PGHOSTADDR: 'unsafe', PGSERVICE: 'unsafe', PGOPTIONS: '-c transaction_read_only=off', PGSSLMODE: 'disable', PSQL_ECHO: 'all' },
    execute(command, args, settings) {
      calls.push({ command, args, settings });
      return { status: command === 'render' ? providerCode : sqlCode,
        stdout: JSON.stringify(command === 'render' ? provider : sql), stderr: secret + ' PRIVATE-FINANCIAL-DIAGNOSTIC 1',
        ...(command === 'render' ? providerProcess : databaseProcess) };
    } });
  assert.ok(!JSON.stringify(result).includes(secret));
  assert.ok(!JSON.stringify(result).includes('PRIVATE-FINANCIAL'));
  return { result, calls };
}

test('logical Render identity and strict TLS return only statuses and fingerprints, with external facts UNKNOWN', () => {
  const { result, calls } = fixture();
  assert.equal(result.databaseScreen, 'PASS'); assert.equal(result.overall, 'UNKNOWN');
  assert.equal(result.checks.logicalSourceIdentity, 'PASS'); assert.equal(result.checks.rollback, 'PASS');
  assert.equal(calls.length, 2);
  const { env, timeout, killSignal } = calls[1].settings;
  assert.equal(env.PGHOST, host); assert.equal(env.PGDATABASE, 'aqualifestyle');
  assert.equal(env.PGUSER, 'fixture_user'); assert.equal(env.PGPASSWORD, secret);
  assert.equal(env.PGSSLMODE, 'verify-full'); assert.equal(env.PGSSLSNI, '1');
  assert.equal(env.PGSSLROOTCERT, '/etc/ssl/certs/ca-certificates.crt');
  assert.match(env.PGOPTIONS, /default_transaction_read_only=on/);
  assert.match(env.PGOPTIONS, /statement_timeout=30000/);
  assert.equal(env.PGHOSTADDR, undefined); assert.equal(env.PGSERVICE, undefined); assert.equal(env.PSQL_ECHO, undefined);
  assert.equal(env.PSQL_HISTORY, '/dev/null'); assert.ok(timeout <= 120000); assert.equal(killSignal, 'SIGKILL');
  assert.ok(calls[1].args.includes('-X')); assert.ok(!calls[1].args.join(' ').includes(secret));
  assert.ok(calls[1].args.includes('--file=-'));
  assert.match(calls[1].settings.input, /REPEATABLE READ READ ONLY/);
  assert.ok(!calls[1].settings.input.includes('\\ir'));
  for (const value of Object.values(result.checks)) assert.ok(['PASS', 'FAIL', 'UNKNOWN'].includes(value));
});

test('wrong resource, host, database, port, missing credentials or ambiguous endpoints never reach SQL', () => {
  for (const provider of [
    { ...metadata(), id: 'dpg-wrong' },
    { id: resource, externalConnectionString: `postgresql://fixture_user:${secret}@evil.example/aqualifestyle` },
    { id: resource, externalConnectionString: `postgresql://fixture_user:${secret}@${host}/other` },
    { id: resource, externalConnectionString: `postgresql://fixture_user:${secret}@${host}:9999/aqualifestyle` },
    { id: resource, externalConnectionString: `postgresql://fixture_user@${host}/aqualifestyle` },
    { ...metadata(), alternate: `postgresql://fixture_user:other@${host}/aqualifestyle` }
  ]) { const { result, calls } = fixture({ provider }); assert.equal(result.overall, 'UNKNOWN'); assert.equal(calls.length, 1); }
});

test('missing, expired, future, oversized or malformed windows and scope inputs cause no network operation', () => {
  for (const patch of [{ start: '2026-10-27T10:00:00Z' }, { end: '2026-10-27T09:00:30Z' },
    { end: '2026-10-27T10:00:00Z' }, { end: 'not-a-date' }, { resetId: 'bad/value' }, { boundary: '2026-10-26T00:00:00Z' }]) {
    let calls = 0;
    const result = assess({ ...options, ...patch }, { now: () => time, execute() { calls++; throw Error('MUST NOT EXECUTE'); } });
    assert.equal(result.overall, 'UNKNOWN');
    assert.equal(calls, 0);
  }
});

test('changed reviewed SQL fails before provider access', () => {
  const { result, calls } = fixture({ read: path => Buffer.concat([readFileSync(path), Buffer.from('\n-- tamper')]) });
  assert.equal(result.overall, 'UNKNOWN'); assert.equal(calls.length, 0);
  let changed = false;
  const frozen = assess(options, { now: () => time,
    read: path => changed ? Buffer.from('UNAPPROVED CHANGED SOURCE') : readFileSync(path),
    execute(command, args, settings) {
      changed = true;
      if (command === 'render') return { status: 0, stdout: JSON.stringify(metadata()) };
      assert.match(settings.input, /REPEATABLE READ READ ONLY/);
      assert.ok(!settings.input.includes('UNAPPROVED CHANGED SOURCE'));
      return { status: 0, stdout: JSON.stringify(sqlReceipt()) };
    } });
  assert.equal(frozen.databaseScreen, 'PASS');
});

test('provider, TLS, SQL permission and client failures remain UNKNOWN without raw errors or retry', () => {
  for (const patch of [{ providerCode: 1 }, { sqlCode: 2 }, { sqlCode: 3 }]) {
    const { result, calls } = fixture(patch); assert.equal(result.overall, 'UNKNOWN');
    assert.equal(calls.length, patch.providerCode ? 1 : 2);
  }
});

test('window expiry after provider lookup or SQL prevents a success receipt', () => {
  let readingTime = time, networkCalls = 0;
  const duringRead = assess(options, { now: () => readingTime,
    read(path) { readingTime = Date.parse(options.end); return readFileSync(path); },
    execute() { networkCalls++; throw Error('MUST NOT EXECUTE'); } });
  assert.equal(duringRead.overall, 'UNKNOWN'); assert.equal(networkCalls, 0);
  let tick = 0;
  const { result, calls } = fixture({ now: () => ++tick > 2 ? Date.parse(options.end) : time });
  assert.equal(result.overall, 'UNKNOWN'); assert.equal(calls.length, 1);
  let current = time;
  const afterSql = assess(options, { now: () => current, execute(command) {
    if (command === 'render') return { status: 0, stdout: JSON.stringify(metadata()) };
    current = Date.parse(options.end); return { status: 0, stdout: JSON.stringify(sqlReceipt()) };
  } });
  assert.equal(afterSql.overall, 'UNKNOWN');
});

test('unexpected counts, rows, invented business acceptance or inconsistent PASS are rejected and suppressed', () => {
  for (const modify of [r => { r.populationCount = 1; }, r => { r.customer = secret; },
    r => { r.overall = 'PASS'; }, r => { r.checks.externalBusinessObligations = 'PASS'; },
    r => { r.checks.visibility = 'UNKNOWN'; }, r => { r.sourceFingerprint = null; }]) {
    const sql = sqlReceipt(); modify(sql); const { result } = fixture({ sql }); assert.equal(result.overall, 'UNKNOWN');
    assert.equal(result.checks.execution, 'UNKNOWN');
  }
});

test('known financial exposure remains FAIL, with no count or source-row output', () => {
  const sql = sqlReceipt(); sql.checks.financialAndStructuralExposure = sql.databaseScreen = sql.overall = 'FAIL'; sql.sourceFingerprint = null;
  const { result } = fixture({ sql }); assert.equal(result.overall, 'FAIL'); assert.equal(result.databaseScreen, 'FAIL');
  assert.equal(result.checks.externalBusinessObligations, 'UNKNOWN');
});

test('incomplete visibility remains UNKNOWN for individual scope and financial checks', () => {
  const sql = sqlReceipt();
  for (const k of ['visibility', 'schema', 'scope', 'financialAndStructuralExposure']) sql.checks[k] = 'UNKNOWN';
  sql.databaseScreen = sql.overall = 'UNKNOWN'; sql.sourceFingerprint = null;
  const { result } = fixture({ sql });
  assert.equal(result.checks.execution, 'PASS'); assert.equal(result.databaseScreen, 'UNKNOWN');
  assert.equal(result.checks.financialAndStructuralExposure, 'UNKNOWN');
});

test('SQL entry requires server TLS/read-only/identity and emits only after rollback; native function is absent', () => {
  const entry = readFileSync(new URL('../../AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.EntityFrameworkCore/Operations/CleanStart/preflight.sql', import.meta.url), 'utf8');
  const checks = readFileSync(new URL('../../AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.EntityFrameworkCore/Operations/CleanStart/preflight-checks.sql', import.meta.url), 'utf8');
  assert.match(entry, /REPEATABLE READ READ ONLY/); assert.match(entry, /pg_stat_ssl/);
  assert.match(entry, /SET LOCAL search_path=pg_catalog/);
  assert.match(entry, /current_user<>session_user/); assert.match(entry, /current_database\(\)<>'aqualifestyle'/);
  assert.ok(entry.indexOf('ROLLBACK;') < entry.indexOf('\\echo :receipt'));
  assert.match(entry, /\\o \/dev\/null/); assert.ok(!(entry + checks).includes('pg_control_system'));
});

test('actual CLI with no approved parameters returns only UNKNOWN without a production connection', () => {
  const p = spawnSync(process.execPath, [fileURLToPath(new URL('./aqgreen-clean-start-preflight.mjs', import.meta.url))], { encoding: 'utf8' });
  assert.equal(p.status, 1); assert.equal(p.stderr, ''); assert.equal(JSON.parse(p.stdout).overall, 'UNKNOWN');
  const r = JSON.parse(p.stdout);
  assert.equal(r.diagnostics.failureCategory, 'CONFIGURATION_OR_PREREQUISITES');
  assert.equal(r.diagnostics.launcherExitStatus, p.status);
  assert.ok(Date.parse(r.evidence.finishedAtUtc) >= Date.parse(r.evidence.attemptedAtUtc));
});

test('configuration, provider and database process failures retain only safe categories and exit statuses', () => {
  for (const [patch, category, stage, provider, database] of [
    [{ providerCode: 1 }, 'CONFIGURATION_OR_PREREQUISITES', 'PROVIDER_LOOKUP', 1, null],
    [{ providerProcess: { status: null, error: { code: 'ENOENT', message: secret } } }, 'CONFIGURATION_OR_PREREQUISITES', 'PROVIDER_LOOKUP', null, null],
    [{ sqlCode: 1 }, 'CONFIGURATION_OR_PREREQUISITES', 'DATABASE_CLIENT', 0, 1],
    [{ sqlCode: 2 }, 'DATABASE_CONNECTION_OR_TLS', 'DATABASE_CLIENT', 0, 2],
    [{ sqlCode: 3 }, 'SQL_EXECUTION', 'DATABASE_CLIENT', 0, 3],
    [{ databaseProcess: { status: null, error: { code: 'ENOENT', message: secret } } }, 'CONFIGURATION_OR_PREREQUISITES', 'DATABASE_CLIENT', 0, null],
    [{ databaseProcess: { status: null, signal: 'SIGTERM' } }, 'UNDETERMINED', 'DATABASE_CLIENT', 0, null]
  ]) {
    const { result, calls } = fixture(patch);
    assert.equal(result.overall, 'UNKNOWN');
    assert.deepEqual(result.diagnostics, { failureCategory: category, stage, launcherExitStatus: 1,
      processExitStatuses: { provider, database } });
    assert.equal(result.evidence.finishedAtUtc, new Date(time).toISOString());
    assert.equal(calls.length, stage === 'PROVIDER_LOOKUP' ? 1 : 2);
  }
});

test('identity failures are distinct from missing credentials and SQL failures without exposing diagnostics', () => {
  const wrongTarget = fixture({ provider: { ...metadata(), id: 'dpg-wrong' } });
  assert.equal(wrongTarget.result.diagnostics.failureCategory, 'DATABASE_IDENTITY_VERIFICATION');
  assert.equal(wrongTarget.result.diagnostics.stage, 'LOGICAL_TARGET_VERIFICATION');
  assert.equal(wrongTarget.calls.length, 1);
  const noPassword = fixture({ provider: { id: resource, externalConnectionString: `postgresql://fixture_user@${host}/aqualifestyle` } });
  assert.equal(noPassword.result.diagnostics.failureCategory, 'CONFIGURATION_OR_PREREQUISITES');
  for (const [stderr, expected] of [
    ['psql:<stdin>:30: ERROR:  CLEAN_START_SOURCE_OR_READ_ONLY_SESSION_UNVERIFIED\n', 'DATABASE_IDENTITY_VERIFICATION'],
    [`psql:<stdin>:30: ERROR:  private row mentions CLEAN_START_SOURCE_OR_READ_ONLY_SESSION_UNVERIFIED ${secret}\n`, 'SQL_EXECUTION'],
    [`psql:<stdin>:30: ERROR:  ${secret}\n`, 'SQL_EXECUTION']
  ]) {
    const { result, calls } = fixture({ sqlCode: 3, databaseProcess: { stderr } });
    assert.equal(result.diagnostics.failureCategory, expected);
    assert.equal(result.diagnostics.processExitStatuses.database, 3);
    assert.ok(calls[1].args.includes('--set=VERBOSITY=terse'));
    assert.equal(calls[1].settings.env.LC_ALL, 'C');
    assert.ok(!JSON.stringify(result).includes('CLEAN_START_SOURCE_OR_READ_ONLY_SESSION_UNVERIFIED'));
  }
});

test('child timeouts and fixed server timeout markers identify timeout without guessing from a signal', () => {
  for (const patch of [
    { providerProcess: { status: null, signal: 'SIGKILL', error: { code: 'ETIMEDOUT', message: secret } } },
    { databaseProcess: { status: null, signal: 'SIGKILL', error: { code: 'ETIMEDOUT', message: secret } } },
    ...['statement', 'lock'].map(kind => ({ sqlCode: 3, databaseProcess: {
      stderr: `psql:<stdin>:120: ERROR:  canceling statement due to ${kind} timeout\n` } })),
    { sqlCode: 2, databaseProcess: { stderr: 'psql:<stdin>:120: FATAL:  terminating connection due to idle-in-transaction timeout\n' } }
  ]) {
    const { result } = fixture(patch);
    assert.equal(result.diagnostics.failureCategory, 'EXECUTION_TIMEOUT_OR_WINDOW_EXPIRY');
    assert.equal(result.overall, 'UNKNOWN');
  }
  const { result } = fixture({ sqlCode: 3, databaseProcess: {
    stderr: `psql:<stdin>:120: ERROR:  ${secret} mentions canceling statement due to statement timeout\n` } });
  assert.equal(result.diagnostics.failureCategory, 'SQL_EXECUTION');
});

test('malformed provider or database output and rejected receipt fields are result-validation failures', () => {
  for (const patch of [{ providerProcess: { stdout: secret } }, { databaseProcess: { stdout: secret } },
    { sql: { ...sqlReceipt(), sensitiveCount: 1 } }]) {
    const { result } = fixture(patch);
    assert.equal(result.diagnostics.failureCategory, 'RESULT_VALIDATION');
    assert.equal(result.overall, 'UNKNOWN');
  }
});

test('window cancellation records completion while a short but open window alone is not a failure', () => {
  const end = Date.parse(options.end);
  const expired = fixture({ now: () => end });
  assert.equal(expired.calls.length, 0);
  assert.equal(expired.result.diagnostics.failureCategory, 'EXECUTION_TIMEOUT_OR_WINDOW_EXPIRY');
  assert.deepEqual(expired.result.diagnostics.processExitStatuses, { provider: null, database: null });
  let current = time;
  const deadline = assess(options, { now: () => current, execute(command) {
    if (command === 'render') return { status: 0, stdout: JSON.stringify(metadata()) };
    current = end;
    return { status: 0, stdout: JSON.stringify(sqlReceipt()) };
  } });
  assert.equal(deadline.diagnostics.failureCategory, 'EXECUTION_TIMEOUT_OR_WINDOW_EXPIRY');
  assert.equal(deadline.evidence.finishedAtUtc, new Date(end).toISOString());
  assert.deepEqual(deadline.diagnostics.processExitStatuses, { provider: 0, database: 0 });
  const short = fixture({ now: () => end - 34025 });
  assert.equal(short.result.databaseScreen, 'PASS');
  assert.equal(short.result.diagnostics.failureCategory, null);
  assert.ok(short.calls[1].settings.timeout <= 34025);
});

test('completed database PASS, FAIL and UNKNOWN retain business semantics and accurately record process success', () => {
  for (const screen of ['PASS', 'FAIL', 'UNKNOWN']) {
    const sql = sqlReceipt();
    sql.databaseScreen = screen; sql.overall = screen === 'FAIL' ? 'FAIL' : 'UNKNOWN';
    if (screen !== 'PASS') {
      sql.sourceFingerprint = null; sql.checks.financialAndStructuralExposure = screen;
    }
    const { result } = fixture({ sql });
    assert.equal(result.databaseScreen, screen);
    assert.equal(result.overall, sql.overall);
    assert.deepEqual(result.diagnostics, { failureCategory: null, stage: 'COMPLETE',
      launcherExitStatus: screen === 'FAIL' ? 2 : 1, processExitStatuses: { provider: 0, database: 0 } });
    assert.equal(result.checks.externalBusinessObligations, 'UNKNOWN');
    assert.equal(result.evidence.finishedAtUtc, new Date(time).toISOString());
  }
});
