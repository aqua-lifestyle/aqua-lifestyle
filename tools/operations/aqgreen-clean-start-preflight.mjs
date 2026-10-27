// Reviewed, status-only READ-ONLY assessment. No production authorization is implied.
import { readFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { fileURLToPath } from 'node:url';

export const resource = 'dpg-d9f0jne1a83c73d8m780-a';
const database = 'aqualifestyle';
const hostname = `${resource}.frankfurt-postgres.render.com`;
const directory = new URL('../../AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.EntityFrameworkCore/Operations/CleanStart/', import.meta.url);
export const pins = Object.freeze({
  'preflight.sql': 'b0b495e30d0f3b910980f6d212b75b57de725c35f56c253ff973f681382264dd',
  'preflight-checks.sql': '1d8845c76fa87843f48fc2e648a57333c94d2417fc018c4b499ff28d25e91651',
  'state.sql': 'db3d36ac23172c2005aa6edd202ea6f9cb6b4635c75c1bdbd2889f47571fd100'
});
const hash = b => createHash('sha256').update(b).digest('hex');
const digest = s => typeof s === 'string' && /^[a-f0-9]{64}$/.test(s);
const states = ['PASS', 'FAIL', 'UNKNOWN'];
const keys = ['transaction', 'visibility', 'schema', 'scope', 'financialAndStructuralExposure', 'testDataClassification', 'externalBusinessObligations'];
const exact = (o, fields) => o && !Array.isArray(o) && Object.keys(o).sort().join() === [...fields].sort().join();
const utc = s => typeof s === 'string' && /^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\dZ$/.test(s) &&
  Number.isFinite(Date.parse(s)) && new Date(s).toISOString().replace('.000Z', 'Z') === s;
const worst = values => values.includes('FAIL') ? 'FAIL' : values.includes('UNKNOWN') ? 'UNKNOWN' : 'PASS';

export function connection(metadata) {
  const ids = new Set(), urls = new Set();
  const visit = value => {
    if (value && typeof value === 'object') for (const [k, v] of Object.entries(value)) {
      if (k === 'id' && typeof v === 'string' && v.startsWith('dpg-')) ids.add(v);
      visit(v);
    }
    else if (typeof value === 'string' && /^postgres(?:ql)?:\/\//.test(value)) {
      const u = new URL(value);
      if (u.hostname === hostname && (!u.port || u.port === '5432') && decodeURIComponent(u.pathname) === '/' + database && !u.hash)
        urls.add(u.href);
    }
  };
  visit(metadata);
  if (ids.size !== 1 || !ids.has(resource) || urls.size !== 1) throw Error('UNKNOWN');
  const u = new URL([...urls][0]), user = decodeURIComponent(u.username), password = decodeURIComponent(u.password);
  if (!/^[A-Za-z0-9_]+$/.test(user) || !password || /[\x00-\x1f\x7f]/.test(password)) throw Error('UNKNOWN');
  return { host: hostname, user, password };
}

export function assess(options, { execute = spawnSync, read = readFileSync, now = Date.now, environment = process.env } = {}) {
  const started = now();
  const unknown = () => ({ contract: 'AQGreenCleanStartScreenV2', databaseScreen: 'UNKNOWN', overall: 'UNKNOWN',
    checks: { execution: 'UNKNOWN' }, evidence: { resource, database, tenant: 1, attemptedAtUtc: new Date(started).toISOString() } });
  try {
    if (!exact(options, ['boundary', 'resetId', 'start', 'end']) || ![options.boundary, options.start, options.end].every(utc) ||
        !/^[A-Za-z0-9.-]{1,80}$/.test(options.resetId) || Date.parse(options.end) <= Date.parse(options.start) ||
        Date.parse(options.end) - Date.parse(options.start) > 900_000 ||
        started < Date.parse(options.start) || started >= Date.parse(options.end) || Date.parse(options.boundary) <= Date.parse(options.end)) return unknown();
    const sqlFiles = {};
    for (const [file, pin] of Object.entries(pins)) {
      const bytes = read(new URL(file, directory));
      if (hash(bytes) !== pin) return unknown();
      sqlFiles[file] = bytes.toString('utf8');
    }
    const runnerDigest = hash(read(new URL(import.meta.url)));
    const include = '\\ir preflight-checks.sql\n';
    if (sqlFiles['preflight.sql'].split(include).length !== 2) return unknown();
    const approvedSql = sqlFiles['preflight.sql'].replace(include, sqlFiles['preflight-checks.sql']);
    const remaining = limit => Math.min(limit, Date.parse(options.end) - now());
    const providerBudget = remaining(15000);
    if (providerBudget <= 0) return unknown();
    const provider = execute('render', ['postgres', 'get', resource, '--include-sensitive-connection-info', '--output', 'json', '--confirm'],
      { encoding: 'utf8', timeout: providerBudget, killSignal: 'SIGKILL', maxBuffer: 1048576, stdio: ['ignore', 'pipe', 'pipe'] });
    if (provider.status !== 0 || now() >= Date.parse(options.end)) return unknown();
    const c = connection(JSON.parse(provider.stdout));
    const env = { ...environment };
    for (const key of Object.keys(env)) if (key.startsWith('PG') || key.startsWith('PSQL_')) delete env[key];
    Object.assign(env, { PGHOST: c.host, PGPORT: '5432', PGDATABASE: database, PGUSER: c.user, PGPASSWORD: c.password,
      PGSSLMODE: 'verify-full', PGSSLROOTCERT: '/etc/ssl/certs/ca-certificates.crt', PGSSLSNI: '1', PGCONNECT_TIMEOUT: '8',
      PGAPPNAME: 'aqgreen-clean-start-status-screen', PSQL_HISTORY: '/dev/null',
      PGOPTIONS: '-c default_transaction_read_only=on -c statement_timeout=30000 -c lock_timeout=3000 -c idle_in_transaction_session_timeout=45000' });
    const sqlBudget = remaining(120000);
    if (sqlBudget <= 0) return unknown();
    const result = execute('psql', ['-X', '-qAt', '--no-password', '--set=ON_ERROR_STOP=1', '--set=VERBOSITY=sqlstate',
      '--set=boundary_utc=' + options.boundary, '--set=reset_id=' + options.resetId,
      '--file=-'],
      { env, input: approvedSql, encoding: 'utf8', timeout: sqlBudget, killSignal: 'SIGKILL', maxBuffer: 65536, stdio: ['pipe', 'pipe', 'pipe'] });
    // Never emit raw client/provider errors, SQL diagnostics, credentials or rows.
    if (result.status !== 0 || now() >= Date.parse(options.end)) return unknown();
    const r = JSON.parse(result.stdout.trim());
    if (!exact(r, ['contract', 'databaseScreen', 'overall', 'checks', 'sourceFingerprint', 'databaseFingerprint', 'schemaFingerprint', 'snapshotFingerprint', 'observedAtUtc']) ||
        r.contract !== 'AQGreenCleanStartScreenV2' || !exact(r.checks, keys) ||
        !Object.values(r.checks).every(v => states.includes(v)) || r.checks.transaction !== 'PASS' ||
        r.checks.testDataClassification !== 'UNKNOWN' || r.checks.externalBusinessObligations !== 'UNKNOWN' ||
        r.databaseScreen !== worst(keys.slice(0, 5).map(k => r.checks[k])) ||
        r.overall !== worst([r.databaseScreen, 'UNKNOWN']) ||
        (r.databaseScreen === 'PASS' ? !digest(r.sourceFingerprint) : r.sourceFingerprint !== null) ||
        !digest(r.databaseFingerprint) || !digest(r.schemaFingerprint) || !digest(r.snapshotFingerprint) ||
        !Number.isFinite(Date.parse(r.observedAtUtc)) || Date.parse(r.observedAtUtc) < Date.parse(options.start) ||
        Date.parse(r.observedAtUtc) >= Date.parse(options.end)) return unknown();
    const finished = now();
    if (finished >= Date.parse(options.end)) return unknown();
    return { contract: r.contract, databaseScreen: r.databaseScreen, overall: r.overall,
      checks: { execution: 'PASS', logicalSourceIdentity: 'PASS', rollback: 'PASS', ...r.checks },
      evidence: { resource, database, tenant: 1, identityMode: 'RENDER_RESOURCE_TLS_DATABASE_SESSION_V1',
        observedAtUtc: new Date(r.observedAtUtc).toISOString(), finishedAtUtc: new Date(finished).toISOString(),
        sqlDigests: pins, runnerDigest,
        sourceFingerprint: r.sourceFingerprint, databaseFingerprint: r.databaseFingerprint,
        schemaFingerprint: r.schemaFingerprint, snapshotFingerprint: r.snapshotFingerprint } };
  } catch { return unknown(); }
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const flags = process.argv.slice(2);
  const options = {};
  const names = { '--boundary-utc': 'boundary', '--reset-id': 'resetId', '--window-start-utc': 'start', '--window-end-utc': 'end' };
  let valid = flags.length === 8;
  for (let i = 0; i < flags.length; i += 2) {
    const name = names[flags[i]];
    if (!name || Object.hasOwn(options, name) || flags[i + 1] === undefined) valid = false;
    else options[name] = flags[i + 1];
  }
  const receipt = assess(valid ? options : {});
  process.stdout.write(JSON.stringify(receipt) + '\n');
  process.exitCode = receipt.overall === 'PASS' ? 0 : receipt.overall === 'FAIL' ? 2 : 1;
}
