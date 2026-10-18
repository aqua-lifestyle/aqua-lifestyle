// The only cross-repository capability is Actions:write on the executor. No
// provider credential, executable payload, target or artifact URL is transported.
import { readFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';
import { clients, qualify, requireThat, targets } from './release-control.mjs';

export async function trigger(env, fetcher = fetch, api = clients(env, fetcher)) {
  requireThat(env.GITHUB_REPOSITORY === targets.repository && env.GITHUB_EVENT_NAME === 'workflow_run' &&
    env.GITHUB_REF === 'refs/heads/main', 'TRUSTED_CI_COMPLETION_REQUIRED');
  const event = JSON.parse(readFileSync(env.GITHUB_EVENT_PATH));
  const run = event.workflow_run;
  requireThat(event.action === 'completed' && run?.repository?.full_name === targets.repository &&
    run.head_repository?.full_name === targets.repository && run.event === 'push' &&
    run.head_branch === 'main' && run.status === 'completed' && run.conclusion === 'success',
    'COMPLETED_MAIN_PUSH_REQUIRED');
  const qualified = await qualify(api, run.id);
  requireThat(qualified.sha === run.head_sha && qualified.ciRunAttempt === run.run_attempt,
    'CI_EVENT_PROVENANCE_MISMATCH');
  requireThat(env.EXECUTOR_TRIGGER_TOKEN?.trim(), 'EXECUTOR_TRIGGER_CREDENTIAL_REQUIRED');
  let response;
  try {
    response = await fetcher('https://api.github.com/repos/aqua-lifestyle/aqua-deployment/actions/workflows/deploy.yml/dispatches', {
      method: 'POST', redirect: 'error', signal: AbortSignal.timeout(30000),
      headers: { Authorization: `Bearer ${env.EXECUTOR_TRIGGER_TOKEN}`, Accept: 'application/vnd.github+json',
        'Content-Type': 'application/json', 'X-GitHub-Api-Version': '2022-11-28' },
      body: JSON.stringify({ ref: 'main', inputs: { ci_run_id: qualified.ciRunId,
        ci_run_attempt: String(qualified.ciRunAttempt), sha: qualified.sha } })
    });
  } catch { throw Error('EXECUTOR_DISPATCH_AMBIGUOUS_INSPECT_BEFORE_RETRY'); }
  requireThat(response.status === 204, 'EXECUTOR_DISPATCH_REJECTED');
}
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try { await trigger(process.env); }
  catch { console.error('::error::Automatic executor dispatch failed. Inspect the run; no provider operation was issued here.'); process.exitCode = 1; }
}
