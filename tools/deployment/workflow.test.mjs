import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';

const read = path => readFileSync(new URL('../../' + path, import.meta.url), 'utf8');

test('main releases follow completed CI with protected, serialized deployment', () => {
  const workflow = read('.github/workflows/release.yml');
  assert.match(workflow, /workflow_run:/);
  assert.match(workflow, /workflows: \["Continuous integration"\]/);
  assert.match(workflow, /types: \[completed\]/);
  assert.match(workflow, /name: Automatic deployment trigger/);
  assert.match(workflow, /repositories: aqua-deployment/);
  assert.doesNotMatch(workflow, /workflow_dispatch:|pull_request_target:|deploy-hook/);
  assert.doesNotMatch(workflow, /PRODUCTION_RELEASE_AUTHORIZATION|AUTHORITY_REQUIRED/);
  assert.doesNotMatch(workflow, /environment:|RENDER_API_KEY|VERCEL_TOKEN|release-control.mjs deploy/);
  assert.match(workflow, /permission-actions: write/);
  assert.match(read('tools/deployment/trigger-executor.mjs'), /aqua-deployment\/actions\/workflows\/deploy.yml\/dispatches/);
  assert.match(read('tools/deployment/release-control.mjs'), /await validateInfrastructure\(api, /);
});

test('native Vercel production trigger cannot race the qualified orchestrator', () => {
  const config = JSON.parse(read('AqualLifeStyle/9.4.2/aqua-frontend/vercel.json'));
  assert.equal(config.git.deploymentEnabled.main, false);
  assert.match(read('render.yaml'), /autoDeployTrigger: off/);
  assert.match(read('render.yaml'), /preDeployCommand: ""/);
});

test('version-tag publication requires qualification and grants no deployment permission', () => {
  const workflow = read('.github/workflows/publish-images.yml');
  assert.match(workflow, /needs: qualify/);
  assert.match(workflow, /qualify-tag/);
  assert.doesNotMatch(workflow, /environment:|RENDER_API_KEY|VERCEL_TOKEN/);
});

test('every main push runs CI and successful trusted completion starts CD without activation', () => {
  const ci = read('.github/workflows/ci.yml'), cd = read('.github/workflows/release.yml');
  assert.match(ci, /push:\s+branches: \[main, develop, "feature\/\*\*"\]/);
  assert.match(cd, /workflow_run:\s+workflows: \["Continuous integration"\]\s+types: \[completed\]\s+branches: \[main\]/);
  assert.match(cd, /github.event.workflow_run.conclusion == 'success'/);
  assert.match(cd, /github.event.workflow_run.event == 'push'/);
  assert.match(cd, /github.event.workflow_run.head_repository.full_name == github.repository/);
  assert.match(cd, /run: node tools\/deployment\/trigger-executor.mjs/);
  assert.doesNotMatch(cd, /AUTO_DEPLOY_ENABLED|workflow_dispatch:|PRODUCTION_RELEASE_AUTHORIZATION|RELEASE_TOKEN/);
  const policy = JSON.parse(read('tools/deployment/unattended-policy.json'));
  assert.equal(policy.baseline, 'automatic'); assert.equal(Object.hasOwn(policy, 'bootstrap'), false);
});

test('required container CI executes isolated runtime restart qualification and retains evidence', () => {
  const ci = read('.github/workflows/ci.yml');
  const containers = ci.split('\n  containers:\n')[1].split('\n  postgres-transactional-regression:')[0];
  assert.match(containers, /name: Validate container images/);
  assert.match(containers, /target: migrator/);
  assert.match(containers, /tags: aqualifestyle-runtime-api:qualification/);
  assert.match(containers, /tags: aqualifestyle-runtime-migrator:qualification/);
  assert.match(containers, /set -euo pipefail/);
  assert.match(containers, /node tools\/operations\/database-authority-probe\.mjs \| tee/);
  assert.match(containers, /bash tools\/deployment\/runtime-restart-probe\.sh \| tee/);
  assert.match(containers, /name: runtime-authority-\$\{\{ github.sha \}\}-/);
  assert.doesNotMatch(containers, /continue-on-error:|secrets\./);
});
