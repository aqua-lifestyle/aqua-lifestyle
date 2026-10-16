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
const sourceIdentityFields = ['repository', 'sha', 'ciRunId', 'ciRunAttempt'];
const targetIdentity = Object.freeze({ environment: targets.environment, profile: targets.profile,
  renderServiceId: targets.render.id, vercelProjectId: targets.vercel.id, vercelTeamId: targets.vercel.teamId });
const releaseIdentityFields = ['version', ...sourceIdentityFields, 'releaseRunId', 'releaseRunAttempt',
  ...Object.keys(targetIdentity)];
const positiveId = value => typeof value === 'string' && /^[1-9]\d*$/.test(value);
const positiveAttempt = value => Number.isSafeInteger(value) && value > 0;

export class ReleaseFailure extends Error {}
export function requireThat(condition, code) {
  if (!condition) throw new ReleaseFailure(code);
}
export function validateRun(run, jobs, mainSha) {
  requireThat(positiveId(String(run.id)) && Number.isSafeInteger(run.id) &&
    positiveAttempt(run.run_attempt), 'CI_IDENTITY_UNVERIFIABLE');
  requireThat(run.repository?.full_name === targets.repository &&
    run.head_repository?.full_name === targets.repository, 'CI_REPOSITORY_MISMATCH');
  requireThat(run.event === 'push' && run.head_branch === 'main' &&
    run.path === '.github/workflows/ci.yml' && run.name === 'Continuous integration', 'CI_SOURCE_NOT_MAIN_PUSH');
  requireThat(run.status === 'completed' && run.conclusion === 'success', 'CI_NOT_SUCCESSFUL');
  requireThat(shaPattern.test(run.head_sha) && run.head_sha === mainSha, 'STALE_OR_INVALID_SOURCE');
  requireThat(!targets.excludedCommits.includes(run.head_sha), 'CURRENT_MERGE_NOT_AUTHORIZED');
  requireThat(jobs.length === requiredJobs.length && new Set(jobs.map(j => j.name)).size === jobs.length &&
    jobs.every(j => j.run_id === run.id && j.run_attempt === run.run_attempt) &&
    requiredJobs.every(name => jobs.some(j => j.name === name && j.status === 'completed' &&
      j.conclusion === 'success')), 'CI_INVENTORY_NOT_QUALIFIED');
  return { version: 1, repository: targets.repository, sha: run.head_sha,
    ciRunId: String(run.id), ciRunAttempt: run.run_attempt, ciUrl: run.html_url };
}

export function freezeCandidate(qualified, env) {
  requireThat(env.GITHUB_REPOSITORY === targets.repository && positiveId(env.GITHUB_RUN_ID) &&
    positiveId(env.GITHUB_RUN_ATTEMPT) && positiveAttempt(Number(env.GITHUB_RUN_ATTEMPT)),
  'RELEASE_CONTEXT_UNVERIFIABLE');
  const candidate = { ...qualified, version: 2, releaseRunId: env.GITHUB_RUN_ID,
    releaseRunAttempt: Number(env.GITHUB_RUN_ATTEMPT), ...targetIdentity };
  validateReleaseIdentity(candidate);
  return Object.freeze(candidate);
}

function validateReleaseIdentity(identity) {
  requireThat(identity?.version === 2 && identity.repository === targets.repository &&
    shaPattern.test(identity.sha) && positiveId(identity.ciRunId) && positiveAttempt(identity.ciRunAttempt) &&
    positiveId(identity.releaseRunId) && positiveAttempt(identity.releaseRunAttempt) &&
    Object.entries(targetIdentity).every(([key, value]) => identity[key] === value),
  'RELEASE_IDENTITY_UNVERIFIABLE');
}

export function releaseKey(identity) {
  validateReleaseIdentity(identity);
  return createHash('sha256').update(JSON.stringify(
    releaseIdentityFields.map(key => [key, identity[key]]))).digest('hex');
}

export function verifyCandidate(candidate, qualified, archive, trustedArchive) {
  validateReleaseIdentity(candidate);
  for (const key of releaseIdentityFields)
    requireThat(candidate[key] === qualified[key], 'CANDIDATE_PROVENANCE_MISMATCH');
  requireThat(candidate.sourceSha256 === createHash('sha256').update(archive).digest('hex'),
    'CANDIDATE_CONTENT_MISMATCH');
  requireThat(Buffer.isBuffer(trustedArchive) && archive.equals(trustedArchive),
    'CANDIDATE_NOT_QUALIFIED_GIT_SOURCE');
}

