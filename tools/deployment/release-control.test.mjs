import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { test } from 'node:test';
import { clients, deploy, deploymentHistory, qualify, requiredJobs, targets, validateAuthority,
  validateEnvironment, validateHealth, validateRender, validateRun, validateVercel,
  validateVercelVariables, verifyCandidate, waitFor } from './release-control.mjs';

const source = 'c'.repeat(40), previous = 'b'.repeat(40);
const run = () => ({ id: 123, run_attempt: 1, repository: { full_name: targets.repository },
  head_repository: { full_name: targets.repository }, event: 'push', head_branch: 'main',
  head_sha: source, path: '.github/workflows/ci.yml', name: 'Continuous integration',
  status: 'completed', conclusion: 'success', html_url: 'https://github.com/example/ci/123' });
const jobs = () => requiredJobs.map(name => ({ name, status: 'completed', conclusion: 'success' }));
const qualified = () => validateRun(run(), jobs(), source);
const environment = () => ({ can_admins_bypass: false,
  deployment_branch_policy: { custom_branch_policies: true, protected_branches: false },
  protection_rules: [{ type: 'required_reviewers', prevent_self_review: true,
    reviewers: [{ type: 'User', reviewer: { id: 1 } }] }] });
const branches = () => [{ name: 'main', type: 'branch' }];
const runnerEnv = () => ({ AUTO_DEPLOY_ENABLED: 'true', GITHUB_RUN_ID: '456', GITHUB_RUN_ATTEMPT: '1' });
const authority = () => ({ profile: 'recovery-only', commit: source, ciRunId: '123', releaseRunId: '456',
  releaseRunAttempt: '1', expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
  authorityReference: 'owner/recovery-release-001', baselineRenderDeployId: 'dep-baseline',
  baselineVercelDeployId: 'dpl_Baseline', schemaCompatibilityVerified: true, rollbackVerified: true,
  effectiveRuntimeVerified: true, singleTriggerVerified: true, configurationFreezeVerified: true });
const service = () => ({ id: targets.render.id, name: targets.render.name,
  repo: 'https://github.com/' + targets.repository, branch: 'main', suspended: 'not_suspended',
  autoDeployTrigger: 'off', autoDeploy: 'no', rootDir: '', serviceDetails: {
    runtime: 'docker', healthCheckPath: '/api/health', envSpecificDetails: { dockerContext: '.',
      dockerfilePath: './docker/api/Dockerfile', preDeployCommand: '', dockerCommand: '' } } });
const heldKeys = ['App__WeeklyCommissions__Enabled', 'App__EntryMonthlyObligations__Enabled',
  'App__FinancialOperations__Enabled', 'App__BackgroundJobs__Enabled', 'AQGreenV2Demo__Enabled',
  'AQGreenV2Pilot__Enabled', 'App__AQGreenV2__Commissioning__Enabled', 'SEED_DEMO_DATA', 'Bird__Enabled'];
const variables = () => Object.entries({ ASPNETCORE_ENVIRONMENT: 'Production',
  App__Recovery__Enabled: 'true', ...Object.fromEntries(heldKeys.map(k => [k, 'false'])) })
  .map(([key, value]) => ({ envVar: { key, value } }));
const project = () => ({ id: targets.vercel.id, name: targets.vercel.name, accountId: targets.vercel.teamId,
  rootDirectory: targets.vercel.rootDirectory, nodeVersion: '24.x', autoAssignCustomDomains: true,
  link: { type: 'github', repoId: '789', repo: 'aqua-lifestyle', org: 'aqua-lifestyle', productionBranch: 'main' },
  targets: { production: { id: 'dpl_Baseline' } } });
const publicVars = () => [
  { key: 'NEXT_PUBLIC_ABP_API_URL', value: targets.vercel.publicApiUrl, target: ['production'] },
  { key: 'NEXT_PUBLIC_DEFAULT_TENANT_NAME', value: 'Default', target: ['production'] }
];
const health = sha => ({ status: 200, recovery: 'true', body: { result: { status: 'Healthy',
  buildId: sha, environment: 'Production', databaseStatus: 'Healthy', redisStatus: 'Healthy',
  dataProtectionKeyStoreStatus: 'Healthy' } } });
