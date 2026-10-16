import { createHash } from 'node:crypto';
import { appendFileSync, readFileSync, writeFileSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import { pathToFileURL } from 'node:url';

export const targets = JSON.parse(readFileSync(new URL('./targets.json', import.meta.url)));
export const requiredJobs = [
  'Backend application coverage partition', 'Backend web coverage partition',
  ...['application-administration', 'application-membership', 'application-authentication',
    'application-remainder', 'entity-framework-migrations', 'entity-framework-placement',
    'entity-framework-remainder', 'remainder'].map(x => `Backend application tests (${x})`),
  ...['placement', 'controllers', 'remainder'].map(x => `Backend web tests (${x})`),
  'Frontend quality checks', 'PostgreSQL transactional invitation regressions',
  'Validate container images', 'Release safety tests'
];
const shaPattern = /^[a-f0-9]{40}$/;
const activeRender = new Set(['created', 'build_in_progress', 'pre_deploy_in_progress', 'update_in_progress']);
const activeVercel = new Set(['QUEUED', 'INITIALIZING', 'BUILDING']);
const renderStates = new Set([...activeRender, 'live', 'deactivated', 'build_failed',
  'pre_deploy_failed', 'update_failed', 'canceled']);
const vercelStates = new Set([...activeVercel, 'READY', 'ERROR', 'CANCELED']);

export class ReleaseFailure extends Error {}
export function requireThat(condition, code) {
  if (!condition) throw new ReleaseFailure(code);
}
export function validateRun(run, jobs, mainSha) {
  requireThat(run.repository?.full_name === targets.repository &&
    run.head_repository?.full_name === targets.repository, 'CI_REPOSITORY_MISMATCH');
  requireThat(run.event === 'push' && run.head_branch === 'main' &&
    run.path === '.github/workflows/ci.yml' && run.name === 'Continuous integration', 'CI_SOURCE_NOT_MAIN_PUSH');
  requireThat(run.status === 'completed' && run.conclusion === 'success', 'CI_NOT_SUCCESSFUL');
  requireThat(shaPattern.test(run.head_sha) && run.head_sha === mainSha, 'STALE_OR_INVALID_SOURCE');
  requireThat(!targets.excludedCommits.includes(run.head_sha), 'CURRENT_MERGE_NOT_AUTHORIZED');
  requireThat(jobs.length === requiredJobs.length && new Set(jobs.map(j => j.name)).size === jobs.length &&
    requiredJobs.every(name => jobs.some(j => j.name === name && j.status === 'completed' &&
      j.conclusion === 'success')), 'CI_INVENTORY_NOT_QUALIFIED');
  return { version: 1, repository: targets.repository, sha: run.head_sha,
    ciRunId: String(run.id), ciRunAttempt: run.run_attempt, ciUrl: run.html_url };
}

export function verifyCandidate(candidate, qualified, archive, trustedArchive) {
  for (const key of ['version', 'repository', 'sha', 'ciRunId', 'ciRunAttempt'])
    requireThat(candidate[key] === qualified[key], 'CANDIDATE_PROVENANCE_MISMATCH');
  requireThat(candidate.sourceSha256 === createHash('sha256').update(archive).digest('hex'),
    'CANDIDATE_CONTENT_MISMATCH');
  requireThat(Buffer.isBuffer(trustedArchive) && archive.equals(trustedArchive),
    'CANDIDATE_NOT_QUALIFIED_GIT_SOURCE');
}

export function validateAuthority(raw, qualified, env, now = Date.now()) {
  requireThat(env.AUTO_DEPLOY_ENABLED === 'true', 'ACTIVATION_BLOCKED_AUTO_DEPLOY_NOT_AUTHORIZED');
  let a;
  try { a = JSON.parse(raw); } catch { throw new ReleaseFailure('EXACT_RELEASE_AUTHORITY_REQUIRED'); }
  requireThat(a.profile === targets.profile && a.commit === qualified.sha &&
    String(a.ciRunId) === qualified.ciRunId && String(a.releaseRunId) === env.GITHUB_RUN_ID &&
    String(a.releaseRunAttempt) === env.GITHUB_RUN_ATTEMPT, 'EXACT_RELEASE_AUTHORITY_REQUIRED');
  const until = Date.parse(a.expiresAt);
  requireThat(Number.isFinite(until) && until > now && until <= now + 24 * 60 * 60 * 1000,
    'AUTHORITY_EXPIRED_OR_UNBOUNDED');
  requireThat(typeof a.authorityReference === 'string' && /^[A-Za-z0-9_.\/-]{1,160}$/.test(a.authorityReference) &&
    /^dep-[a-z0-9]+$/.test(a.baselineRenderDeployId) && /^dpl_[A-Za-z0-9]+$/.test(a.baselineVercelDeployId) &&
    a.schemaCompatibilityVerified === true && a.rollbackVerified === true &&
    a.effectiveRuntimeVerified === true && a.singleTriggerVerified === true &&
    a.configurationFreezeVerified === true, 'RECOVERY_EVIDENCE_REQUIRED');
  return a;
}

export function validateEnvironment(environment, branches) {
  requireThat(environment.can_admins_bypass === false &&
    environment.protection_rules?.some(r => r.type === 'required_reviewers' && r.prevent_self_review === true &&
      r.reviewers?.length > 0) && environment.deployment_branch_policy?.custom_branch_policies === true &&
    environment.deployment_branch_policy?.protected_branches === false && branches.length === 1 &&
    branches[0].name === 'main' && branches[0].type === 'branch', 'PROTECTED_ENVIRONMENT_REQUIRED');
}

export function validateRender(service, rows) {
  const d = service.serviceDetails;
  requireThat(service.id === targets.render.id && service.name === targets.render.name &&
    service.repo?.replace(/\.git$/, '') === `https://github.com/${targets.repository}` &&
    service.branch === 'main' && service.suspended === 'not_suspended' &&
    service.autoDeployTrigger === 'off' && service.autoDeploy === 'no' && !service.rootDir &&
    d?.runtime === 'docker' && d.healthCheckPath === '/api/health' &&
    d.envSpecificDetails?.dockerContext === '.' &&
    d.envSpecificDetails?.dockerfilePath === './docker/api/Dockerfile' &&
    d.envSpecificDetails?.preDeployCommand === '' && d.envSpecificDetails?.dockerCommand === '',
  'RENDER_TARGET_OR_RECOVERY_CONFIGURATION_DRIFT');
  const vars = Object.fromEntries(rows.map(x => [x.envVar.key, x.envVar.value]));
  requireThat(vars.ASPNETCORE_ENVIRONMENT === 'Production' && vars.App__Recovery__Enabled === 'true',
    'ACTIVE_RECOVERY_BASELINE_REQUIRED');
  for (const key of ['App__WeeklyCommissions__Enabled', 'App__EntryMonthlyObligations__Enabled',
    'App__FinancialOperations__Enabled', 'App__BackgroundJobs__Enabled', 'AQGreenV2Demo__Enabled',
    'AQGreenV2Pilot__Enabled', 'App__AQGreenV2__Commissioning__Enabled', 'SEED_DEMO_DATA', 'Bird__Enabled'])
    requireThat(vars[key] === 'false', 'RESTRICTED_OPERATION_NOT_HELD');
  for (const key of ['Yoco__Mode', 'Yoco__SecretKey', 'Yoco__WebhookSecret', 'Bird__ApiKey'])
    requireThat(!vars[key]?.trim(), 'RECOVERY_PROVIDER_ISOLATION_REQUIRED');
}

export function validateVercel(project, repoId) {
  requireThat(project.id === targets.vercel.id && project.accountId === targets.vercel.teamId &&
    project.name === targets.vercel.name && project.rootDirectory === targets.vercel.rootDirectory &&
    project.nodeVersion === '24.x' && !project.commandForIgnoringBuildStep &&
    project.link?.type === 'github' && String(project.link.repoId) === String(repoId) &&
    project.link.productionBranch === 'main' && project.link.org === 'aqua-lifestyle' &&
    project.link.repo === 'aqua-lifestyle' && project.autoAssignCustomDomains === true &&
    !project.buildCommand && !project.installCommand && !project.outputDirectory,
  'VERCEL_TARGET_OR_GIT_CONFIGURATION_DRIFT');
}

export function validateVercelVariables(rows) {
  for (const [key, expected] of [['NEXT_PUBLIC_ABP_API_URL', targets.vercel.publicApiUrl],
    ['NEXT_PUBLIC_DEFAULT_TENANT_NAME', targets.vercel.defaultTenantName]]) {
    const selected = rows.filter(x => x.key === key && x.target?.includes('production'));
    requireThat(selected.length === 1 && !selected[0].gitBranch &&
      selected[0].value === expected, 'VERCEL_PUBLIC_BUILD_CONFIGURATION_UNVERIFIED');
  }
}

export function validateHealth(response, sha) {
  const j = response.body.result ?? response.body;
  requireThat(response.status === 200 && j.status === 'Healthy' && j.buildId === sha &&
    j.environment === 'Production' && j.databaseStatus === 'Healthy' && j.redisStatus === 'Healthy' &&
    j.dataProtectionKeyStoreStatus === 'Healthy' && response.recovery === 'true',
  'API_REVISION_OR_RECOVERY_READINESS_FAILED');
}

// No configurable provider URL, response bodies, request headers or raw errors are logged.
// POSTs are never automatically retried: a lost response requires provider reconciliation.
export function clients(env, fetcher = fetch) {
  async function request(base, token, route, body) {
    requireThat(token, 'PROVIDER_CREDENTIAL_UNAVAILABLE');
    let response;
    try {
      response = await fetcher(base + route, { method: body ? 'POST' : 'GET', redirect: 'error',
        signal: AbortSignal.timeout(30_000), headers: { Authorization: `Bearer ${token}`,
          Accept: 'application/json', 'Content-Type': 'application/json' },
        ...(body ? { body: JSON.stringify(body) } : {}) });
    } catch { throw new ReleaseFailure(body ? 'AMBIGUOUS_PROVIDER_POST_INSPECT_BEFORE_RETRY' : 'PROVIDER_READ_FAILED'); }
    requireThat(response.ok, body ? 'PROVIDER_POST_REJECTED_INSPECT_BEFORE_RETRY' : 'PROVIDER_READ_REJECTED');
    try { return await response.json(); } catch { throw new ReleaseFailure('INVALID_PROVIDER_RESPONSE'); }
  }
  return {
    gh: (route, body) => request(`https://api.github.com/repos/${targets.repository}`, env.GH_TOKEN, route, body),
    render: (route, body) => request('https://api.render.com/v1', env.RENDER_API_KEY, route, body),
    vercel: (route, body) => request('https://api.vercel.com', env.VERCEL_TOKEN,
      route + (route.includes('?') ? '&' : '?') + `teamId=${targets.vercel.teamId}`, body),
    health: async () => {
      let r;
      try { r = await fetcher(targets.render.healthUrl, { redirect: 'error',
        signal: AbortSignal.timeout(30_000), headers: { 'Cache-Control': 'no-cache' } });
        return { status: r.status, recovery: r.headers.get('x-aqua-recovery-suspended'), body: await r.json() };
      } catch { throw new ReleaseFailure('API_HEALTH_UNAVAILABLE'); }
    },
    web: async () => {
      try { const r = await fetcher(targets.vercel.productionUrl, { redirect: 'error',
        signal: AbortSignal.timeout(30_000), headers: { 'Cache-Control': 'no-cache' } });
        requireThat(r.status === 200, 'WEB_READINESS_FAILED');
      } catch { throw new ReleaseFailure('WEB_READINESS_FAILED'); }
    }
  };
}

export async function qualify(api, runId) {
  requireThat(/^\d+$/.test(String(runId)), 'CI_RUN_ID_INVALID');
  const run = await api.gh(`/actions/runs/${runId}`);
  const page = await api.gh(`/actions/runs/${runId}/jobs?filter=latest&per_page=100`);
  requireThat(page.total_count === page.jobs?.length, 'CI_INVENTORY_INCOMPLETE');
  const main = await api.gh('/git/ref/heads/main');
  return validateRun(run, page.jobs, main.object.sha);
}

export async function waitFor(read, state, good, pending, options) {
  const { attempts = 120, pause = () => new Promise(resolve => setTimeout(resolve, 10_000)) } = options ?? {};
  for (let i = 0; i < attempts; i++) {
    const value = await read();
    if (state(value) === good) return value;
    requireThat(pending.has(state(value)), 'PROVIDER_DEPLOYMENT_FAILED_INSPECT_RELEASE_REPORT');
    if (i + 1 < attempts) await pause();
  }
  throw new ReleaseFailure('PROVIDER_TIMEOUT_RECONCILE_BEFORE_RETRY');
}

export async function deploymentHistory(api, provider) {
  const result = [], seen = new Set();
  let cursor;
  // Fail closed on an unexpectedly large/inconsistent inventory. Never interpret
  // a truncated first page as absence of a concurrent/previous deployment.
  for (let page = 0; page < 20; page++) {
    let rows, next;
    if (provider === 'render') {
      const data = await api.render(`/services/${targets.render.id}/deploys?limit=100` +
        (cursor ? `&cursor=${encodeURIComponent(cursor)}` : ''));
      rows = data.map(x => x.deploy);
      if (data.length === 100) next = data.at(-1).cursor;
      requireThat(rows.every(x => renderStates.has(x.status)), 'UNKNOWN_RENDER_DEPLOYMENT_STATE');
      requireThat(data.length !== 100 || next, 'RENDER_HISTORY_INCOMPLETE');
    } else {
      const data = await api.vercel(`/v6/deployments?projectId=${targets.vercel.id}&limit=100` +
        (cursor ? `&until=${encodeURIComponent(cursor)}` : ''));
      rows = data.deployments; next = data.pagination?.next;
      requireThat(rows.every(x => vercelStates.has(x.state ?? x.readyState)), 'UNKNOWN_VERCEL_DEPLOYMENT_STATE');
    }
    result.push(...rows);
    if (!next) return result;
    requireThat(!seen.has(String(next)), 'PROVIDER_HISTORY_CURSOR_REPEATED');
    seen.add(String(next)); cursor = next;
  }
  throw new ReleaseFailure('PROVIDER_HISTORY_INVENTORY_TOO_LARGE');
}

export async function deploy(api, qualified, authority, report, options) {
  const servicePath = `/services/${targets.render.id}`;
  const projectPath = `/v9/projects/${targets.vercel.id}`;
  const repository = await api.gh('');
  const readRender = async () => {
    const service = await api.render(servicePath);
    const rows = await api.render(`${servicePath}/env-vars?limit=100`);
    // More than one page cannot safely be interpreted as a complete configuration.
    requireThat(rows.length < 100, 'RENDER_ENVIRONMENT_INVENTORY_INCOMPLETE');
    validateRender(service, rows);
  };
  await readRender();
  let project = await api.vercel(projectPath);
  validateVercel(project, repository.id);
  const readVercelVariables = async () => validateVercelVariables(
    (await api.vercel(`/v10/projects/${targets.vercel.id}/env?decrypt=true`)).envs);
  await readVercelVariables();
  const baseline = await api.render(`${servicePath}/deploys/${authority.baselineRenderDeployId}`);
  requireThat(shaPattern.test(baseline.commit?.id), 'ROLLBACK_BASELINE_REVISION_UNAVAILABLE');
  const compare = await api.gh(`/compare/${baseline.commit.id}...${qualified.sha}`);
  requireThat(compare.status === 'ahead' || compare.status === 'identical', 'SOURCE_NOT_BASELINE_DESCENDANT');
  requireThat(compare.files?.length < 300 && !compare.files.some(f =>
    /\/Migrations\//i.test(f.filename) || /\/Migrations\//i.test(f.previous_filename ?? '')),
  'SCHEMA_CHANGE_REQUIRES_SEPARATE_AUTHORIZATION');
  const renderDeploys = await deploymentHistory(api, 'render');
  const live = renderDeploys.filter(x => x.status === 'live');
  requireThat(live.length === 1 && (live[0].id === authority.baselineRenderDeployId ||
    live[0].commit?.id === qualified.sha), 'RENDER_BASELINE_DRIFT');
  validateHealth(await api.health(), live[0].commit.id);
  requireThat(renderDeploys.every(x => !activeRender.has(x.status) || x.commit?.id === qualified.sha),
    'CONCURRENT_RENDER_DEPLOYMENT');
  const vercelDeploys = await deploymentHistory(api, 'vercel');
  requireThat(vercelDeploys.every(x => !activeVercel.has(x.state ?? x.readyState) ||
    x.meta?.aquaCiRunId === qualified.ciRunId), 'CONCURRENT_VERCEL_DEPLOYMENT');
  const isOurs = x => x.gitSource?.sha === qualified.sha &&
    String(x.meta?.aquaCiRunId) === qualified.ciRunId && x.target === 'production';
  let existingVercel;
  for (const d of vercelDeploys.filter(x => String(x.meta?.aquaCiRunId) === qualified.ciRunId)) {
    requireThat(/^dpl_[A-Za-z0-9]+$/.test(d.uid), 'VERCEL_DEPLOYMENT_ID_UNAVAILABLE');
    const full = await api.vercel(`/v13/deployments/${d.uid}`);
    requireThat(isOurs(full), 'VERCEL_SOURCE_PROVENANCE_MISMATCH');
    requireThat(!existingVercel, 'AMBIGUOUS_VERCEL_DEPLOYMENTS');
    existingVercel = full;
  }
  requireThat(project.targets?.production?.id === authority.baselineVercelDeployId ||
    (existingVercel && project.targets?.production?.id === existingVercel.id), 'VERCEL_BASELINE_DRIFT');

  report({ phase: 'admitted', sha: qualified.sha, ciRunId: qualified.ciRunId,
    authorityReference: authority.authorityReference, baselineRenderDeployId: authority.baselineRenderDeployId,
    baselineVercelDeployId: authority.baselineVercelDeployId });
  let render = renderDeploys.find(x => x.commit?.id === qualified.sha);
  if (render) requireThat(render.status === 'live' || activeRender.has(render.status), 'PRIOR_RENDER_FAILURE_REQUIRES_OPERATOR');
  else {
    // Recheck freshness immediately before the irreversible provider request.
    await qualify(api, qualified.ciRunId);
    requireThat(Date.parse(authority.expiresAt) > (options?.now ?? Date.now)(), 'AUTHORITY_EXPIRED');
    render = await api.render(`${servicePath}/deploys`, { commitId: qualified.sha, clearCache: 'do_not_clear' });
  }
  requireThat(/^dep-[a-z0-9]+$/.test(render.id), 'RENDER_DEPLOYMENT_ID_UNAVAILABLE');
  report({ phase: 'render-requested', renderDeployId: render.id });
  render = await waitFor(() => api.render(`${servicePath}/deploys/${render.id}`),
    x => x.status, 'live', activeRender, options);
  requireThat(render.commit?.id === qualified.sha, 'RENDER_SOURCE_PROVENANCE_MISMATCH');
  await readRender();
  validateHealth(await api.health(), qualified.sha);
  report({ phase: 'render-ready', renderDeployId: render.id, sha: render.commit.id });

  await qualify(api, qualified.ciRunId);
  project = await api.vercel(projectPath);
  validateVercel(project, repository.id);
  await readVercelVariables();
  if (!existingVercel) {
    requireThat(Date.parse(authority.expiresAt) > (options?.now ?? Date.now)(), 'AUTHORITY_EXPIRED');
    existingVercel = await api.vercel('/v13/deployments', {
      name: targets.vercel.name, project: targets.vercel.id, target: 'production',
      gitSource: { type: 'github', repoId: String(repository.id), ref: qualified.sha, sha: qualified.sha },
      meta: { aquaCiRunId: qualified.ciRunId, aquaSourceSha: qualified.sha }
    });
  }
  requireThat(/^dpl_[A-Za-z0-9]+$/.test(existingVercel.id), 'VERCEL_DEPLOYMENT_ID_UNAVAILABLE');
  report({ phase: 'vercel-requested', vercelDeployId: existingVercel.id });
  const vercel = await waitFor(() => api.vercel(`/v13/deployments/${existingVercel.id}`),
    x => x.readyState, 'READY', activeVercel, options);
  requireThat(isOurs(vercel) && vercel.projectId === targets.vercel.id && !vercel.aliasError,
    'VERCEL_SOURCE_PROVENANCE_MISMATCH');
  project = await api.vercel(projectPath);
  requireThat(project.targets?.production?.id === vercel.id, 'VERCEL_PRODUCTION_ALIAS_NOT_ASSIGNED');
  await api.web();
  report({ phase: 'verified', sha: qualified.sha, renderDeployId: render.id, vercelDeployId: vercel.id });
}

async function main(env) {
  const api = clients(env);
  const mode = process.argv[2];
  if (mode === 'qualify-tag') {
    requireThat(env.GITHUB_EVENT_NAME === 'push' && /^refs\/tags\/v\d+\.\d+\.\d+$/.test(env.GITHUB_REF), 'VERSION_TAG_REQUIRED');
    const runs = await api.gh(`/actions/workflows/ci.yml/runs?branch=main&event=push&head_sha=${env.GITHUB_SHA}&per_page=100`);
    const run = runs.workflow_runs.find(x => x.status === 'completed' && x.conclusion === 'success');
    requireThat(run, 'QUALIFIED_MAIN_CI_REQUIRED');
    const q = await qualify(api, run.id);
    requireThat(q.sha === env.GITHUB_SHA, 'TAG_SOURCE_NOT_QUALIFIED');
    appendFileSync(env.GITHUB_OUTPUT, `sha=${q.sha}\n`);
    return;
  }
  requireThat(env.GITHUB_EVENT_NAME === 'workflow_run', 'COMPLETED_CI_EVENT_REQUIRED');
  const event = JSON.parse(readFileSync(env.GITHUB_EVENT_PATH));
  requireThat(event.action === 'completed' && event.workflow_run?.event === 'push' &&
    event.workflow_run?.head_branch === 'main', 'COMPLETED_MAIN_PUSH_REQUIRED');
  const q = await qualify(api, event.workflow_run.id);
  requireThat(q.sha === event.workflow_run.head_sha && q.ciRunAttempt === event.workflow_run.run_attempt,
    'CI_EVENT_PROVENANCE_MISMATCH');
  requireThat(execFileSync('git', ['rev-parse', 'HEAD'], { encoding: 'utf8' }).trim() === q.sha,
    'CHECKOUT_SOURCE_MISMATCH');
  if (mode === 'candidate') {
    const archive = execFileSync('git', ['archive', '--format=tar', q.sha], { maxBuffer: 100 * 1024 * 1024 });
    writeFileSync(`${env.CANDIDATE_DIR}/source.tar`, archive);
    writeFileSync(`${env.CANDIDATE_DIR}/candidate.json`, JSON.stringify({ ...q,
      sourceSha256: createHash('sha256').update(archive).digest('hex') }, null, 2) + '\n');
    appendFileSync(env.GITHUB_OUTPUT, `sha=${q.sha}\n`);
    return;
  }
  requireThat(mode === 'deploy', 'RELEASE_MODE_INVALID');
  verifyCandidate(JSON.parse(readFileSync(`${env.CANDIDATE_DIR}/candidate.json`)), q,
    readFileSync(`${env.CANDIDATE_DIR}/source.tar`),
    execFileSync('git', ['archive', '--format=tar', q.sha], { maxBuffer: 100 * 1024 * 1024 }));
  const authority = validateAuthority(env.PRODUCTION_RELEASE_AUTHORIZATION, q, env);
  const environment = await api.gh(`/environments/${targets.environment}`);
  const branches = await api.gh(`/environments/${targets.environment}/deployment-branch-policies`);
  validateEnvironment(environment, branches.branch_policies);
  const report = row => appendFileSync(env.RELEASE_REPORT, JSON.stringify(row) + '\n');
  await deploy(api, q, authority, report);
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main(process.env).catch(error => {
    const code = error instanceof ReleaseFailure ? error.message : 'UNEXPECTED_RELEASE_ERROR';
    if (process.env.RELEASE_REPORT)
      appendFileSync(process.env.RELEASE_REPORT, JSON.stringify({ phase: 'failed', code }) + '\n');
    console.error(`::error::${code}. See the safe-release runbook; do not resume or retry provider requests blindly.`);
    if (process.env.GITHUB_STEP_SUMMARY)
      appendFileSync(process.env.GITHUB_STEP_SUMMARY, `Release blocked/failed: **${code}**.\nSee docs/operations/safe-automatic-deployment.md. No automatic rollback or configuration repair.\n`);
    process.exitCode = 1;
  });
}