export function validateAuthority(raw, qualified, env, now = Date.now()) {
  validateReleaseIdentity(qualified);
  requireThat(env.AUTO_DEPLOY_ENABLED === 'true', 'ACTIVATION_BLOCKED_AUTO_DEPLOY_NOT_AUTHORIZED');
  let a;
  try { a = JSON.parse(raw); } catch { throw new ReleaseFailure('EXACT_RELEASE_AUTHORITY_REQUIRED'); }
  requireThat(a && a.version === 2 && a.commit === qualified.sha &&
    [...sourceIdentityFields.filter(key => key !== 'sha'), 'releaseRunId', 'releaseRunAttempt',
      ...Object.keys(targetIdentity)].every(key => a[key] === qualified[key]) &&
    env.GITHUB_REPOSITORY === qualified.repository && env.GITHUB_RUN_ID === qualified.releaseRunId &&
    env.GITHUB_RUN_ATTEMPT === String(qualified.releaseRunAttempt), 'EXACT_RELEASE_AUTHORITY_REQUIRED');
  requireThat(a.resumeFromReleaseAttempt === undefined ||
    (positiveAttempt(a.resumeFromReleaseAttempt) && a.resumeFromReleaseAttempt < qualified.releaseRunAttempt),
  'RESUME_AUTHORITY_INVALID');
  const until = Date.parse(a.expiresAt);
  requireThat(Number.isFinite(until) && until > now && until <= now + 24 * 60 * 60 * 1000,
    'AUTHORITY_EXPIRED_OR_UNBOUNDED');
  requireThat(typeof a.authorityReference === 'string' && /^[A-Za-z0-9_.\/-]{1,160}$/.test(a.authorityReference) &&
    /^dep-[a-z0-9]+$/.test(a.baselineRenderDeployId) && /^dpl_[A-Za-z0-9]+$/.test(a.baselineVercelDeployId) &&
    a.schemaCompatibilityVerified === true && a.rollbackVerified === true &&
    a.effectiveRuntimeVerified === true && a.singleTriggerVerified === true &&
    a.configurationFreezeVerified === true, 'RECOVERY_EVIDENCE_REQUIRED');
  return Object.freeze(a);
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
  requireThat(String(run.id) === String(runId) && positiveAttempt(run.run_attempt), 'CI_IDENTITY_UNVERIFIABLE');
  const page = await api.gh(`/actions/runs/${runId}/attempts/${run.run_attempt}/jobs?per_page=100`);
  requireThat(page.total_count === page.jobs?.length, 'CI_INVENTORY_INCOMPLETE');
  const main = await api.gh('/git/ref/heads/main');
  const qualified = validateRun(run, page.jobs, main.object.sha);
  // The latest run can change while its attempt-specific jobs are being read.
  const current = await api.gh(`/actions/runs/${runId}`);
  requireThat(current.id === run.id && current.run_attempt === run.run_attempt, 'CI_QUALIFICATION_CHANGED');
  const currentQualified = validateRun(current, page.jobs, main.object.sha);
  requireThat(sourceIdentityFields.every(key => currentQualified[key] === qualified[key]), 'CI_QUALIFICATION_CHANGED');
  return Object.freeze(qualified);
}

export async function requalify(api, frozen) {
  const fresh = await qualify(api, frozen.ciRunId);
  requireThat(sourceIdentityFields.every(key => fresh[key] === frozen[key]), 'CI_QUALIFICATION_CHANGED');
  return fresh;
}

export function createAuditWriter(identity, emit) {
  validateReleaseIdentity(identity);
  const known = {};
  return row => {
    for (const key of ['renderDeployId', 'vercelDeployId', 'renderReleaseRunAttempt', 'vercelReleaseRunAttempt'])
      if (row[key] !== undefined) known[key] = row[key];
    emit({ ...row, ...Object.fromEntries(releaseIdentityFields.map(key => [key, identity[key]])), ...known });
  };
}