const immediate = { attempts: 3, pause: async () => {} };

test('successful full push inventory qualifies the exact current main source', () => {
  assert.deepEqual(qualified(), { version: 1, repository: targets.repository, sha: source,
    ciRunId: '123', ciRunAttempt: 1, ciUrl: 'https://github.com/example/ci/123' });
});
for (const [field, value] of [['conclusion', 'failure'], ['conclusion', 'cancelled'], ['status', 'in_progress'],
  ['event', 'pull_request'], ['event', 'workflow_dispatch'], ['head_branch', 'feature/unqualified'],
  ['path', '.github/workflows/unrelated.yml'], ['head_sha', 'main']]) {
  test(`unqualified CI ${field}=${value} cannot qualify a source candidate`, () => {
    assert.throws(() => validateRun({ ...run(), [field]: value }, jobs(), source));
  });
}
test('fork/repository spoofing, stale main and explicitly excluded merge fail closed', () => {
  for (const field of ['repository', 'head_repository'])
    assert.throws(() => validateRun({ ...run(), [field]: { full_name: 'untrusted/fork' } }, jobs(), source));
  assert.throws(() => validateRun(run(), jobs(), previous), /STALE/);
  assert.throws(() => validateRun({ ...run(), head_sha: targets.excludedCommits[0] }, jobs(),
    targets.excludedCommits[0]), /NOT_AUTHORIZED/);
});
test('missing, duplicate, skipped or failed CI jobs cannot be mistaken for qualification', () => {
  assert.throws(() => validateRun(run(), jobs().slice(1), source));
  const duplicate = jobs(); duplicate[0] = duplicate[1];
  assert.throws(() => validateRun(run(), duplicate, source));
  for (const outcome of ['skipped', 'failure', 'cancelled', null]) {
    const rows = jobs(); rows[0].conclusion = outcome;
    assert.throws(() => validateRun(run(), rows, source));
  }
});
test('candidate source bytes and CI identity are bound, not a mutable branch label', () => {
  const archive = Buffer.from('synthetic immutable source');
  const manifest = { ...qualified(), sourceSha256: createHash('sha256').update(archive).digest('hex') };
  verifyCandidate(manifest, qualified(), archive, archive);
  assert.throws(() => verifyCandidate(manifest, qualified(), Buffer.from('changed'), archive));
  assert.throws(() => verifyCandidate(manifest, qualified(), archive, Buffer.from('actual Git source')),
    /NOT_QUALIFIED_GIT_SOURCE/);
  for (const field of ['version', 'repository', 'sha', 'ciRunId', 'ciRunAttempt'])
    assert.throws(() => verifyCandidate({ ...manifest, [field]: 'forged' }, qualified(), archive, archive));
});
test('API qualification rejects truncated inventory even if the run says success', async () => {
  const f = fixture(); f.state.jobCount = 101;
  await assert.rejects(qualify(f.api, 123), /INVENTORY_INCOMPLETE/);
  assert.equal(f.posts.length, 0);
});
test('production activation has no permissive default', () => {
  assert.throws(() => validateAuthority(JSON.stringify(authority()), qualified(), {}), /ACTIVATION_BLOCKED/);
  assert.throws(() => validateAuthority('', qualified(), runnerEnv()), /AUTHORITY_REQUIRED/);
  assert.throws(() => validateAuthority('{secret-invalid', qualified(), runnerEnv()), /AUTHORITY_REQUIRED/);
});
for (const [field, value] of [['profile', 'normal'], ['commit', previous], ['ciRunId', '999'],
  ['releaseRunId', '999'], ['releaseRunAttempt', '2'], ['schemaCompatibilityVerified', false],
  ['rollbackVerified', false], ['effectiveRuntimeVerified', false], ['singleTriggerVerified', false],
  ['configurationFreezeVerified', false], ['baselineRenderDeployId', 'unknown'],
  ['baselineVercelDeployId', 'unknown'], ['authorityReference', 'http://unsafe.test/?token=secret']]) {
  test(`exact protected authority rejects ${field} mismatch/missing evidence`, () => {
    assert.throws(() => validateAuthority(JSON.stringify({ ...authority(), [field]: value }), qualified(), runnerEnv()));
  });
}
test('expired or unbounded authority cannot be used, valid authority is accepted', () => {
  for (const expiresAt of [null, 'invalid', new Date(Date.now() - 1).toISOString(),
    new Date(Date.now() + 86_500_000).toISOString()])
    assert.throws(() => validateAuthority(JSON.stringify({ ...authority(), expiresAt }), qualified(), runnerEnv()));
  assert.equal(validateAuthority(JSON.stringify(authority()), qualified(), runnerEnv()).commit, source);
});
test('environment requires independent reviewer, no admin bypass and main-only branch policy', () => {
  validateEnvironment(environment(), branches());
  for (const change of [{ can_admins_bypass: true }, { protection_rules: [] },
    { deployment_branch_policy: null }])
    assert.throws(() => validateEnvironment({ ...environment(), ...change }, branches()));
  const self = environment(); self.protection_rules[0].prevent_self_review = false;
  assert.throws(() => validateEnvironment(self, branches()));
  for (const rows of [[], [...branches(), { name: '*', type: 'tag' }], [{ name: '*', type: 'branch' }]])
    assert.throws(() => validateEnvironment(environment(), rows));
});
for (const key of heldKeys) {
  test(`restricted ${key} must be explicitly false, not absent or true`, () => {
    for (const value of ['true', undefined, 'unknown']) {
      const rows = variables().filter(x => x.envVar.key !== key);
      if (value !== undefined) rows.push({ envVar: { key, value } });
      assert.throws(() => validateRender(service(), rows));
    }
  });
}
for (const key of ['Yoco__Mode', 'Yoco__SecretKey', 'Yoco__WebhookSecret', 'Bird__ApiKey']) {
  test(`recovery cannot deploy with configured ${key}`, () => {
    assert.throws(() => validateRender(service(), [...variables(), { envVar: { key, value: 'synthetic' } }]));
  });
}
test('provider configuration cannot resume a service, run a hook or use a different target', () => {
  validateRender(service(), variables());
  for (const change of [{ suspended: 'suspended' }, { autoDeployTrigger: 'checksPass' },
    { autoDeploy: 'yes' }, { id: 'srv-other' }, { branch: 'develop' }, { rootDir: 'other' }])
    assert.throws(() => validateRender({ ...service(), ...change }, variables()));
  for (const field of ['preDeployCommand', 'dockerCommand', 'dockerfilePath', 'dockerContext']) {
    const s = service(); s.serviceDetails.envSpecificDetails[field] = 'unexpected';
    assert.throws(() => validateRender(s, variables()));
  }
  const rows = variables(); rows.find(x => x.envVar.key === 'App__Recovery__Enabled').envVar.value = 'false';
  assert.throws(() => validateRender(service(), rows), /RECOVERY_BASELINE/);
});
test('Vercel requires correct connected project, production branch and build settings', () => {
  validateVercel(project(), 789);
  for (const change of [{ link: null }, { id: 'prj_other' }, { accountId: 'team_other' },
    { rootDirectory: 'wrong' }, { nodeVersion: '22.x' }, { commandForIgnoringBuildStep: 'exit 0' },
    { installCommand: 'unexpected' }, { autoAssignCustomDomains: false }])
    assert.throws(() => validateVercel({ ...project(), ...change }, 789));
  assert.throws(() => validateVercel(project(), 999));
});
test('frontend public build target must be verifiable and unambiguous, not redacted or inferred', () => {
  validateVercelVariables(publicVars());
  for (const change of [{ value: '' }, { value: 'https://another-api.example.test' },
    { gitBranch: 'main' }]) {
    const vars = publicVars(); Object.assign(vars[0], change);
    assert.throws(() => validateVercelVariables(vars), /UNVERIFIED/);
  }
  assert.throws(() => validateVercelVariables(publicVars().slice(1)));
  assert.throws(() => validateVercelVariables([...publicVars(), publicVars()[0]]));
});

