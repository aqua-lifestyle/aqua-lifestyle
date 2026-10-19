// Candidate-controlled CI hardening, NOT independent enforcement. See the custody
// contract. This command has no production secrets, network calls or signer.
import { execFileSync, spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { classifyReport, reportFromEvents, validateContext, validateInventory, verifySourceFiles } from './mandatory-security.mjs';

export function runSecurityTests(env = process.env, local = false) {
  const root = fileURLToPath(new URL('../../', import.meta.url));
  const policyBytes = readFileSync(new URL('./mandatory-security-tests.json', import.meta.url));
  const policy = validateInventory(JSON.parse(policyBytes));
  const context = { kind: local ? 'local' : 'ci', repository: local ? 'aqua-lifestyle/aqua-lifestyle' : env.GITHUB_REPOSITORY,
    sha: execFileSync('git', ['rev-parse', 'HEAD'], { cwd: root, encoding: 'utf8' }).trim(),
    runId: local ? null : env.GITHUB_RUN_ID, runAttempt: local ? null : Number(env.GITHUB_RUN_ATTEMPT) };
  validateContext(context);
  if (!local && context.sha !== env.GITHUB_SHA) throw Error('SECURITY_CHECKOUT_MISMATCH');
  verifySourceFiles(root, policy);
  const files = [...new Set(policy.tests.map(t => t.file))].sort();
  const testEnv = { ...env, NODE_OPTIONS: '' };
  delete testEnv.NODE_TEST_CONTEXT;
  const result = spawnSync(process.execPath, ['--test', '--test-reporter=./tools/deployment/security-reporter.mjs', ...files],
    { cwd: root, encoding: 'utf8', timeout: 240000, maxBuffer: 16 * 1024 * 1024,
      env: testEnv });
  if (result.error || result.signal || result.status !== 0) throw Error('SECURITY_TEST_PROCESS_FAILED');
  const events = result.stdout.trim().split('\n').map(line => JSON.parse(line));
  const report = reportFromEvents(events, root, policy, context, policyBytes, result.status);
  const qualified = classifyReport(report, policy, context, policyBytes);
  const output = resolve(root, 'TestResults/mandatory-security');
  mkdirSync(output, { recursive: true });
  writeFileSync(resolve(output, 'report.json'), JSON.stringify(report, null, 2) + '\n');
  console.log(JSON.stringify(qualified));
  return qualified;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    if (process.argv.length > 3 || (process.argv[2] && process.argv[2] !== '--local')) throw Error('SECURITY_ARGUMENT_INVALID');
    runSecurityTests(process.env, process.argv[2] === '--local');
  } catch (error) {
    console.error(JSON.stringify({ verdict: 'BLOCKED', reason: error.message, independentEnforcement: 'UNVERIFIED' }));
    process.exitCode = 1;
  }
}