export function validateResumeReceipt(rows, frozen, authority) {
  requireThat(positiveAttempt(authority.resumeFromReleaseAttempt) && rows.length > 0 && rows.length <= 100,
    'RESUME_RECEIPT_REQUIRED');
  const known = {};
  for (const row of rows) {
    validateReleaseIdentity(row);
    requireThat(releaseIdentityFields.filter(key => key !== 'releaseRunAttempt')
      .every(key => row[key] === frozen[key]) && row.releaseRunAttempt === authority.resumeFromReleaseAttempt,
    'RESUME_RECEIPT_IDENTITY_MISMATCH');
    requireThat(['admitted', 'render-requested', 'render-ready', 'vercel-requested', 'verified', 'failed'].includes(row.phase),
      'RESUME_RECEIPT_UNVERIFIABLE');
    requireThat(row.phase !== 'verified', 'RELEASE_ALREADY_VERIFIED');
    for (const key of ['renderDeployId', 'vercelDeployId', 'renderReleaseRunAttempt', 'vercelReleaseRunAttempt']) {
      if (row[key] === undefined) continue;
      requireThat(known[key] === undefined || known[key] === row[key], 'CONTRADICTORY_RESUME_RECEIPT');
      known[key] = row[key];
    }
  }
  for (const provider of ['render', 'vercel']) {
    const id = known[`${provider}DeployId`], attempt = known[`${provider}ReleaseRunAttempt`];
    requireThat(id === undefined ? attempt === undefined :
      (positiveAttempt(attempt) && attempt <= authority.resumeFromReleaseAttempt &&
        (provider === 'render' ? /^dep-[a-z0-9]+$/ : /^dpl_[A-Za-z0-9]+$/).test(id)),
    'RESUME_RECEIPT_UNVERIFIABLE');
  }
  requireThat(!known.vercelDeployId || known.renderDeployId, 'CONTRADICTORY_RESUME_RECEIPT');
  return Object.freeze(known);
}

