import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { test } from 'node:test';
import { auditArtifact, clients, createAuditWriter, deploy, deploymentHistory, evidencePlan, freezeCandidate,
  main, policyDigest, qualify, requalify, releaseKey, requiredJobs, run as runRelease,
  standingPolicy, targets, validatePolicy, validateCandidateReview,
  validateBaselineReceipt, validateInfrastructure, validateResumeReceipt,
  validateEnvironment, validateHealth, validateRender, validateRun, validateVercel,
  validateVercelVariables, verifyCandidate, waitFor } from './release-control.mjs';

const source = 'c'.repeat(40), previous = 'b'.repeat(40);
const run = () => ({ id: 123, run_attempt: 1, repository: { full_name: targets.repository },
  head_repository: { full_name: targets.repository }, event: 'push', head_branch: 'main',
  head_sha: source, path: '.github/workflows/ci.yml', name: 'Continuous integration',
  status: 'completed', conclusion: 'success', html_url: 'https://github.com/example/ci/123' });
const jobs = (attempt = 1) => requiredJobs.map(name => ({ name, status: 'completed', conclusion: 'success',
  run_id: 123, run_attempt: attempt }));
const ciQualified = () => validateRun(run(), jobs(), source);
const qualified = () => freezeCandidate(ciQualified(), runnerEnv(), policy());
const environment = () => ({ name: targets.environment, can_admins_bypass: false,
  deployment_branch_policy: { custom_branch_policies: true, protected_branches: false },
  protection_rules: [{ type: 'branch_policy' }] });
const branches = () => [{ name: 'main', type: 'branch' }];
const runnerEnv = (attempt = 1) => ({ AUTO_DEPLOY_ENABLED: 'true', GITHUB_REPOSITORY: targets.repository,
  GITHUB_RUN_ID: '456', GITHUB_RUN_ATTEMPT: String(attempt), GITHUB_REF: 'refs/heads/main',
  GITHUB_WORKFLOW_REF: `${targets.repository}/.github/workflows/release.yml@refs/heads/main`,
  RENDER_API_KEY: 'synthetic-render-key', VERCEL_TOKEN: 'synthetic-vercel-token' });
const policy = () => ({ ...standingPolicy, bootstrap: { renderDeployId: 'dep-baseline', renderSha: previous,
  vercelDeployId: 'dpl_Baseline', vercelSha: previous, releaseFloor: 1,
  evidenceReference: 'isolated/qualified-bootstrap' } });