function fixture() {
  const state = { service: service(), vars: variables(), project: project(),
    main: source, jobCount: requiredJobs.length, files: [], renderRows: [
      { id: 'dep-baseline', status: 'live', commit: { id: previous } }], vercelRows: [],
    publicVars: publicVars(), newRender: undefined, newVercel: undefined, renderReads: 0, vercelReads: 0 };
  const posts = [], reports = [];
  const api = {
    gh: async route => {
      if (route === '') return { id: 789 };
      if (route === '/actions/runs/123') return run();
      if (route.includes('/jobs?')) return { total_count: state.jobCount, jobs: jobs() };
      if (route === '/git/ref/heads/main') return { object: { sha: state.main } };
      if (route.startsWith('/compare/')) return { status: 'ahead', files: state.files };
      throw new Error('Unexpected GitHub fixture route');
    },
    render: async (route, body) => {
      if (body) { posts.push({ provider: 'render', route, body });
        state.newRender = { id: 'dep-new', status: 'build_in_progress', commit: { id: source } };
        return structuredClone(state.newRender); }
      if (route.endsWith('/env-vars?limit=100')) return state.vars;
      if (route.endsWith('/deploys?limit=100')) return state.renderRows.map(deploy => ({ deploy }));
      if (route.endsWith('/deploys/dep-baseline')) return state.renderRows[0];
      if (route.endsWith('/deploys/dep-new')) {
        if (++state.renderReads > 1) state.newRender.status = 'live';
        return state.newRender;
      }
      return state.service;
    },
    vercel: async (route, body) => {
      if (body) { posts.push({ provider: 'vercel', route, body });
        state.newVercel = { id: 'dpl_New', readyState: 'BUILDING', target: 'production',
          projectId: targets.vercel.id, gitSource: { sha: source }, meta: { aquaCiRunId: '123' } };
        return structuredClone(state.newVercel); }
      if (route.startsWith('/v9/')) return state.project;
      if (route.startsWith('/v10/')) return { envs: state.publicVars };
      if (route.startsWith('/v6/')) return { deployments: state.vercelRows };
      if (route.endsWith('/dpl_New')) {
        if (++state.vercelReads > 1) {
          state.newVercel.readyState = 'READY'; state.project.targets.production.id = 'dpl_New';
        }
        return state.newVercel;
      }
      throw new Error('Unexpected Vercel fixture route');
    },
    health: async () => health(state.newRender?.status === 'live' ? source : previous),
    web: async () => {}
  };
  const execute = () => deploy(api, qualified(), authority(), r => reports.push(r), immediate);
  return { state, api, posts, reports, execute };
}

