// Local adversarial fixture. Holds verifier/policy/key outside a disposable
// candidate copy. It does NOT establish independent GitHub or secret custody.
import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import { generateKeyPairSync, sign } from 'node:crypto';
import { cpSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { classifyReport, digest, reportFromEvents, verifyAttestedReport, verifySourceFiles } from './mandatory-security.mjs';

const root = fileURLToPath(new URL('../../', import.meta.url));
const policyBytes = readFileSync(new URL('./mandatory-security-tests.json', import.meta.url));
const policy = JSON.parse(policyBytes);
const temp = mkdtempSync(join(tmpdir(), 'aqua-security-adversary-'));
const emit = value => console.log(JSON.stringify({ fixtureOnly: true, independentEnforcement: 'UNVERIFIED', ...value }));
try {
  // Copy source bytes, not Git metadata, credentials, build outputs or user files.
  // The suite's real-Git archive exercise gets its own synthetic repository.
  for (const path of [...policy.sources.map(s => s.path), 'tools/deployment/mandatory-security-tests.json',
    'render.yaml', 'AqualLifeStyle/9.4.2/aqua-frontend/vercel.json']) {
    mkdirSync(dirname(join(temp, path)), { recursive: true }); cpSync(join(root, path), join(temp, path));
  }
  const git = args => execFileSync('git', ['-c', 'core.hooksPath=/dev/null', '-c', 'commit.gpgsign=false', ...args],
    { cwd: temp, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] }).trim();
  git(['init']); git(['add', '.']);
  git(['-c', 'user.name=Synthetic fixture', '-c', 'user.email=fixture@example.invalid', 'commit', '-m', 'Synthetic candidate']);
  const context = { kind: 'ci', repository: 'aqua-lifestyle/aqua-lifestyle', sha: git(['rev-parse', 'HEAD']), runId: '123', runAttempt: 2 };
  const reporter = fileURLToPath(new URL('./security-reporter.mjs', import.meta.url));
  // Original 171 cases retain IDs 0001..0171; the classifier cases have later IDs.
  // Two duplicated baseline display names were disambiguated without changing assertions.
  const baseline = { ...policy, tests: policy.tests.filter(t => Number(t.id.slice(-4)) <= 171) };
  assert.equal(baseline.tests.length, 171);
  const bytes = Buffer.from(JSON.stringify(baseline));
  const execute = () => {
    const run = spawnSync(process.execPath, ['--test', `--test-reporter=${reporter}`,
      ...new Set(baseline.tests.map(t => t.file))], { cwd: temp, encoding: 'utf8',
      env: { PATH: process.env.PATH, NODE_OPTIONS: '' }, timeout: 240000, maxBuffer: 16 * 1024 * 1024 });
    assert.equal(run.status, 0, run.stderr || run.stdout);
    return reportFromEvents(run.stdout.trim().split('\n').map(JSON.parse), temp, baseline, context, bytes, run.status);
  };
  verifySourceFiles(temp, policy);
  const positive = execute();
  assert.equal(classifyReport(positive, baseline, context, bytes).cases, 171);
  emit({ probe: 'unmodified-original-case-set', verdict: 'PASS', tests: 171 });
  const file = 'tools/deployment/release-control.test.mjs';
  const original = readFileSync(join(temp, file), 'utf8');
  const needle = "test('Render failure never starts Vercel or automatic rollback'";
  assert(original.includes(needle));
  for (const mutation of ['skip', 'delete', 'rename']) {
    const changed = mutation === 'skip' ? original.replace(needle, needle.replace('test(', 'test.skip(')) :
      mutation === 'rename' ? original.replace(needle, needle.replace('Render failure', 'Renamed regression')) :
      original.replace(needle, needle.replace('test(', 'void ('));
    writeFileSync(join(temp, file), changed);
    const report = execute();
    assert.throws(() => classifyReport(report, baseline, context, bytes));
    assert.throws(() => verifySourceFiles(temp, policy));
    if (mutation === 'skip') {
      assert.equal(report.summary.tests, 171); assert.equal(report.summary.passed, 170); assert.equal(report.summary.skipped, 1);
    }
    emit({ probe: `mandatory-${mutation}`, processExit: 0, summary: report.summary, qualification: 'BLOCKED' });
    writeFileSync(join(temp, file), original);
  }
  for (const path of ['.github/workflows/ci.yml', '.github/workflows/release.yml', 'tools/deployment/run-security-tests.mjs']) {
    const text = readFileSync(join(temp, path));
    for (const attack of ['remove', 'replace']) {
      if (attack === 'remove') rmSync(join(temp, path)); else writeFileSync(join(temp, path), 'candidate declares success without verification');
      assert.throws(() => verifySourceFiles(temp, policy));
      emit({ probe: `${attack}-${path}`, qualification: 'BLOCKED' });
      writeFileSync(join(temp, path), text);
    }
  }
  const inventoryPath = join(temp, 'tools/deployment/mandatory-security-tests.json');
  writeFileSync(inventoryPath, '{"version":1,"tests":[],"sources":[]}');
  // The separately retained pin is authoritative, never the candidate's new hash.
  assert.notEqual(digest(readFileSync(inventoryPath)), digest(policyBytes));
  emit({ probe: 'candidate-inventory-replacement', qualification: 'BLOCKED' });
  const pair = generateKeyPairSync('ed25519');
  const key = pair.publicKey.export({ type: 'spki', format: 'pem' });
  const reportBytes = Buffer.from(JSON.stringify(positive));
  const payload = JSON.stringify({ version: 1, verdict: 'PASS', context, inventorySha256: digest(bytes), reportSha256: digest(reportBytes) });
  const envelope = { payload, signature: sign(null, Buffer.from(payload), pair.privateKey).toString('base64') };
  verifyAttestedReport(envelope, key, reportBytes, bytes, context);
  for (const attack of ['missing-file', 'forged-file', 'wrong-commit', 'wrong-attempt', 'forged-signature']) {
    const badEnvelope = attack === 'forged-signature' ? { ...envelope, signature: Buffer.alloc(64).toString('base64') } : envelope;
    const badReport = attack === 'missing-file' ? undefined : attack === 'forged-file' ? Buffer.from('{}') : reportBytes;
    const expected = attack === 'wrong-commit' ? { ...context, sha: 'f'.repeat(40) } :
      attack === 'wrong-attempt' ? { ...context, runAttempt: 3 } : context;
    assert.throws(() => verifyAttestedReport(badEnvelope, key, badReport, bytes, expected));
    emit({ probe: attack, qualification: 'BLOCKED' });
  }
  emit({ verdict: 'PASS', scope: 'local classifier and separately retained custody fixture',
    liveGitHubEnforcement: 'BLOCKED', credentialBoundary: 'BLOCKED' });
} finally { rmSync(temp, { recursive: true, force: true }); }