export async function resumeArtifact(api, candidate, authority) {
  if (authority.resumeFromReleaseAttempt === undefined) return '';
  const inventory = await api.gh(`/actions/runs/${candidate.releaseRunId}/artifacts?per_page=100`);
  requireThat(inventory.total_count === inventory.artifacts?.length, 'AUDIT_ARTIFACT_INVENTORY_INCOMPLETE');
  const name = `production-release-audit-${candidate.releaseRunId}-${authority.resumeFromReleaseAttempt}`;
  const selected = inventory.artifacts.filter(a => a.name === name && !a.expired);
  requireThat(selected.length === 1 && positiveId(String(selected[0].id)) &&
    selected[0].workflow_run?.id === Number(candidate.releaseRunId) &&
    selected[0].workflow_run?.head_sha === candidate.sha, 'TRUSTED_RESUME_ARTIFACT_REQUIRED');
  return String(selected[0].id);
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
  validateReleaseIdentity(qualified);
  await requalify(api, qualified);
  const prior = options?.receiptRows ? validateResumeReceipt(options.receiptRows, qualified, authority) : {};
  requireThat(authority.resumeFromReleaseAttempt === undefined || options?.receiptRows,
    'RESUME_RECEIPT_REQUIRED');
  requireThat(authority.resumeFromReleaseAttempt !== undefined || !options?.receiptRows,
    'UNAUTHORIZED_RESUME_RECEIPT');
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
    (live[0].id === prior.renderDeployId && live[0].commit?.id === qualified.sha)), 'RENDER_BASELINE_DRIFT');
  validateHealth(await api.health(), live[0].commit.id);
  requireThat(renderDeploys.every(x => !activeRender.has(x.status) || x.id === prior.renderDeployId),
    'CONCURRENT_RENDER_DEPLOYMENT');
  let render = prior.renderDeployId ? renderDeploys.find(x => x.id === prior.renderDeployId) : undefined;
  requireThat(!prior.renderDeployId || (render && render.commit?.id === qualified.sha),
    'RENDER_RECEIPT_DEPLOYMENT_MISMATCH');
  requireThat(renderDeploys.every(x => x.commit?.id !== qualified.sha || x.id === prior.renderDeployId),
    'RENDER_PROVENANCE_RECEIPT_REQUIRED');
  const vercelDeploys = await deploymentHistory(api, 'vercel');
  requireThat(vercelDeploys.every(x => !activeVercel.has(x.state ?? x.readyState) ||
    x.uid === prior.vercelDeployId), 'CONCURRENT_VERCEL_DEPLOYMENT');
  requireThat(vercelDeploys.every(x =>
    (x.meta?.aquaCiRunId !== qualified.ciRunId && x.meta?.aquaSourceSha !== qualified.sha) ||
    x.uid === prior.vercelDeployId), 'VERCEL_PROVENANCE_RECEIPT_REQUIRED');
  const origin = { ...qualified, releaseRunAttempt: prior.vercelReleaseRunAttempt ?? qualified.releaseRunAttempt };
  const isOurs = x => x.gitSource?.sha === qualified.sha &&
    String(x.gitSource?.repoId) === String(repository.id) && x.projectId === targets.vercel.id &&
    x.meta?.aquaCiRunId === qualified.ciRunId && x.meta?.aquaCiRunAttempt === String(qualified.ciRunAttempt) &&
    x.meta?.aquaReleaseIdentity === releaseKey(origin) && x.target === 'production';
  let existingVercel;
  for (const d of vercelDeploys.filter(x => x.uid === prior.vercelDeployId)) {
    requireThat(/^dpl_[A-Za-z0-9]+$/.test(d.uid), 'VERCEL_DEPLOYMENT_ID_UNAVAILABLE');
    const full = await api.vercel(`/v13/deployments/${d.uid}`);
    requireThat(isOurs(full), 'VERCEL_SOURCE_PROVENANCE_MISMATCH');
    requireThat(full.readyState === 'READY' || activeVercel.has(full.readyState), 'PRIOR_VERCEL_FAILURE_REQUIRES_OPERATOR');
    requireThat(!existingVercel, 'AMBIGUOUS_VERCEL_DEPLOYMENTS');
    existingVercel = full;
  }
  requireThat(!prior.vercelDeployId || existingVercel, 'VERCEL_RECEIPT_DEPLOYMENT_MISMATCH');
  requireThat(project.targets?.production?.id === authority.baselineVercelDeployId ||
    (existingVercel && project.targets?.production?.id === existingVercel.id), 'VERCEL_BASELINE_DRIFT');

  report({ phase: 'admitted', sha: qualified.sha, ciRunId: qualified.ciRunId,
    authorityReference: authority.authorityReference, baselineRenderDeployId: authority.baselineRenderDeployId,
    baselineVercelDeployId: authority.baselineVercelDeployId, ...prior });
  if (render) requireThat(render.status === 'live' || activeRender.has(render.status), 'PRIOR_RENDER_FAILURE_REQUIRES_OPERATOR');
  else {
    // Recheck freshness immediately before the irreversible provider request.
    await requalify(api, qualified);
    requireThat(Date.parse(authority.expiresAt) > (options?.now ?? Date.now)(), 'AUTHORITY_EXPIRED');
    render = await api.render(`${servicePath}/deploys`, { commitId: qualified.sha, clearCache: 'do_not_clear' });
  }
  requireThat(/^dep-[a-z0-9]+$/.test(render.id), 'RENDER_DEPLOYMENT_ID_UNAVAILABLE');
  report({ phase: 'render-requested', renderDeployId: render.id,
    renderReleaseRunAttempt: prior.renderReleaseRunAttempt ?? qualified.releaseRunAttempt });
  render = await waitFor(() => api.render(`${servicePath}/deploys/${render.id}`),
    x => x.status, 'live', activeRender, options);
  requireThat(render.commit?.id === qualified.sha, 'RENDER_SOURCE_PROVENANCE_MISMATCH');
  await readRender();
  validateHealth(await api.health(), qualified.sha);
  report({ phase: 'render-ready', renderDeployId: render.id, sha: render.commit.id });

  await requalify(api, qualified);
  project = await api.vercel(projectPath);
  validateVercel(project, repository.id);
  await readVercelVariables();
  if (!existingVercel) {
    await requalify(api, qualified);
    requireThat(Date.parse(authority.expiresAt) > (options?.now ?? Date.now)(), 'AUTHORITY_EXPIRED');
    existingVercel = await api.vercel('/v13/deployments', {
      name: targets.vercel.name, project: targets.vercel.id, target: 'production',
      gitSource: { type: 'github', repoId: String(repository.id), ref: qualified.sha, sha: qualified.sha },
      meta: { aquaCiRunId: qualified.ciRunId, aquaCiRunAttempt: String(qualified.ciRunAttempt),
        aquaSourceSha: qualified.sha, aquaReleaseIdentity: releaseKey(qualified) }
    });
  }
  requireThat(/^dpl_[A-Za-z0-9]+$/.test(existingVercel.id), 'VERCEL_DEPLOYMENT_ID_UNAVAILABLE');
  report({ phase: 'vercel-requested', vercelDeployId: existingVercel.id,
    vercelReleaseRunAttempt: prior.vercelReleaseRunAttempt ?? qualified.releaseRunAttempt });
  const vercel = await waitFor(() => api.vercel(`/v13/deployments/${existingVercel.id}`),
    x => x.readyState, 'READY', activeVercel, options);
  requireThat(isOurs(vercel) && vercel.projectId === targets.vercel.id && !vercel.aliasError,
    'VERCEL_SOURCE_PROVENANCE_MISMATCH');
  project = await api.vercel(projectPath);
  requireThat(project.targets?.production?.id === vercel.id, 'VERCEL_PRODUCTION_ALIAS_NOT_ASSIGNED');
  await api.web();
  await requalify(api, qualified);
  report({ phase: 'verified', sha: qualified.sha, renderDeployId: render.id, vercelDeployId: vercel.id });
}