test('isolated rollout uses exact source, Render readiness before Vercel, and records both revisions', async () => {
  const f = fixture();
  await f.execute();
  assert.deepEqual(f.posts.map(x => x.provider), ['render', 'vercel']);
  assert.deepEqual(f.posts[0].body, { commitId: source, clearCache: 'do_not_clear' });
  assert.equal(f.posts[1].body.gitSource.sha, source);
  assert.equal(f.posts[1].body.gitSource.ref, source);
  assert.equal(f.posts[1].body.target, 'production');
  assert.deepEqual(f.reports.map(x => x.phase), ['admitted', 'render-requested', 'render-ready', 'vercel-requested', 'verified']);
  assert.deepEqual(f.reports.at(-1), { phase: 'verified', sha: source,
    renderDeployId: 'dep-new', vercelDeployId: 'dpl_New' });
});
for (const [name, modify] of [
  ['suspension', f => { f.state.service.suspended = 'suspended'; }],
  ['runtime profile', f => { f.state.vars.find(x => x.envVar.key === 'App__Recovery__Enabled').envVar.value = 'false'; }],
  ['unconnected Vercel', f => { f.state.project.link = null; }],
  ['wrong frontend API target', f => { f.state.publicVars[0].value = 'https://another-api.example.test'; }],
  ['migration', f => { f.state.files = [{ filename: 'src/EF/Migrations/New.cs' }]; }],
  ['renamed migration', f => { f.state.files = [{ filename: 'new.cs', previous_filename: 'EF/Migrations/Old.cs' }]; }],
  ['truncated comparison', f => { f.state.files = Array.from({ length: 300 }, () => ({ filename: 'source.cs' })); }],
  ['foreign Render deploy', f => { f.state.renderRows.push({ id: 'dep-other', status: 'build_in_progress', commit: { id: previous } }); }],
  ['foreign Vercel deploy', f => { f.state.vercelRows.push({ uid: 'dpl_Other', state: 'BUILDING' }); }],
  ['changed Vercel baseline', f => { f.state.project.targets.production.id = 'dpl_Other'; }],
  ['unknown Render state', f => { f.state.renderRows[0].status = 'unexpected'; }]
]) {
  test(`${name} fails before any provider mutation`, async () => {
    const f = fixture(); modify(f); await assert.rejects(f.execute()); assert.equal(f.posts.length, 0);
  });
}
test('API health must establish revision, dependencies and effective recovery before writes', async () => {
  const f = fixture(); f.api.health = async () => ({ ...health(previous), recovery: null });
  await assert.rejects(f.execute(), /READINESS/); assert.equal(f.posts.length, 0);
  assert.throws(() => validateHealth(health(previous), source));
  const noRedis = health(previous); noRedis.body.result.redisStatus = 'Unavailable';
  assert.throws(() => validateHealth(noRedis, previous));
});
test('a non-live unknown provider state cannot hide a concurrent operation', async () => {
  for (const provider of ['render', 'vercel']) {
    const f = fixture();
    if (provider === 'render') f.state.renderRows.push({ id: 'dep-unknown', status: 'future_state' });
    else f.state.vercelRows.push({ uid: 'dpl_Unknown', state: 'future_state' });
    await assert.rejects(f.execute(), /UNKNOWN_/); assert.equal(f.posts.length, 0);
  }
});
test('history pagination includes hidden concurrency and rejects repeated cursors', async () => {
  let pages = 0;
  const api = { vercel: async () => ++pages === 1 ? { deployments: [{ uid: 'dpl_Old', state: 'READY' }],
    pagination: { next: 123 } } : { deployments: [{ uid: 'dpl_New', state: 'BUILDING' }], pagination: { next: null } } };
  const rows = await deploymentHistory(api, 'vercel'); assert.equal(rows.length, 2); assert.equal(rows[1].state, 'BUILDING');
  api.vercel = async () => ({ deployments: [], pagination: { next: 123 } });
  await assert.rejects(deploymentHistory(api, 'vercel'), /CURSOR_REPEATED/);
  const render = { render: async () => Array.from({ length: 100 }, () => ({ deploy: { status: 'deactivated' } })) };
  await assert.rejects(deploymentHistory(render, 'render'), /HISTORY_INCOMPLETE/);
});
test('newer main at the final admission check cannot start a stale deployment', async () => {
  const f = fixture(); f.state.main = previous;
  await assert.rejects(f.execute(), /STALE/); assert.equal(f.posts.length, 0);
});
test('Render failure never starts Vercel or automatic rollback', async () => {
  const f = fixture(); const read = f.api.render;
  f.api.render = async (route, body) => route.endsWith('/dep-new') ?
    { id: 'dep-new', status: 'build_failed', commit: { id: source } } : read(route, body);
  await assert.rejects(f.execute(), /DEPLOYMENT_FAILED/);
  assert.deepEqual(f.posts.map(x => x.provider), ['render']);
  assert.equal(f.reports.at(-1).phase, 'render-requested');
});
test('API config drift or wrong running revision blocks the later frontend deployment', async () => {
  for (const failure of ['config', 'revision']) {
    const f = fixture(); const read = f.api.render;
    f.api.render = async (route, body) => {
      const value = await read(route, body);
      if (route.endsWith('/dep-new') && value.status === 'live') {
        if (failure === 'config') f.state.service.autoDeploy = 'yes';
        else value.commit.id = previous;
      }
      return value;
    };
    await assert.rejects(f.execute()); assert.equal(f.posts.length, 1);
  }
});
test('a newer main after API completion stops the stale frontend rollout', async () => {
  const f = fixture(); const read = f.api.health;
  f.api.health = async () => { const h = await read(); if (f.state.newRender?.status === 'live') f.state.main = previous; return h; };
  await assert.rejects(f.execute(), /STALE/); assert.equal(f.posts.length, 1);
});
test('authority expiring during API build cannot authorize the later Vercel POST', async () => {
  const f = fixture(); let checks = 0;
  await assert.rejects(deploy(f.api, qualified(), authority(), r => f.reports.push(r), {
    ...immediate, now: () => ++checks === 1 ? Date.now() : Date.now() + 86_400_000
  }), /AUTHORITY_EXPIRED/);
  assert.equal(f.posts.length, 1); assert.equal(f.posts[0].provider, 'render');
});
test('a retry reconciles the completed API and existing Vercel build without duplicate POSTs', async () => {
  const f = fixture();
  f.state.renderRows[0].status = 'deactivated';
  f.state.renderRows.unshift({ id: 'dep-new', status: 'live', commit: { id: source } });
  f.state.newRender = f.state.renderRows[0]; f.state.renderReads = 1;
  f.state.newVercel = { id: 'dpl_New', readyState: 'BUILDING', target: 'production',
    projectId: targets.vercel.id, gitSource: { sha: source }, meta: { aquaCiRunId: '123' } };
  f.state.vercelRows = [{ uid: 'dpl_New', state: 'BUILDING', meta: { aquaCiRunId: '123' } }];
  // Baseline lookup is independent of the order of current deployments.
  const read = f.api.render;
  f.api.render = async (route, body) => route.endsWith('/dep-baseline') ?
    { id: 'dep-baseline', commit: { id: previous }, status: 'deactivated' } : read(route, body);
  await f.execute(); assert.equal(f.posts.length, 0); assert.equal(f.reports.at(-1).phase, 'verified');
});
test('failed existing candidate and duplicate Vercel candidates require operator reconciliation', async () => {
  const f = fixture(); f.state.renderRows.push({ id: 'dep-new', status: 'build_failed', commit: { id: source } });
  await assert.rejects(f.execute(), /PRIOR_RENDER_FAILURE/); assert.equal(f.posts.length, 0);
  const duplicate = fixture();
  duplicate.state.newVercel = { id: 'dpl_New', readyState: 'BUILDING', target: 'production',
    projectId: targets.vercel.id, gitSource: { sha: source }, meta: { aquaCiRunId: '123' } };
  duplicate.state.vercelRows = [1, 2].map(() => ({ uid: 'dpl_New', state: 'BUILDING', meta: { aquaCiRunId: '123' } }));
  await assert.rejects(duplicate.execute(), /AMBIGUOUS/); assert.equal(duplicate.posts.length, 0);
});
test('Vercel READY alone cannot prove source or production alias assignment', async () => {
  for (const failure of ['source', 'alias']) {
    const f = fixture(); const read = f.api.vercel;
    f.api.vercel = async (route, body) => {
      const value = await read(route, body);
      if (route.endsWith('/dpl_New') && value.readyState === 'READY') {
        if (failure === 'source') value.gitSource.sha = previous;
        else f.state.project.targets.production.id = 'dpl_Baseline';
      }
      return value;
    };
    await assert.rejects(f.execute()); assert.notEqual(f.reports.at(-1).phase, 'verified');
  }
});
test('timeout and unrecognized provider states preserve failure, never success', async () => {
  await assert.rejects(waitFor(async () => ({ s: 'BUILDING' }), x => x.s, 'READY', active(), immediate), /TIMEOUT/);
  await assert.rejects(waitFor(async () => ({ s: 'unknown' }), x => x.s, 'READY', active(), immediate), /FAILED/);
  function active() { return new Set(['BUILDING']); }
});
test('provider client never retries ambiguous POST or exposes tokens and response bodies', async () => {
  const calls = [], token = 'synthetic-token-never-log';
  const api = clients({ RENDER_API_KEY: token }, async (...args) => {
    calls.push(args); throw new Error(token + ' sensitive response');
  });
  await assert.rejects(api.render('/services/test/deploys', { commitId: source }),
    error => error.message === 'AMBIGUOUS_PROVIDER_POST_INSPECT_BEFORE_RETRY');
  assert.equal(calls.length, 1); assert.equal(calls[0][1].redirect, 'error');
  const denied = clients({ VERCEL_TOKEN: token }, async () => new Response(token, { status: 403 }));
  await assert.rejects(denied.vercel('/v13/deployments', {}), error => !error.message.includes(token));
});
