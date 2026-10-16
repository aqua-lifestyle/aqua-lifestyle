import assert from 'node:assert/strict';
import { generateKeyPairSync, sign } from 'node:crypto';
import { mkdtempSync, mkdirSync, readFileSync, rmSync, symlinkSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { test } from 'node:test';
import { classifyReport, digest, reportFromEvents, validateInventory, verifyAttestedReport, verifySourceFiles } from './mandatory-security.mjs';

// Synthetic classifier/cryptographic fixtures, never actual execution evidence.
const context = { kind: 'ci', repository: 'aqua-lifestyle/aqua-lifestyle', sha: 'a'.repeat(40), runId: '123', runAttempt: 2 };
const name = 'Render failure never starts Vercel or automatic rollback';
const file = 'tools/deployment/release-control.test.mjs';
const policy = { version: 1, tests: [{ id: 'AQUA-SEC-0001', file, name }],
  sources: [{ path: file, sha256: digest('protected assertions') }] };
const policyBytes = Buffer.from(JSON.stringify(policy));
const passed = () => ({ version: 1, context: { ...context }, inventorySha256: digest(policyBytes), exitCode: 0,
  results: [{ ...policy.tests[0], outcome: 'passed', executions: 1 }],
  summary: { tests: 1, passed: 1, failed: 0, cancelled: 0, skipped: 0, todo: 0, suites: 0 } });
const keys = generateKeyPairSync('ed25519');
const publicKey = keys.publicKey.export({ type: 'spki', format: 'pem' });
const envelope = reportBytes => {
  const payload = JSON.stringify({ version: 1, verdict: 'PASS', context, inventorySha256: digest(policyBytes), reportSha256: digest(reportBytes) });
  return { payload, signature: sign(null, Buffer.from(payload), keys.privateKey).toString('base64') };
};

test('exact mandatory identities pass once with exact CI source and attempt', () => {
  assert.equal(classifyReport(passed(), policy, context, policyBytes).cases, 1);
  assert.equal(classifyReport(passed(), policy, context, policyBytes).independentEnforcement, 'UNVERIFIED');
});
for (const outcome of ['skipped', 'todo', 'neutral', 'inconclusive', 'cancelled', 'failed', undefined]) {
  test(`mandatory result rejects ${outcome} even with a successful process`, () => {
    const report = passed(); report.results[0].outcome = outcome;
    assert.throws(() => classifyReport(report, policy, context, policyBytes), /MANDATORY_TEST_NOT_PASSED/);
  });
}
test('deleted, renamed, duplicated and unexpectedly excluded mandatory cases fail closed', () => {
  for (const change of [r => r.results.pop(), r => { r.results[0].name += ' renamed'; },
    r => r.results.push(r.results[0]), r => { r.results[0].file = 'other.test.mjs'; },
    r => { r.results[0].id = 'AQUA-SEC-9999'; }, r => { r.results[0].executions = 0; },
    r => { r.results[0].executions = 2; }]) {
    const report = passed(); change(report);
    assert.throws(() => classifyReport(report, policy, context, policyBytes));
  }
});
test('the exact synthetic 170-pass one-skip result cannot qualify', () => {
  const full = { ...policy, tests: Array.from({ length: 171 }, (_, i) => ({ id: `AQUA-SEC-${String(i + 1).padStart(4, '0')}`, file, name: i ? `case ${i}` : name })) };
  const bytes = Buffer.from(JSON.stringify(full));
  const report = { ...passed(), inventorySha256: digest(bytes),
    results: full.tests.map((t, i) => ({ ...t, outcome: i ? 'passed' : 'skipped', executions: i ? 1 : 0 })),
    summary: { ...passed().summary, tests: 171, passed: 170, skipped: 1 } };
  assert.throws(() => classifyReport(report, full, context, bytes), /MANDATORY_TEST_NOT_PASSED/);
});
for (const field of ['sha', 'runId', 'runAttempt', 'repository', 'kind']) {
  test(`security evidence cannot substitute a different ${field}`, () => {
    const report = passed(); report.context[field] = field === 'runAttempt' ? 1 : field === 'sha' ? 'b'.repeat(40) : 'other';
    assert.throws(() => classifyReport(report, policy, context, policyBytes));
  });
}
test('summary counters and unsuccessful process cannot override exact per-case execution', () => {
  for (const field of Object.keys(passed().summary)) {
    const report = passed(); report.summary[field]++;
    assert.throws(() => classifyReport(report, policy, context, policyBytes));
    delete report.summary[field];
    assert.throws(() => classifyReport(report, policy, context, policyBytes));
  }
  const report = passed(); report.exitCode = 1;
  assert.throws(() => classifyReport(report, policy, context, policyBytes));
});
test('missing result and modified policy cannot qualify', () => {
  assert.throws(() => classifyReport(undefined, policy, context, policyBytes));
  const report = passed(); report.inventorySha256 = 'f'.repeat(64);
  assert.throws(() => classifyReport(report, policy, context, policyBytes));
  for (const invalid of [{ ...policy, tests: [] }, { ...policy, tests: [policy.tests[0], policy.tests[0]] },
    { ...policy, sources: [{ path: '../escape', sha256: 'f'.repeat(64) }] }])
    assert.throws(() => validateInventory(invalid));
});
test('signed result consumer accepts only the separately pinned key and exact report bytes', () => {
  const bytes = Buffer.from(JSON.stringify(passed()));
  assert.equal(verifyAttestedReport(envelope(bytes), publicKey, bytes, policyBytes, context).verdict, 'PASS');
  const forged = Buffer.from(JSON.stringify({ ...passed(), attacker: true }));
  assert.throws(() => verifyAttestedReport(envelope(bytes), publicKey, forged, policyBytes, context), /ATTESTATION_MISMATCH/);
  const attacker = generateKeyPairSync('ed25519');
  const fake = envelope(bytes); fake.signature = sign(null, Buffer.from(fake.payload), attacker.privateKey).toString('base64');
  assert.throws(() => verifyAttestedReport(fake, publicKey, bytes, policyBytes, context), /UNTRUSTED/);
  assert.throws(() => verifyAttestedReport(undefined, publicKey, bytes, policyBytes, context));
  assert.throws(() => verifyAttestedReport(envelope(bytes), publicKey, bytes, policyBytes, { ...context, runAttempt: 3 }));
  assert.throws(() => verifyAttestedReport(envelope(bytes), publicKey, bytes, policyBytes, { ...context, sha: 'b'.repeat(40) }));
});
test('signed false success cannot authorize a skipped security case', () => {
  const report = passed(); report.results[0].outcome = 'skipped';
  const bytes = Buffer.from(JSON.stringify(report));
  assert.throws(() => verifyAttestedReport(envelope(bytes), publicKey, bytes, policyBytes, context), /MANDATORY_TEST_NOT_PASSED/);
});
test('reporter preserves skip and todo semantics and rejects missing or duplicate final summaries', () => {
  const root = '/synthetic';
  const terminal = { type: 'test:pass', data: { name, file: `${root}/${file}`, nesting: 0, details: { type: 'test' } } };
  const summary = { type: 'test:summary', data: { success: true, counts: passed().summary } };
  const events = [terminal, summary];
  assert.equal(classifyReport(reportFromEvents(events, root, policy, context, policyBytes, 0), policy, context, policyBytes).cases, 1);
  for (const flag of ['skip', 'todo']) {
    const changed = structuredClone(events); changed[0].data[flag] = 'mandatory case excluded';
    assert.throws(() => classifyReport(reportFromEvents(changed, root, policy, context, policyBytes, 0), policy, context, policyBytes));
  }
  for (const changed of [[terminal], [...events, summary], [terminal, { ...summary, data: { success: false } }]])
    assert.throws(() => reportFromEvents(changed, root, policy, context, policyBytes, 0));
});
test('separately retained policy rejects weakened assertions, inventory edits and deployment gate deletion', () => {
  const root = mkdtempSync(join(tmpdir(), 'aqua-policy-custody-'));
  const originals = { [file]: 'protected assertions', 'tools/deployment/mandatory-security-tests.json': 'protected inventory',
    '.github/workflows/ci.yml': 'required verification job', '.github/workflows/release.yml': 'credential gate' };
  const externalPolicy = { ...policy, sources: Object.entries(originals).map(([path, text]) => ({ path, sha256: digest(text) })) };
  try {
    for (const [path, text] of Object.entries(originals)) {
      mkdirSync(join(root, path, '..'), { recursive: true }); writeFileSync(join(root, path), text);
    }
    verifySourceFiles(root, externalPolicy);
    for (const [path, text] of Object.entries(originals)) {
      writeFileSync(join(root, path), 'candidate removes protection');
      assert.throws(() => verifySourceFiles(root, externalPolicy), /SOURCE_CHANGED/);
      writeFileSync(join(root, path), text);
      rmSync(join(root, path)); assert.throws(() => verifySourceFiles(root, externalPolicy));
      writeFileSync(join(root, path), text);
    }
    rmSync(join(root, file)); symlinkSync(join(root, '.github/workflows/ci.yml'), join(root, file));
    assert.throws(() => verifySourceFiles(root, externalPolicy), /SOURCE_SYMLINK/);
  } finally { rmSync(root, { recursive: true, force: true }); }
});

test('release safety CI classifies mandatory cases and retains exact-attempt evidence', () => {
  const workflow = readFileSync(new URL('../../.github/workflows/ci.yml', import.meta.url), 'utf8');
  assert.match(workflow, /run: node tools\/deployment\/run-security-tests\.mjs/);
  assert.match(workflow, /name: mandatory-security-\$\{\{ github.sha \}\}-\$\{\{ github.run_id \}\}-\$\{\{ github.run_attempt \}\}/);
  assert.match(workflow, /path: TestResults\/mandatory-security\/report.json\s+if-no-files-found: error/);
});