const governance = () => [
  { type: 'deletion' }, { type: 'non_fast_forward' },
  { type: 'pull_request', parameters: { required_approving_review_count: 1,
    dismiss_stale_reviews_on_push: true, require_last_push_approval: true, required_review_thread_resolution: true } },
  { type: 'required_status_checks', parameters: { strict_required_status_checks_policy: true,
    required_status_checks: requiredJobs.map(context => ({ context, integration_id: 15368 })) } }
].map(r => ({ ...r, ruleset_source_type: 'Repository', ruleset_id: 1 }));
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
  assert.deepEqual(ciQualified(), { version: 1, repository: targets.repository, sha: source,
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
  for (const field of ['version', 'repository', 'sha', 'ciRunId', 'ciRunAttempt', 'releaseRunId',
    'releaseRunAttempt', 'environment', 'profile', 'renderServiceId', 'vercelProjectId', 'vercelTeamId'])
    assert.throws(() => verifyCandidate({ ...manifest, [field]: 'forged' }, qualified(), archive, archive));
});
test('API qualification rejects truncated inventory even if the run says success', async () => {
  const f = fixture(); f.state.jobCount = 101;
  await assert.rejects(qualify(f.api, 123), /INVENTORY_INCOMPLETE/);
  assert.equal(f.posts.length, 0);
});
test('standing deployment activation has no permissive default', () => {
  for (const value of [undefined, 'false', 'TRUE'])
    assert.throws(() => validatePolicy(qualified(), { ...runnerEnv(), AUTO_DEPLOY_ENABLED: value }, policy()), /STANDING_SWITCH/);
  assert.throws(() => validatePolicy(freezeCandidate(ciQualified(), runnerEnv()), runnerEnv()), /BOOTSTRAP_REQUIRED/);
  for (const field of ['RENDER_API_KEY', 'VERCEL_TOKEN'])
    assert.throws(() => validatePolicy(qualified(), { ...runnerEnv(), [field]: '' }, policy()), /CREDENTIALS_REQUIRED/);
});
for (const [field, value] of [['version', 2], ['epoch', 'different-policy'], ['epoch', undefined],
  ['profile', 'normal'], ['bootstrap', null], ['unexpectedPermission', true]]) {
  test(`standing policy rejects ${field}=${JSON.stringify(value)} mismatch/missing evidence`, () => {
    assert.throws(() => validatePolicy(qualified(), runnerEnv(), { ...policy(), [field]: value }));
  });
}
for (const [field, value] of [['renderDeployId', 'unknown'], ['vercelDeployId', 'unknown'], ['renderSha', 'main'],
  ['vercelSha', 'main'], ['releaseFloor', -1], ['releaseFloor', undefined], ['evidenceReference', 'http://unsafe/?secret']]) {
  test(`bootstrap rejects unverifiable ${field}=${JSON.stringify(value)}`, () => {
    const p = policy(); p.bootstrap[field] = value;
    assert.throws(() => validatePolicy(freezeCandidate(ciQualified(), runnerEnv(), p), runnerEnv(), p), /BOOTSTRAP_REQUIRED/);
  });
}
test('standing policy is source-bound and needs no expiring per-release token', () => {
  const setup = validatePolicy(qualified(), runnerEnv(), policy());
  assert.equal(setup.renderSha, previous); assert(Object.isFrozen(setup));
  assert.equal(qualified().policySha256, policyDigest(policy()));
});
test('unattended environment forbids manual gates and admin bypass and requires main-only branch policy', () => {
  validateEnvironment(environment(), branches());
  for (const change of [{ can_admins_bypass: true }, { protection_rules: [{ type: 'required_reviewers' }] },
    { protection_rules: [{ type: 'wait_timer' }] }, { protection_rules: [{ type: 'custom' }] },
    { name: 'Production' },
    { deployment_branch_policy: null }])
    assert.throws(() => validateEnvironment({ ...environment(), ...change }, branches()));
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
    candidate: qualified(), policy: policy(), env: runnerEnv(), receiptRows: undefined, baselineRows: undefined,
    rules: governance(), ruleset: { id: 1, enforcement: 'active', target: 'branch', bypass_actors: [] },
    environment: environment(), branches: branches(),
    pr: { number: 1, merged: true, state: 'closed', merge_commit_sha: source,
      base: { ref: 'main', repo: { full_name: targets.repository } }, head: { sha: 'd'.repeat(40) },
      user: { login: 'author', id: 1 }, merged_at: '2026-10-16T10:00:00Z' },
    reviews: [{ id: 1, user: { login: 'reviewer', id: 2, type: 'User' }, state: 'APPROVED',
      commit_id: 'd'.repeat(40), submitted_at: '2026-10-16T09:00:00Z' }],
    reviewerPermission: { permission: 'write', user: { id: 2, login: 'reviewer' } },
    publicVars: publicVars(), newRender: undefined, newVercel: undefined, renderReads: 0, vercelReads: 0 };
  const posts = [], reports = [];
  const api = {
    gh: async route => {
      if (route === '') return { id: 789 };
      if (route === '/actions/runs/123') return run();
      if (route.includes('/jobs?')) return { total_count: state.jobCount, jobs: jobs() };
      if (route === '/git/ref/heads/main') return { object: { sha: state.main } };
      if (route.startsWith('/compare/')) return { status: 'ahead', files: state.files };
      if (route === '/rules/branches/main?per_page=100') return state.rules;
      if (route === '/rulesets/1') return state.ruleset;
      if (route.endsWith('/deployment-branch-policies?per_page=100'))
        return { total_count: state.branches.length, branch_policies: state.branches };
      if (route === `/environments/${targets.environment}`) return state.environment;
      if (route.startsWith('/commits/') && route.endsWith('/pulls?per_page=100')) return [state.pr];
      if (route === '/pulls/1') return structuredClone(state.pr);
      if (route === '/pulls/1/reviews?per_page=100') return state.reviews;
      if (route.startsWith('/collaborators/')) return state.reviewerPermission;
      throw new Error('Unexpected GitHub fixture route');
    },
    render: async (route, body) => {
      if (body) { posts.push({ provider: 'render', route, body });
        state.newRender = { id: 'dep-new', status: 'build_in_progress', commit: { id: source } };
        state.renderRows.unshift(state.newRender);
        return structuredClone(state.newRender); }
      if (route.endsWith('/env-vars?limit=100')) return state.vars;
      if (route.endsWith('/deploys?limit=100')) return state.renderRows.map(deploy => ({ deploy }));
      if (route.endsWith('/deploys/dep-baseline')) return state.renderRows.find(x => x.id === 'dep-baseline');
      if (route.endsWith('/deploys/dep-new')) {
        if (++state.renderReads > 1) {
          state.newRender.status = 'live';
          for (const row of state.renderRows) if (row.id !== state.newRender.id && row.status === 'live') row.status = 'deactivated';
        }
        return state.newRender;
      }
      return state.service;
    },
    vercel: async (route, body) => {
      if (body) { posts.push({ provider: 'vercel', route, body });
        state.newVercel = { id: 'dpl_New', readyState: 'BUILDING', target: 'production',
          projectId: targets.vercel.id, gitSource: { sha: source, repoId: '789' }, meta: body.meta };
        return structuredClone(state.newVercel); }
      if (route.startsWith('/v9/')) return state.project;
      if (route.startsWith('/v10/')) return { envs: state.publicVars };
      if (route.startsWith('/v6/')) return { deployments: state.vercelRows };
      if (route.endsWith('/dpl_Baseline')) return { id: 'dpl_Baseline', projectId: targets.vercel.id,
        target: 'production', readyState: 'READY', gitSource: { sha: previous, repoId: '789' } };
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
  const execute = async () => {
    return deploy(api, state.candidate, state.policy,
      createAuditWriter(state.candidate, r => reports.push(r)), { ...immediate, env: state.env,
        evidencePlan: state.plan ?? { policySha256: state.candidate.policySha256, baseline: null, resume: null },
        receiptRows: state.receiptRows, baselineRows: state.baselineRows });
  };
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
  assert.deepEqual(f.reports.map(x => x.phase), ['baseline-selected', 'admitted', 'render-requesting',
    'render-requested', 'render-ready', 'vercel-requesting', 'vercel-requested', 'verified']);
  const { ciUrl, ...identity } = qualified();
  assert.deepEqual(f.reports.at(-1), { phase: 'verified', ...identity,
    renderDeployId: 'dep-new', vercelDeployId: 'dpl_New',
    baselineRenderDeployId: 'dep-baseline', baselineVercelDeployId: 'dpl_Baseline',
    baselineRenderSha: previous, baselineVercelSha: previous,
    renderReleaseRunAttempt: 1, vercelReleaseRunAttempt: 1 });
});
test('qualified main deploys with standing setup and no per-release authority', async () => {
  const f = fixture();
  assert.equal(Object.hasOwn(f.state.env, 'PRODUCTION_RELEASE_AUTHORIZATION'), false);
  await f.execute();
  assert.deepEqual(f.posts.map(x => x.provider), ['render', 'vercel']);
  assert.equal(f.reports.at(-1).phase, 'verified');
});
test('unattended environment needs no human approval', () => {
  validateEnvironment({ ...environment(), protection_rules: [] }, branches());
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
for (const boundary of ['before Render', 'after Render']) {
  test(`frozen CI attempt rejects a successful rerun ${boundary}`, async () => {
    const f = fixture(); const read = f.api.gh;
    f.api.gh = async route => {
      const value = await read(route);
      if (route.includes('/attempts/2/jobs')) return { total_count: requiredJobs.length, jobs: jobs(2) };
      if (route === '/actions/runs/123' &&
          (boundary === 'before Render' || f.state.newRender?.status === 'live'))
        return { ...value, run_attempt: 2 };
      return value;
    };
    await assert.rejects(f.execute(), /CI_QUALIFICATION_CHANGED/);
    assert.deepEqual(f.posts.map(x => x.provider), boundary === 'before Render' ? [] : ['render']);
    if (boundary === 'after Render') {
      assert.equal(f.reports.at(-1).phase, 'render-ready');
      assert.equal(f.reports.at(-1).renderDeployId, 'dep-new');
    }
  });
}
test('governance revocation during API build blocks the later Vercel POST', async () => {
  const f = fixture(), read = f.api.health;
  f.api.health = async () => { const value = await read();
    if (f.state.newRender?.status === 'live')
      f.state.rules.find(r => r.type === 'pull_request').parameters.required_approving_review_count = 0;
    return value;
  };
  await assert.rejects(f.execute(), /MAIN_PR_AND_CI_GOVERNANCE_REQUIRED/);
  assert.equal(f.posts.length, 1); assert.equal(f.posts[0].provider, 'render');
});
function resumeFixture({ includeVercel = true } = {}) {
  const f = fixture();
  f.state.candidate = freezeCandidate(ciQualified(), runnerEnv(2), f.state.policy);
  f.state.env = runnerEnv(2);
  f.state.plan = { policySha256: f.state.candidate.policySha256, baseline: null,
    resume: { artifactId: '789', runId: '456', runAttempt: 1, sha: source } };
  const receiptRows = [];
  const record = createAuditWriter(qualified(), row => receiptRows.push(row));
  record({ phase: 'render-requested', renderDeployId: 'dep-new', renderReleaseRunAttempt: 1 });
  f.state.renderRows[0].status = 'deactivated';
  f.state.renderRows.unshift({ id: 'dep-new', status: 'live', commit: { id: source } });
  f.state.newRender = f.state.renderRows[0]; f.state.renderReads = 1;
  if (includeVercel) {
    const meta = { aquaCiRunId: '123', aquaCiRunAttempt: '1', aquaSourceSha: source,
      aquaReleaseIdentity: releaseKey(qualified()) };
    f.state.newVercel = { id: 'dpl_New', readyState: 'BUILDING', target: 'production',
      projectId: targets.vercel.id, gitSource: { sha: source, repoId: '789' }, meta };
    f.state.vercelRows = [{ uid: 'dpl_New', state: 'BUILDING', meta }];
    record({ phase: 'vercel-requested', vercelDeployId: 'dpl_New', vercelReleaseRunAttempt: 1 });
  }
  record({ phase: 'failed', code: 'PROVIDER_TIMEOUT_RECONCILE_BEFORE_RETRY' });
  f.state.receiptRows = receiptRows;
  // Baseline lookup is independent of the order of current deployments.
  const read = f.api.render;
  f.api.render = async (route, body) => route.endsWith('/dep-baseline') ?
    { id: 'dep-baseline', commit: { id: previous }, status: 'deactivated' } : read(route, body);
  return f;
}
test('a retry reconciles the completed API and existing Vercel build without duplicate POSTs', async () => {
  const f = resumeFixture();
  await f.execute(); assert.equal(f.posts.length, 0); assert.equal(f.reports.at(-1).phase, 'verified');
  assert.equal(f.reports.at(-1).ciRunAttempt, 1);
  assert.equal(f.reports.at(-1).releaseRunAttempt, 2);
  assert.equal(f.reports.at(-1).renderReleaseRunAttempt, 1);
  assert.equal(f.reports.at(-1).vercelReleaseRunAttempt, 1);
});
test('failed existing candidate and duplicate Vercel candidates require operator reconciliation', async () => {
  const f = resumeFixture(); f.state.renderRows[0].status = 'build_failed'; f.state.renderRows[1].status = 'live';
  await assert.rejects(f.execute(), /PRIOR_RENDER_FAILURE/); assert.equal(f.posts.length, 0);
  const duplicate = resumeFixture(); duplicate.state.vercelRows.push({ ...duplicate.state.vercelRows[0] });
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

for (const attempt of [undefined, null, 0, -1, 1.5, '1']) {
  test(`missing or invalid CI attempt ${String(attempt)} cannot qualify`, () => {
    assert.throws(() => validateRun({ ...run(), run_attempt: attempt }, jobs(), source), /CI_IDENTITY_UNVERIFIABLE/);
  });
}
test('attempt-specific job evidence cannot borrow earlier successes or another run identity', async () => {
  assert.throws(() => validateRun({ ...run(), run_attempt: 2 }, jobs(1), source), /CI_INVENTORY_NOT_QUALIFIED/);
  const otherRun = jobs(); otherRun[0].run_id = 999;
  assert.throws(() => validateRun(run(), otherRun, source), /CI_INVENTORY_NOT_QUALIFIED/);
  const f = fixture(); const seen = [], read = f.api.gh;
  f.api.gh = async route => { seen.push(route); return read(route); };
  await qualify(f.api, 123);
  assert(seen.includes('/actions/runs/123/attempts/1/jobs?per_page=100'));
  assert(!seen.some(route => route.includes('filter=latest')));
});
test('a rerun starting while job evidence is read invalidates the snapshot', async () => {
  const f = fixture(), read = f.api.gh; let reads = 0;
  f.api.gh = async route => { const value = await read(route);
    return route === '/actions/runs/123' && ++reads > 1 ? { ...value, run_attempt: 2 } : value; };
  await assert.rejects(f.execute(), /CI_QUALIFICATION_CHANGED/);
  assert.equal(f.posts.length, 0);
});
for (const boundary of ['before Render', 'after Render']) {
  for (const failure of ['failed rerun', 'changed run', 'changed source', 'missing attempt', 'unavailable evidence']) {
    test(`${failure} ${boundary} stops all subsequent provider mutations`, async () => {
      const f = fixture(), read = f.api.gh;
      f.api.gh = async route => {
        const changed = boundary === 'before Render' || f.state.newRender?.status === 'live';
        if (changed && failure === 'unavailable evidence') throw new Error('mock unavailable');
        const value = await read(route);
        if (!changed) return value;
        if (route === '/actions/runs/123') {
          if (failure === 'failed rerun') return { ...value, run_attempt: 2, conclusion: 'failure' };
          if (failure === 'changed run') return { ...value, id: 999 };
          if (failure === 'changed source') return { ...value, head_sha: previous };
          if (failure === 'missing attempt') return { ...value, run_attempt: undefined };
        }
        if (failure === 'failed rerun' && route.includes('/attempts/2/jobs'))
          return { total_count: requiredJobs.length, jobs: jobs(2) };
        if (failure === 'changed source' && route === '/git/ref/heads/main') return { object: { sha: previous } };
        return value;
      };
      await assert.rejects(f.execute());
      assert.deepEqual(f.posts.map(x => x.provider), boundary === 'before Render' ? [] : ['render']);
      if (boundary === 'after Render') assert.equal(f.reports.at(-1).renderDeployId, 'dep-new');
    });
  }
}
test('candidate cannot be replayed into a later release or attempt despite standing permission', async () => {
  const original = qualified(), archive = Buffer.from('source');
  const manifest = { ...original, sourceSha256: createHash('sha256').update(archive).digest('hex') };
  const retry = freezeCandidate(ciQualified(), runnerEnv(2), policy());
  assert.throws(() => verifyCandidate(manifest, retry, archive, archive), /PROVENANCE_MISMATCH/);
  assert.throws(() => validatePolicy(original, runnerEnv(2), policy()), /RELEASE_CONTEXT_REQUIRED/);
  const second = freezeCandidate(ciQualified(), { ...runnerEnv(), GITHUB_RUN_ID: '999' }, policy());
  assert.throws(() => verifyCandidate(manifest, second, archive, archive), /PROVENANCE_MISMATCH/);
  const f = fixture(); f.state.candidate = { ...f.state.candidate, ciRunAttempt: 2 };
  await assert.rejects(f.execute(), /CI_QUALIFICATION_CHANGED/); assert.equal(f.posts.length, 0);
});
test('a changed candidate CI attempt cannot reuse an immutable partial receipt', async () => {
  const f = resumeFixture();
  f.state.candidate = freezeCandidate({ ...ciQualified(), ciRunAttempt: 2 }, runnerEnv(2), f.state.policy);
  const read = f.api.gh;
  f.api.gh = async route => route === '/actions/runs/123' ? { ...run(), run_attempt: 2 } :
    route.includes('/attempts/2/jobs') ? { total_count: requiredJobs.length, jobs: jobs(2) } : read(route);
  await assert.rejects(f.execute(), /RESUME_RECEIPT_IDENTITY_MISMATCH/);
  assert.equal(f.posts.length, 0);
});
test('same-attempt partial Render completion can resume only the pending Vercel operation', async () => {
  const f = resumeFixture({ includeVercel: false });
  await f.execute();
  assert.deepEqual(f.posts.map(x => x.provider), ['vercel']);
  assert.equal(f.posts[0].body.meta.aquaCiRunAttempt, '1');
  assert.equal(f.posts[0].body.meta.aquaReleaseIdentity, releaseKey(f.state.candidate));
  assert.equal(f.reports.at(-1).renderReleaseRunAttempt, 1);
  assert.equal(f.reports.at(-1).vercelReleaseRunAttempt, 2);
});
test('Render completion plus a successful CI rerun blocks Vercel retry admission', async () => {
  const f = resumeFixture({ includeVercel: false }), read = f.api.gh;
  f.api.gh = async route => route === '/actions/runs/123' ? { ...run(), run_attempt: 2 } :
    route.includes('/attempts/2/jobs') ? { total_count: requiredJobs.length, jobs: jobs(2) } : read(route);
  await assert.rejects(f.execute(), /CI_QUALIFICATION_CHANGED/);
  assert.equal(f.posts.length, 0);
});
for (const mismatch of ['ciRunAttempt', 'releaseRunId', 'releaseRunAttempt', 'environment', 'renderServiceId']) {
  test(`retry receipt with another ${mismatch} cannot adopt any provider deployment`, async () => {
    const f = resumeFixture();
    f.state.receiptRows = f.state.receiptRows.map(row => ({ ...row,
      [mismatch]: typeof row[mismatch] === 'number' ? row[mismatch] + 1 : 'another-value' }));
    await assert.rejects(f.execute()); assert.equal(f.posts.length, 0);
  });
}
test('contradictory, legacy, completed or unsolicited receipts cannot authorize another release', async () => {
  for (const kind of ['contradictory', 'legacy', 'completed', 'unsolicited', 'missing', 'future-origin']) {
    const f = resumeFixture();
    if (kind === 'contradictory') f.state.receiptRows.at(-1).renderDeployId = 'dep-another';
    if (kind === 'legacy') f.state.receiptRows.forEach(row => { delete row.ciRunAttempt; });
    if (kind === 'completed') f.state.receiptRows.at(-1).phase = 'verified';
    if (kind === 'unsolicited') { f.state.candidate = qualified(); f.state.env = runnerEnv(); }
    if (kind === 'missing') f.state.receiptRows = undefined;
    if (kind === 'future-origin') f.state.receiptRows.forEach(row => { row.renderReleaseRunAttempt = 2; });
    await assert.rejects(f.execute()); assert.equal(f.posts.length, 0);
  }
});
test('CI attempt metadata and original release identity must match even with a valid receipt', async () => {
  for (const field of ['aquaCiRunAttempt', 'aquaReleaseIdentity', 'aquaCiRunId']) {
    const f = resumeFixture(); f.state.newVercel.meta[field] = 'different';
    await assert.rejects(f.execute()); assert.equal(f.posts.length, 0);
  }
});
test('an ambiguous POST without a recorded provider ID cannot be retried by inferring from SHA', async () => {
  const f = resumeFixture({ includeVercel: false });
  f.state.receiptRows = f.state.receiptRows.map(row => {
    const { renderDeployId, renderReleaseRunAttempt, ...withoutId } = row; return withoutId;
  });
  await assert.rejects(f.execute(), /RENDER_BASELINE_DRIFT|PROVENANCE_RECEIPT/);
  assert.equal(f.posts.length, 0);
});
test('a foreign concurrent release cannot be adopted even when its SHA and CI run match', async () => {
  const f = resumeFixture(); f.state.vercelRows.push({ uid: 'dpl_Foreign', state: 'BUILDING',
    meta: { ...f.state.newVercel.meta } });
  await assert.rejects(f.execute(), /CONCURRENT_VERCEL/); assert.equal(f.posts.length, 0);
  const render = resumeFixture(); render.state.renderRows.push({ id: 'dep-foreign', status: 'build_in_progress',
    commit: { id: source } });
  await assert.rejects(render.execute(), /CONCURRENT_RENDER/); assert.equal(render.posts.length, 0);
});
test('every partial/failure audit retains the immutable identity and already accepted deployment IDs', () => {
  const rows = [], writer = createAuditWriter(qualified(), row => rows.push(row));
  writer({ phase: 'render-requested', renderDeployId: 'dep-new', renderReleaseRunAttempt: 1 });
  writer({ phase: 'failed', code: 'CI_QUALIFICATION_CHANGED', ciRunAttempt: 999 });
  assert.equal(rows[1].ciRunAttempt, 1); assert.equal(rows[1].renderDeployId, 'dep-new');
  assert.equal(rows[1].renderReleaseRunAttempt, 1); assert.equal(rows[1].environment, targets.environment);
});
test('resume requires a unique, unexpired artifact from the exact earlier attempt of this release run', async () => {
  const candidate = freezeCandidate(ciQualified(), runnerEnv(2), policy());
  const producing = { id: 456, head_sha: source };
  const artifact = { id: 789, name: 'production-release-audit-456-1', expired: false,
    workflow_run: { id: 456, head_sha: source } };
  const api = { gh: async route => { assert.equal(route, '/actions/runs/456/artifacts?per_page=100');
    return { total_count: 1, artifacts: [artifact] }; } };
  assert.deepEqual(await auditArtifact(api, producing, 1),
    { artifactId: '789', runId: '456', runAttempt: 1, sha: source });
  for (const change of [{ expired: true }, { name: 'production-release-audit-999-1' },
    { workflow_run: { id: 999, head_sha: source } }, { workflow_run: { id: 456, head_sha: previous } }]) {
    const bad = { gh: async () => ({ total_count: 1, artifacts: [{ ...artifact, ...change }] }) };
    await assert.rejects(auditArtifact(bad, producing, 1));
  }
  await assert.rejects(auditArtifact({ gh: async () => ({ total_count: 2, artifacts: [artifact, artifact] }) },
    producing, 1), /TRUSTED_AUDIT/);
  await assert.rejects(auditArtifact({ gh: async () => ({ total_count: 101, artifacts: [artifact] }) },
    producing, 1), /INVENTORY_INCOMPLETE/);
});

const releaseRun = (overrides = {}) => ({ id: 456, run_attempt: 1, run_number: 2,
  head_sha: source, event: 'workflow_run', head_branch: 'main',
  repository: { full_name: targets.repository }, head_repository: { full_name: targets.repository },
  path: '.github/workflows/release.yml', name: 'Qualified production release',
  status: 'in_progress', conclusion: null, ...overrides });
function planFixture(candidate = qualified()) {
  const current = releaseRun({ run_attempt: candidate.releaseRunAttempt, head_sha: candidate.sha });
  const state = { current, runs: [current], artifacts: [] };
  const api = { gh: async route => {
    if (route === '/actions/runs/456') return structuredClone(state.current);
    if (route.includes('/workflows/release.yml/runs?'))
      return { total_count: state.runs.length, workflow_runs: structuredClone(state.runs) };
    if (route.includes('/artifacts?'))
      return { total_count: state.artifacts.length, artifacts: structuredClone(state.artifacts) };
    throw new Error('unexpected evidence route');
  } };
  return { state, api };
}
const artifactFor = (run, attempt = run.run_attempt) => ({ id: 789,
  name: `production-release-audit-${run.id}-${attempt}`, expired: false,
  workflow_run: { id: run.id, head_sha: run.head_sha } });

test('standing policy selects bootstrap once, then the immutable verified predecessor automatically', async () => {
  const f = planFixture();
  assert.deepEqual(await evidencePlan(f.api, qualified(), policy()),
    { policySha256: qualified().policySha256, baseline: null, resume: null, resumeChain: [] });
  const previousRun = releaseRun({ id: 455, run_number: 2, head_sha: previous,
    status: 'completed', conclusion: 'success' });
  f.state.current.run_number = 3; f.state.runs = [f.state.current, previousRun];
  f.state.artifacts = [artifactFor(previousRun)];
  const selected = await evidencePlan(f.api, qualified(), policy());
  assert.deepEqual(selected.baseline, { artifactId: '789', runId: '455', runAttempt: 1, sha: previous });
  assert.equal(selected.resume, null);
});
test('own previous-attempt receipt is selected automatically without a resume token', async () => {
  const candidate = freezeCandidate(ciQualified(), runnerEnv(2), policy());
  const f = planFixture(candidate); f.state.artifacts = [artifactFor(f.state.current, 1)];
  assert.deepEqual((await evidencePlan(f.api, candidate, policy())).resume,
    { artifactId: '789', runId: '456', runAttempt: 1, sha: source });
});
for (const kind of ['deleted receipt', 'expired receipt', 'truncated history', 'deleted run',
  'foreign workflow', 'fork', 'duplicate run', 'changed release attempt', 'newer completed release']) {
  test(`evidence discovery rejects ${kind} without resetting to bootstrap`, async () => {
    const f = planFixture();
    const old = releaseRun({ id: 455, run_number: 2, head_sha: previous, status: 'completed', conclusion: 'success' });
    f.state.current.run_number = 3; f.state.runs = [f.state.current, old];
    f.state.artifacts = [artifactFor(old)];
    if (kind === 'deleted receipt') f.state.artifacts = [];
    if (kind === 'expired receipt') f.state.artifacts[0].expired = true;
    if (kind === 'truncated history') f.api.gh = async () => ({ ...f.state.current, total_count: 101, workflow_runs: [] });
    if (kind === 'deleted run') f.state.runs.pop();
    if (kind === 'foreign workflow') old.path = '.github/workflows/other.yml';
    if (kind === 'fork') old.head_repository = { full_name: 'untrusted/fork' };
    if (kind === 'duplicate run') f.state.runs.push(old);
    if (kind === 'changed release attempt') f.state.current.run_attempt = 2;
    if (kind === 'newer completed release') old.run_number = 4;
    await assert.rejects(evidencePlan(f.api, qualified(), policy()));
  });
}

test('a second qualified commit uses the verified receipt baseline without new owner approval', async () => {
  const first = fixture(); await first.execute();
  const second = fixture(), predecessor = first.reports.at(-1);
  const next = 'e'.repeat(40);
  second.state.candidate = freezeCandidate({ ...ciQualified(), sha: next, ciRunId: '124' },
    { ...runnerEnv(), GITHUB_RUN_ID: '457' }, second.state.policy);
  second.state.env = { ...runnerEnv(), GITHUB_RUN_ID: '457' };
  second.state.baselineRows = first.reports;
  second.state.plan = { policySha256: second.state.candidate.policySha256,
    baseline: { artifactId: '789', runId: '456', runAttempt: 1, sha: source }, resume: null };
  second.state.renderRows = [{ id: 'dep-new', status: 'live', commit: { id: source } }];
  second.state.project.targets.production.id = 'dpl_New';
  const gh = second.api.gh;
  second.api.gh = async route => route === '/actions/runs/124' ? { ...run(), id: 124, head_sha: next } :
    route.includes('/attempts/1/jobs') ? { total_count: requiredJobs.length,
      jobs: jobs().map(j => ({ ...j, run_id: 124 })) } :
    route === '/git/ref/heads/main' ? { object: { sha: next } } :
    route.startsWith('/commits/') ? [{ ...second.state.pr, merge_commit_sha: next }] :
    route === '/pulls/1' ? { ...second.state.pr, merge_commit_sha: next } : gh(route);
  const render = second.api.render, web = second.api.vercel;
  second.api.render = async (route, body) => {
    if (route.endsWith('/dep-new')) return { id: 'dep-new', status: 'deactivated', commit: { id: source } };
    if (route.endsWith('/dep-next')) return { id: 'dep-next', status: 'live', commit: { id: next } };
    if (body) {
      second.posts.push({ provider: 'render', body });
      second.state.renderRows[0].status = 'deactivated';
      second.state.renderRows.unshift({ id: 'dep-next', status: 'live', commit: { id: next } });
      return second.state.renderRows[0];
    }
    return render(route, body);
  };
  second.api.vercel = async (route, body) => {
    if (route.endsWith('/dpl_New')) return { ...first.state.newVercel, readyState: 'READY' };
    if (body) {
      second.posts.push({ provider: 'vercel', body });
      second.state.project.targets.production.id = 'dpl_Next';
      second.state.newVercel = { id: 'dpl_Next', target: 'production', readyState: 'READY',
        projectId: targets.vercel.id, gitSource: { sha: next, repoId: '789' }, meta: body.meta };
      return second.state.newVercel;
    }
    if (route.endsWith('/dpl_Next')) return second.state.newVercel;
    return web(route, body);
  };
  second.api.health = async () => health(second.posts.length ? next : source);
  await second.execute();
  assert.deepEqual(second.posts.map(p => p.provider), ['render', 'vercel']);
  assert.equal(second.reports.at(-1).baselineRenderDeployId, predecessor.renderDeployId);
  assert.equal(second.reports.at(-1).sha, next);
  assert.equal(second.reports.at(-1).ciRunId, '124');
});
for (const kind of ['not verified', 'wrong run', 'changed policy', 'contradictory ID', 'missing origin']) {
  test(`verified predecessor rejects ${kind}`, async () => {
    const f = fixture(); await f.execute();
    const rows = structuredClone(f.reports);
    const descriptor = { runId: '456', runAttempt: 1, sha: source };
    if (kind === 'not verified') rows.at(-1).phase = 'failed';
    if (kind === 'wrong run') descriptor.runId = '999';
    if (kind === 'changed policy') rows.forEach(r => { r.policySha256 = 'f'.repeat(64); });
    if (kind === 'contradictory ID') rows[1].renderDeployId = 'dep-foreign';
    if (kind === 'missing origin') delete rows.at(-1).vercelReleaseRunAttempt;
    assert.throws(() => validateBaselineReceipt(rows, descriptor, qualified()));
  });
}
test('completed release or later successful CI rerun cannot authorize another deployment of that source', async () => {
  const first = fixture(); await first.execute();
  for (const attempt of [1, 2]) {
    const f = fixture(); f.state.candidate = freezeCandidate({ ...ciQualified(), ciRunAttempt: attempt }, runnerEnv(), policy());
    f.state.baselineRows = first.reports;
    f.state.plan = { policySha256: f.state.candidate.policySha256,
      baseline: { runId: '456', runAttempt: 1, sha: source }, resume: null };
    const read = f.api.gh;
    f.api.gh = async route => route === '/actions/runs/123' ? { ...run(), run_attempt: attempt } :
      route.includes(`/attempts/${attempt}/jobs`) ? { total_count: requiredJobs.length, jobs: jobs(attempt) } : read(route);
    await assert.rejects(f.execute(), /SOURCE_ALREADY_DEPLOYED/);
    assert.equal(f.posts.length, 0);
    assert.equal(first.reports.at(-1).ciRunAttempt, 1);
  }
});
for (const kind of ['direct push', 'stale approval', 'dismissed approval', 'request changes', 'self approval',
  'bot approval', 'post-merge approval', 'reviewer revoked', 'unknown permission', 'incomplete reviews']) {
  test(`candidate review rejects ${kind} before any mutation`, async () => {
    const f = fixture();
    if (kind === 'direct push') f.state.pr.merge_commit_sha = previous;
    if (kind === 'stale approval') f.state.reviews[0].commit_id = previous;
    if (kind === 'dismissed approval') f.state.reviews[0].state = 'DISMISSED';
    if (kind === 'request changes') f.state.reviews.push({ ...f.state.reviews[0], id: 2, state: 'CHANGES_REQUESTED' });
    if (kind === 'self approval') f.state.reviews[0].user = { ...f.state.pr.user, type: 'User' };
    if (kind === 'bot approval') f.state.reviews[0].user.type = 'Bot';
    if (kind === 'post-merge approval') f.state.reviews[0].submitted_at = '2026-10-17T00:00:00Z';
    if (kind === 'reviewer revoked') f.state.reviewerPermission.permission = 'read';
    if (kind === 'unknown permission') f.state.reviewerPermission.user.id = 999;
    if (kind === 'incomplete reviews') f.state.reviews = Array.from({ length: 100 }, () => f.state.reviews[0]);
    await assert.rejects(f.execute()); assert.equal(f.posts.length, 0);
  });
}
test('review revocation after Render completion blocks Vercel', async () => {
  const f = fixture(), read = f.api.health;
  f.api.health = async () => { const value = await read();
    if (f.state.newRender?.status === 'live') f.state.reviews[0].state = 'DISMISSED'; return value; };
  await assert.rejects(f.execute(), /PREMERGE_EXACT_HEAD_REVIEW_REQUIRED/);
  assert.deepEqual(f.posts.map(x => x.provider), ['render']);
});
for (const kind of ['missing rules', 'wrong app', 'missing job', 'no latest approval', 'manual environment gate', 'wrong branch type']) {
  test(`infrastructure rejects ${kind} before entering provider rollout`, async () => {
    const f = fixture();
    if (kind === 'missing rules') f.state.rules = [];
    if (kind === 'wrong app') f.state.rules.at(-1).parameters.required_status_checks[0].integration_id = 999;
    if (kind === 'missing job') f.state.rules.at(-1).parameters.required_status_checks.pop();
    if (kind === 'no latest approval') f.state.rules[2].parameters.require_last_push_approval = false;
    if (kind === 'manual environment gate') f.state.environment.protection_rules.push({ type: 'required_reviewers' });
    if (kind === 'wrong branch type') f.state.branches[0].type = 'tag';
    await assert.rejects(f.execute()); assert.equal(f.posts.length, 0);
  });
}

test('workflow entry qualifies, archives, admits and deploys automatically with no per-release secret', async () => {
  const temp = mkdtempSync(join(tmpdir(), 'aqua-unattended-entry-'));
  const head = execFileSync('git', ['rev-parse', 'HEAD'], { encoding: 'utf8' }).trim();
  const f = fixture(), original = f.api.gh;
  const replace = value => JSON.parse(JSON.stringify(value).replaceAll(source, head));
  const current = releaseRun({ head_sha: head });
  f.api.gh = async route => route === '/actions/runs/456' ? current :
    route.includes('/workflows/release.yml/runs?') ? { total_count: 1, workflow_runs: [current] } :
    replace(await original(route));
  for (const key of ['render', 'vercel', 'health']) {
    const read = f.api[key]; f.api[key] = async (...args) => replace(await read(...args));
  }
  const env = { ...runnerEnv(), GITHUB_EVENT_NAME: 'workflow_run', GITHUB_EVENT_PATH: join(temp, 'event.json'),
    GITHUB_OUTPUT: join(temp, 'output'), CANDIDATE_DIR: join(temp, 'candidate'),
    RELEASE_REPORT: join(temp, 'report.jsonl'), GITHUB_STEP_SUMMARY: join(temp, 'summary') };
  mkdirSync(env.CANDIDATE_DIR);
  writeFileSync(env.GITHUB_EVENT_PATH, JSON.stringify({ action: 'completed', workflow_run: { ...run(), head_sha: head } }));
  try {
    const options = { api: f.api, policy: policy(), deploymentOptions: immediate };
    await main(env, { ...options, mode: 'candidate' });
    const manifest = JSON.parse(readFileSync(join(env.CANDIDATE_DIR, 'candidate.json')));
    assert.equal(manifest.sha, head); assert.equal(manifest.version, 3);
    await main(env, { ...options, mode: 'admit' });
    await main(env, { ...options, mode: 'deploy' });
    assert.deepEqual(f.posts.map(x => x.provider), ['render', 'vercel']);
    const rows = readFileSync(env.RELEASE_REPORT, 'utf8').trim().split('\n').map(x => JSON.parse(x));
    assert.equal(rows.at(-1).phase, 'verified'); assert.equal(rows.at(-1).ciRunAttempt, 1);
    assert.equal(rows.at(-1).policySha256, policyDigest(policy()));
    f.state.reviews[0].state = 'DISMISSED';
    assert.equal(await runRelease(env, { ...options, mode: 'deploy' }), 1);
    const failed = JSON.parse(readFileSync(env.RELEASE_REPORT, 'utf8').trim().split('\n').at(-1));
    assert.equal(failed.code, 'PREMERGE_EXACT_HEAD_REVIEW_REQUIRED');
    assert.equal(failed.policySha256, policyDigest(policy()));
  } finally { rmSync(temp, { recursive: true, force: true }); }
});

test('retry chain retains attempt1 operations across attempt2 preflight failure and attempt3 reconciliation', async () => {
  const f = resumeFixture();
  const attempt2 = createAuditWriter(f.state.candidate, row => f.state.receiptRows.push(row));
  attempt2({ phase: 'failed', code: 'MAIN_PR_AND_CI_GOVERNANCE_REQUIRED' });
  f.state.candidate = freezeCandidate(ciQualified(), runnerEnv(3), f.state.policy);
  f.state.env = runnerEnv(3); f.state.plan.resume.runAttempt = 2;
  await f.execute();
  assert.equal(f.posts.length, 0);
  assert.equal(f.reports.at(-1).renderReleaseRunAttempt, 1);
  assert.equal(f.reports.at(-1).vercelReleaseRunAttempt, 1);
  assert.equal(f.reports.at(-1).releaseRunAttempt, 3);
});
test('retry chain cannot cross an ambiguous request or omit an earlier attempt', async () => {
  for (const kind of ['ambiguous', 'missing first', 'gap']) {
    const f = resumeFixture();
    const rows = f.state.receiptRows;
    f.state.candidate = freezeCandidate(ciQualified(), runnerEnv(3), f.state.policy);
    f.state.env = runnerEnv(3); f.state.plan.resume.runAttempt = 2;
    const writer = createAuditWriter(freezeCandidate(ciQualified(), runnerEnv(2), policy()), row => rows.push(row));
    writer({ phase: 'failed', code: kind === 'ambiguous' ? 'AMBIGUOUS_PROVIDER_POST_INSPECT_BEFORE_RETRY' : 'MAIN_PR_AND_CI_GOVERNANCE_REQUIRED' });
    if (kind === 'missing first') rows.splice(0, rows.length - 1);
    if (kind === 'gap') rows.at(-1).releaseRunAttempt = 4;
    await assert.rejects(f.execute()); assert.equal(f.posts.length, 0);
  }
});
test('failed rerun preserves the earlier successful attempt as a future release baseline', async () => {
  const f = planFixture();
  const prior = releaseRun({ id: 455, run_number: 2, head_sha: previous,
    run_attempt: 2, status: 'completed', conclusion: 'failure' });
  f.state.current.run_number = 3; f.state.runs = [f.state.current, prior];
  f.state.artifacts = [artifactFor(prior, 1)];
  const read = f.api.gh;
  f.api.gh = async route => route === '/actions/runs/455/attempts/1' ?
    { ...prior, run_attempt: 1, conclusion: 'success' } : read(route);
  const plan = await evidencePlan(f.api, qualified(), policy());
  assert.deepEqual(plan.baseline, { artifactId: '789', runId: '455', runAttempt: 1, sha: previous });
});
test('foreign Vercel build starting while Render deploys blocks the Vercel POST', async () => {
  const f = fixture(), read = f.api.health;
  f.api.health = async () => { const value = await read();
    if (f.state.newRender?.status === 'live') f.state.vercelRows.push({ uid: 'dpl_Foreign', state: 'BUILDING' });
    return value;
  };
  await assert.rejects(f.execute(), /VERCEL_CHANGED_BEFORE_REQUEST/);
  assert.deepEqual(f.posts.map(x => x.provider), ['render']);
});
test('foreign Render build starting during admission blocks Render POST', async () => {
  const f = fixture(), read = f.api.vercel;
  f.api.vercel = async (route, body) => { const value = await read(route, body);
    if (route.startsWith('/v6/')) f.state.renderRows.push({ id: 'dep-foreign', status: 'build_in_progress', commit: { id: previous } });
    return value;
  };
  await assert.rejects(f.execute(), /RENDER_CHANGED_BEFORE_REQUEST/);
  assert.equal(f.posts.length, 0);
});

for (const drift of ['Render identity', 'runtime restriction', 'recovery health']) {
  test(`${drift} drift during Vercel build cannot mint a verified receipt`, async () => {
    const f = fixture(), read = f.api.vercel;
    f.api.vercel = async (route, body) => {
      const value = await read(route, body);
      if (route.endsWith('/dpl_New') && value.readyState === 'READY') {
        if (drift === 'Render identity') {
          f.state.renderRows.forEach(r => { r.status = 'deactivated'; });
          f.state.renderRows.unshift({ id: 'dep-foreign', status: 'live', commit: { id: source } });
        }
        if (drift === 'runtime restriction') f.state.vars.find(x => x.envVar.key === 'Bird__Enabled').envVar.value = 'true';
        if (drift === 'recovery health') f.api.health = async () => ({ ...health(source), recovery: null });
      }
      return value;
    };
    await assert.rejects(f.execute());
    assert.equal(f.reports.some(r => r.phase === 'verified'), false);
    assert.deepEqual(f.posts.map(x => x.provider), ['render', 'vercel']);
  });
}

test('cancellation after request intent and before recorded provider ID cannot be retried blindly', async () => {
  const f = resumeFixture();
  f.state.receiptRows = [];
  const writer = createAuditWriter(qualified(), r => f.state.receiptRows.push(r));
  writer({ phase: 'render-requesting' });
  await assert.rejects(f.execute(), /AMBIGUOUS_PRIOR_REQUEST_REQUIRES_RECONCILIATION/);
  assert.equal(f.posts.length, 0);
});
test('a later main can supersede a pre-provider failure but cannot supersede an ambiguous prior release', async () => {
  for (const providerConclusion of ['skipped', 'failure', 'cancelled', 'success']) {
    const f = planFixture();
    const old = releaseRun({ id: 455, run_number: 2, head_sha: previous, status: 'completed', conclusion: 'failure' });
    f.state.current.run_number = 3; f.state.runs = [f.state.current, old];
    const read = f.api.gh;
    f.api.gh = async route => route === '/actions/runs/455/attempts/1/jobs?per_page=100' ? {
      total_count: 2, jobs: [
        { name: 'Qualify immutable source candidate', run_id: 455, run_attempt: 1, status: 'completed', conclusion: 'success' },
        { name: 'Unattended recovery release', run_id: 455, run_attempt: 1, status: 'completed', conclusion: 'failure',
          steps: [{ name: 'Revalidate policy, provenance, providers and recovery before automatic deployment', conclusion: providerConclusion }] }
      ]
    } : read(route);
    if (providerConclusion === 'skipped') assert.equal((await evidencePlan(f.api, qualified(), policy())).baseline, null);
    else await assert.rejects(evidencePlan(f.api, qualified(), policy()), /UNVERIFIED_PRIOR_RELEASE_REQUIRES_RECONCILIATION/);
  }
});
