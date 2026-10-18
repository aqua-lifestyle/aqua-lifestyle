# Credential-free deployment executor

Owner instruction of 2026-10-18 authorizes implementation using the current
authenticated account. Custodian login is not an engineering prerequisite. Actual
credential custody remains BLOCKED; no production secrets or provider changes are
authorized by implementation, tests, or a merge of these files.

## Implemented route

Application PR106 replaces its provider-bearing release jobs with one
`workflow_run` trigger. Successful same-repository main-push CI automatically
dispatches `aqua-lifestyle/aqua-deployment/.github/workflows/deploy.yml@main`.
This is a machine API call, not a manual workflow-dispatch prerequisite.

The three inputs are source SHA, CI run ID and attempt. They are hints, not release
authority. The executor re-fetches current main, that exact successful push run,
all 17 attempt-specific jobs and their GitHub Actions App15368 check records.
Extra inputs, wrong/fork/PR/stale CI, missing/skipped/duplicate checks and wrong
attempts are rejected. The existing merged-PR and governance checks remain.

The executor holds its own reviewed controller, targets and policy. It never
imports application deployment scripts, workflows, package hooks or target files.
Both jobs check out the executor revision. Application source is fetched from the
fixed public Git origin as inert objects, never checked out or executed. The
candidate job has no provider secrets; the production job independently recreates
the Git archive and verifies the candidate digest, bytes and full identity.
Version4 receipts bind source/CI plus executor repository, SHA, run and attempt.

The reused PR106 controller retains Render exact-commit deployment, backend
revision/dependency/health verification before Vercel, exact frontend Git source,
production alias/readiness/revision checks and redacted immutable receipts. It
retains serialized releases, no blind POST retries, earlier-attempt receipt chains,
verified predecessor selection and fail-closed legacy-release ambiguity handling.
There is no parallel application provider workflow, release activation flag,
per-release token or human approval gate.

## Smallest one-time setup, outside this implementation

1. Review/install the executor branch on its main and merge PR106 only under
   separately authorized merge procedures. Its initial main has documentation only;
   no deployment workflow is active there during preparation.
2. Before provider credentials: qualify organization base, team, App and automation
   access. Ordinary application writers must not control executor source, workflows
   or secrets. `Xolani-M` remains an administrator by current instruction, so this
   boundary is BLOCKED. Designated custodian is `aqualifestyleclub`. The independent
   mandatory-test/verifier custody gap also remains unresolved: App15368 success is
   not proof that candidate-controlled mandatory test bodies were not weakened.
3. Apply the already prepared main history protections with no bypass actors and
   main-only `production-release` with `can_admins_bypass=false`, no reviewers or
   wait. Restrict Actions to reviewed full-SHA dependencies, read default tokens,
   and no workflow PR approvals. Verify actual readbacks; none were changed here.
4. Create an executor-only triggering GitHub App with Actions:write and implicit
   Metadata:read, without Contents/Workflows/Administration/Secrets write. Configure
   application variable `DEPLOYMENT_TRIGGER_APP_ID` and secret
   `DEPLOYMENT_TRIGGER_APP_PRIVATE_KEY`. These are trigger-only authentication;
   never place Render/Vercel credentials in the application repository. GitHub's
   [workflow dispatch API](https://docs.github.com/en/rest/actions/workflows#create-a-workflow-dispatch-event)
   supports Actions:write; `repository_dispatch` requiring Contents write is excluded.
5. Verify the executor's read token can observe all public source CI/check/PR and
   governance records plus executor environment/history/artifact records. Missing
   bypass-actor visibility fails closed. Do not grant runtime administration simply
   to hide this prerequisite; a supported observation permission route must be
   qualified before provisioning provider credentials.
6. Only after custody is qualified, separately authorize persistent
   `RENDER_API_KEY` and `VERCEL_TOKEN` in the executor's production environment.
   Render's observed ADMIN key has broad workspace exposure; qualify a dedicated
   narrower identity. Use persistent Vercel team authentication, not the expiring
   local CLI session. Neither credential has been provisioned by this task.
7. Resolve the existing Render runtime/DB privilege, Vercel Git-source access/public
   build input/baseline provenance, backup/restore and monitoring prerequisites in
   [the infrastructure plan](production-infrastructure-plan.md). The recovery-only,
   migration, financial, messaging and worker checks are preserved. Native provider
   auto-deploy must stay disabled so it cannot race the executor. These are initial
   infrastructure requirements, not recurring release authorization.

Before live enablement, prove ordinary-identity executor writes/secret management
are denied, test the scoped trigger and governance reads, and qualify exact-source
transport with authorized non-production provider resources. Only an observed live
Render/Vercel rollout can establish production deployment success. Current provider
tests use substitutes; local Git provenance tests do not prove hosted isolation or
provider deployment. No B6/B7 business commissioning is authorized here.
