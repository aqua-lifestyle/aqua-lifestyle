import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';

const read = path => readFileSync(new URL('../../' + path, import.meta.url), 'utf8');

test('main releases follow completed CI with protected, serialized deployment', () => {
  const workflow = read('.github/workflows/release.yml');
  assert.match(workflow, /workflow_run:/);
  assert.match(workflow, /workflows: \["Continuous integration"\]/);
  assert.match(workflow, /types: \[completed\]/);
  assert.match(workflow, /environment: production-release/);
  assert.match(workflow, /cancel-in-progress: false/);
  assert.doesNotMatch(workflow, /workflow_dispatch:|pull_request_target:|deploy-hook/);
  assert.doesNotMatch(workflow, /PRODUCTION_RELEASE_AUTHORIZATION|AUTHORITY_REQUIRED/);
  assert.match(workflow, /pull-requests: read/);
  assert.match(workflow, /RESUME_RECEIPT_DIR:/);
  assert.match(read('tools/deployment/release-control.mjs'), /await validateInfrastructure\(api\);/);
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
  assert.match(cd, /needs: candidate/);
  assert.doesNotMatch(cd, /AUTO_DEPLOY_ENABLED|workflow_dispatch:|PRODUCTION_RELEASE_AUTHORIZATION|RELEASE_TOKEN/);
  const policy = JSON.parse(read('tools/deployment/unattended-policy.json'));
  assert.equal(policy.baseline, 'automatic'); assert.equal(Object.hasOwn(policy, 'bootstrap'), false);
});