let failureReport;
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
  const qualified = await qualify(api, event.workflow_run.id);
  requireThat(qualified.sha === event.workflow_run.head_sha && qualified.ciRunAttempt === event.workflow_run.run_attempt,
    'CI_EVENT_PROVENANCE_MISMATCH');
  const q = freezeCandidate(qualified, env);
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
  requireThat(mode === 'deploy' || mode === 'admit', 'RELEASE_MODE_INVALID');
  verifyCandidate(JSON.parse(readFileSync(`${env.CANDIDATE_DIR}/candidate.json`)), q,
    readFileSync(`${env.CANDIDATE_DIR}/source.tar`),
    execFileSync('git', ['archive', '--format=tar', q.sha], { maxBuffer: 100 * 1024 * 1024 }));
  const report = createAuditWriter(q, row => appendFileSync(env.RELEASE_REPORT, JSON.stringify(row) + '\n'));
  failureReport = code => report({ phase: 'failed', code });
  const authority = validateAuthority(env.PRODUCTION_RELEASE_AUTHORIZATION, q, env);
  const environment = await api.gh(`/environments/${targets.environment}`);
  const branches = await api.gh(`/environments/${targets.environment}/deployment-branch-policies`);
  validateEnvironment(environment, branches.branch_policies);
  if (mode === 'admit') {
    appendFileSync(env.GITHUB_OUTPUT, `receipt-artifact-id=${await resumeArtifact(api, q, authority)}\n`);
    return;
  }
  let receiptRows;
  if (authority.resumeFromReleaseAttempt !== undefined) {
    requireThat(positiveId(env.RESUME_ARTIFACT_ID) &&
      env.RESUME_ARTIFACT_ID === await resumeArtifact(api, q, authority), 'TRUSTED_RESUME_ARTIFACT_REQUIRED');
    let text;
    try { text = readFileSync(env.RESUME_RECEIPT_FILE, 'utf8');
      requireThat(text.length <= 1024 * 1024, 'RESUME_RECEIPT_UNVERIFIABLE');
      receiptRows = text.trim().split('\n').map(line => JSON.parse(line));
    } catch { throw new ReleaseFailure('RESUME_RECEIPT_UNVERIFIABLE'); }
  }
  await deploy(api, q, authority, report, { receiptRows });
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main(process.env).catch(error => {
    const code = error instanceof ReleaseFailure ? error.message : 'UNEXPECTED_RELEASE_ERROR';
    if (failureReport) failureReport(code);
    else if (process.env.RELEASE_REPORT)
      appendFileSync(process.env.RELEASE_REPORT, JSON.stringify({ phase: 'blocked', code, identityVerified: false }) + '\n');
    console.error(`::error::${code}. See the safe-release runbook; do not resume or retry provider requests blindly.`);
    if (process.env.GITHUB_STEP_SUMMARY)
      appendFileSync(process.env.GITHUB_STEP_SUMMARY, `Release blocked/failed: **${code}**.\nSee docs/operations/safe-automatic-deployment.md. No automatic rollback or configuration repair.\n`);
    process.exitCode = 1;
  });
}
