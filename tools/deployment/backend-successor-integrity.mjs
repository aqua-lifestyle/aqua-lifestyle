// Qualified source CI measures the actual image and migrated disposable schema.
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { writeFileSync } from 'node:fs';

const [pg, image, dockerHost, output] = process.argv.slice(2);
const sha = process.env.GITHUB_SHA, runId = process.env.GITHUB_RUN_ID,
  attempt = process.env.GITHUB_RUN_ATTEMPT;
if (!/^aqua-runtime-pg-[A-Za-z0-9]+$/.test(pg ?? '') ||
  !/^sha256:[a-f0-9]{64}$/.test(image ?? '') || !/^unix:\/\/.+/.test(dockerHost ?? '') ||
  !output || !/^[a-f0-9]{40}$/.test(sha ?? '') || !/^[1-9]\d*$/.test(runId ?? '') ||
  !/^[1-9]\d*$/.test(attempt ?? '') || process.env.GITHUB_REPOSITORY !== 'aqua-lifestyle/aqua-lifestyle')
  throw new Error('QUALIFIED_RUNTIME_EVIDENCE_CONTEXT_REQUIRED');
const docker = (...args) => execFileSync('docker', ['--host', dockerHost, ...args],
  { encoding: 'utf8', maxBuffer: 2 * 1024 * 1024, stdio: ['ignore', 'pipe', 'pipe'] });
const hash = bytes => createHash('sha256').update(bytes).digest('hex');
const assembly = name => {
  const path = `/app/${name}.dll`;
  const match = /^([a-f0-9]{64})  (\/.+)$/.exec(docker('run', '--rm', '--entrypoint', 'sha256sum', image, path).trim());
  if (!match || match[2] !== path) throw new Error('QUALIFIED_ASSEMBLY_DIGEST_UNAVAILABLE');
  return match[1];
};
// Exact UTF-8 SQL used by AQGreenReconciliationEvidenceSnapshot.CaptureAsync.
const sql = `SELECT encode(convert_to((SELECT string_agg(
  table_name || ':' || column_name || ':' || data_type || ':' || is_nullable,
  E'\\n' ORDER BY table_name, ordinal_position)
  FROM information_schema.columns WHERE table_schema = 'public'), 'UTF8'), 'base64')`;
const encoded = docker('exec', pg, 'psql', '-X', '-qAt', '-v', 'ON_ERROR_STOP=1',
  '-U', 'postgres', '-d', 'aqua_runtime', '-c', sql).replace(/\s+/g, '');
if (!/^[A-Za-z0-9+/=]+$/.test(encoded)) throw new Error('QUALIFIED_SCHEMA_DIGEST_UNAVAILABLE');
const schema = Buffer.from(encoded, 'base64');
if (schema.length < 100) throw new Error('QUALIFIED_SCHEMA_DIGEST_UNAVAILABLE');
const receipt = { contract: 'aqua.backend-successor-integrity-v1', repository: process.env.GITHUB_REPOSITORY,
  sha, ciRunId: runId, ciRunAttempt: Number(attempt), successorSchemaSha256: hash(schema),
  successorAssemblySha256: assembly('AqualLifeStyle.EntityFrameworkCore'),
  successorApplicationSha256: assembly('AqualLifeStyle.Application'),
  successorCoreSha256: assembly('AqualLifeStyle.Core') };
writeFileSync(output, JSON.stringify(receipt) + '\n', { flag: 'wx', mode: 0o600 });
