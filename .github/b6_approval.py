#!/usr/bin/env python3
"""Read-only public/private approval preflight. Never acquires supply or uses sudo."""

import base64
import hashlib
import json
import os
from pathlib import Path
import re
import secrets
import subprocess
import tempfile
import time
import urllib.request

SOURCE = "aqua-lifestyle/aqua-lifestyle"
CUSTODY = "aqua-lifestyle/b6-qualification-evidence"
PACKAGE = "docs/operations/aqgreen-v2-b6/snapshot-package/replacement-supply/"
WORKFLOW = ".github/workflows/b6-qualification.yml"
VERIFIER = ".github/b6_approval.py"
PUBLIC_SOURCE = ".github/workflows/b6-public-approval.yml"
PUBLIC_CARRIER = ".github/workflows/ci.yml"
PUBLIC_BRANCH = "test/b6-public-approval-gate-20261021"
PUBLIC_WORKFLOW_ID = 314866230
ENVIRONMENT = "b6-public-approval-probe"
ENVIRONMENT_ID = 24924994010
ATTESTATION = "OWNER_ATTESTS_A_DIFFERENT_INDEPENDENT_HUMAN_NOT_AN_AUTHOR_CONTROLLED_ACCOUNT"


class PendingApproval(Exception):
    """An explicitly pending authorization, never a successful verification."""


def require(value, reason):
    if not value:
        raise RuntimeError(reason)


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=True)


def sha(value):
    return hashlib.sha256(value).hexdigest()


def strict_json(value):
    def pairs(items):
        result = {}
        for key, item in items:
            require(key not in result, "duplicate authority JSON key")
            result[key] = item
        return result
    return json.loads(value, object_pairs_hook=pairs)


class GitHub:
    def __init__(self):
        self.immutable_contents = {}

    def public(self, path):
        # Do not send the private GITHUB_TOKEN to the public repository.
        immutable = "/contents/" in path and re.search(r"\?ref=[0-9a-f]{40}$", path) is not None
        if immutable and path in self.immutable_contents:
            return self.immutable_contents[path]
        url = "https://api.github.com/" + path
        request = urllib.request.Request(url, headers={"Accept": "application/vnd.github+json",
                                                     "User-Agent": "B6-approval-preflight"})
        with urllib.request.urlopen(request, timeout=30) as response:
            require(response.status == 200 and response.geturl() == url, "public authority response mismatch")
            result = strict_json(response.read())
        if immutable:
            self.immutable_contents[path] = result
        return result

    def private(self, path):
        require(path.startswith("repos/" + CUSTODY + "/") or path == "repos/" + CUSTODY,
                "private credential scope mismatch")
        return strict_json(subprocess.check_output(["gh", "api", path]))


def contents(api, repository, path, ref):
    getter = api.private if repository == CUSTODY else api.public
    result = getter(f"repos/{repository}/contents/{path}?ref={ref}")
    require(result["encoding"] == "base64", "authority file encoding mismatch")
    return base64.b64decode("".join(result["content"].splitlines()), validate=True)


def context(env, api):
    require(env["GITHUB_REPOSITORY"] == CUSTODY, "private custody repository required")
    require(env["GITHUB_EVENT_NAME"] == "workflow_dispatch" and env["GITHUB_REF"] == "refs/heads/main",
            "manual default-branch execution required")
    mode = env["HANDOFF_MODE"]
    require(mode in ("handoff-test", "qualification"), "explicit handoff mode required")
    if mode == "qualification":
        require(env.get("PRIVILEGED_EXECUTION_ENABLED") == "OWNER_AUTHORIZED_AFTER_INDEPENDENT_ACCEPTANCE",
                "privileged execution remains owner-disabled")
    source, public_commit = env["SOURCE_COMMIT"], env["PUBLIC_WORKFLOW_COMMIT"]
    require(re.fullmatch("[0-9a-f]{40}", source) and re.fullmatch("[0-9a-f]{40}", public_commit), "full workflow/source commits required")
    require(re.fullmatch("[0-9a-f]{40}", env["GITHUB_SHA"]), "private workflow commit required")
    for key in ("SOURCE_PR", "AUTHORIZATION_ISSUE", "GITHUB_RUN_ID", "GITHUB_RUN_ATTEMPT"):
        require(re.fullmatch("[1-9][0-9]*", env[key]), "numeric authority/run identities required")
    reviewer = env.get("INDEPENDENT_REVIEWER", "")
    require(re.fullmatch("[A-Za-z0-9][A-Za-z0-9-]{0,38}", reviewer), "owner-designated independent reviewer required")
    forbidden = {"xolani-m", env["GITHUB_ACTOR"].lower(), env["GITHUB_TRIGGERING_ACTOR"].lower()}
    require(reviewer.lower() not in forbidden, "reviewer cannot author or execute qualification")
    require(api.private(f"repos/{CUSTODY}")["visibility"] == "private", "custody must remain private")
    require(api.public(f"repos/{SOURCE}")["visibility"] == "public", "public approval authority must remain public")
    pr = api.public(f"repos/{SOURCE}/pulls/{env['SOURCE_PR']}")
    require(pr["head"]["repo"]["full_name"] == SOURCE and pr["head"]["sha"] == source,
            "reviewed source PR identity mismatch")
    require(mode == "handoff-test" or not pr["draft"], "operational source PR remains draft")
    require(pr["user"]["login"].lower() != reviewer.lower(), "reviewer cannot author the source PR")
    baseline_bytes = contents(api, SOURCE, PACKAGE + "candidate-baseline.json", source)
    baseline = strict_json(baseline_bytes)
    require(baseline["custody_repository"] == CUSTODY and baseline["status"] == "CANDIDATE_NOT_APPROVED",
            "replacement baseline identity mismatch")
    amendment = contents(api, SOURCE, PACKAGE + "PUBLIC-HANDOFF-AMENDMENT.md", source)
    private_workflow = contents(api, CUSTODY, WORKFLOW, env["GITHUB_SHA"])
    require(private_workflow == contents(api, SOURCE, WORKFLOW, source),
            "executing workflow differs from reviewed source")
    verifier = Path(__file__).read_bytes()
    require(verifier == contents(api, SOURCE, VERIFIER, source), "executing verifier differs from reviewed source")
    public_workflow = contents(api, SOURCE, PUBLIC_CARRIER, public_commit)
    require(public_workflow == contents(api, SOURCE, PUBLIC_SOURCE, source), "public approval workflow substitution")
    protection = api.public(f"repos/{SOURCE}/environments/{ENVIRONMENT}")
    require(protection["id"] == ENVIRONMENT_ID and protection.get("can_admins_bypass") is False,
            "public environment identity/no-bypass mismatch")
    rules = [r for r in protection["protection_rules"] if r["type"] == "required_reviewers"]
    require(len(rules) == 1 and rules[0].get("prevent_self_review") is True, "independent environment protection required")
    reviewers = rules[0]["reviewers"]
    require(len(reviewers) == 1 and reviewers[0]["type"] == "User"
            and reviewers[0]["reviewer"]["login"].lower() == reviewer.lower(), "designated reviewer must exclusively protect execution")
    identity = reviewers[0]["reviewer"]
    require(identity["type"] == "User" and type(identity["id"]) is int and identity["id"] > 0,
            "independent human account identity required")
    require(pr["user"]["id"] != identity["id"], "reviewer cannot author the source PR by immutable identity")
    private_run = api.private(f"repos/{CUSTODY}/actions/runs/{env['GITHUB_RUN_ID']}/attempts/{env['GITHUB_RUN_ATTEMPT']}")
    require(type(private_run["id"]) is int and private_run["id"] == int(env["GITHUB_RUN_ID"])
            and type(private_run["run_attempt"]) is int and private_run["run_attempt"] == int(env["GITHUB_RUN_ATTEMPT"])
            and private_run["head_sha"] == env["GITHUB_SHA"] and private_run["path"] == WORKFLOW
            and private_run["event"] == "workflow_dispatch" and private_run["status"] == "in_progress"
            and private_run["repository"]["full_name"] == CUSTODY, "private workflow/run identity mismatch")
    for actor in (private_run["actor"], private_run["triggering_actor"]):
        require(type(actor["id"]) is int and actor["id"] > 0 and actor["id"] != identity["id"],
                "private reviewer cannot execute or rerun by immutable identity")
    review = None
    if mode == "qualification":
        reviews, page = [], 1
        while True:
            batch = api.public(f"repos/{SOURCE}/pulls/{env['SOURCE_PR']}/reviews?per_page=100&page={page}")
            reviews.extend(batch)
            if len(batch) < 100:
                break
            page += 1
        reviews = [r for r in reviews if r["user"]["id"] == identity["id"] and r["state"] != "COMMENTED"]
        require(reviews, "independent supply review missing")
        review = max(reviews, key=lambda r: r["id"])
        require(review["state"] == "APPROVED" and review["commit_id"] == source
                and review["user"]["login"].lower() == reviewer.lower(), "current exact-commit supply approval missing")
        lines = (review["body"] or "").splitlines()
        require("B6_SUPPLY_BASELINE_APPROVED sha256:" + sha(baseline_bytes) in lines, "exact baseline approval missing")
        require("B6_PUBLIC_HANDOFF_AMENDMENT_ACCEPTED sha256:" + sha(amendment) in lines,
                "handoff amendment remains pending independent acceptance")
    return {"mode": mode, "baseline_bytes": baseline_bytes, "baseline_sha256": sha(baseline_bytes),
            "amendment_sha256": sha(amendment), "private_workflow_sha256": sha(private_workflow),
            "verifier_sha256": sha(verifier), "public_workflow_sha256": sha(public_workflow),
            "reviewer": {"login": identity["login"], "id": identity["id"]},
            "protection": protection, "source_review": review}


def permit(env, checked, nonce):
    require(re.fullmatch("[0-9a-f]{64}", nonce), "fresh nonce identity required")
    return {"schema": "B6PublicPrivateApprovalPermitV1", "mode": checked["mode"],
            "source_repository": SOURCE, "source_commit": env["SOURCE_COMMIT"], "source_pr": int(env["SOURCE_PR"]),
            "baseline_sha256": checked["baseline_sha256"], "amendment_sha256": checked["amendment_sha256"],
            "private_repository": CUSTODY, "private_workflow_commit": env["GITHUB_SHA"],
            "private_workflow_sha256": checked["private_workflow_sha256"], "verifier_sha256": checked["verifier_sha256"],
            "private_run_id": env["GITHUB_RUN_ID"], "private_run_attempt": env["GITHUB_RUN_ATTEMPT"],
            "public_repository": SOURCE, "public_workflow_commit": env["PUBLIC_WORKFLOW_COMMIT"],
            "public_workflow_path": PUBLIC_CARRIER, "public_workflow_id": PUBLIC_WORKFLOW_ID,
            "public_workflow_sha256": checked["public_workflow_sha256"], "public_environment_id": ENVIRONMENT_ID,
            "owner_authorization_issue": int(env["AUTHORIZATION_ISSUE"]), "reviewer": checked["reviewer"], "nonce": nonce}


def verify_authorization(env, checked, expected_permit, api):
    issue = api.private(f"repos/{CUSTODY}/issues/{env['AUTHORIZATION_ISSUE']}")
    require(issue["user"]["login"] == "Xolani-M" and issue["state"] == "open" and "pull_request" not in issue,
            "owner authorization issue required")
    authority = strict_json(issue["body"])
    if authority == {"schema": "B6PublicPrivateAuthorizationPendingV1"}:
        raise PendingApproval("OWNER_AUTHORIZATION_PENDING")
    require(set(authority) == {"schema", "permit", "public_run_id", "public_run_attempt", "independence_attestation"}
            and authority["schema"] == "B6PublicPrivateRunAuthorizationV1", "authorization contract mismatch")
    require(canonical(authority["permit"]) == canonical(expected_permit), "owner permit must bind exact source, baseline, workflows, run, attempt and nonce")
    require(authority["independence_attestation"] == ATTESTATION, "genuine independent human appointment required")
    run_id, attempt = authority["public_run_id"], authority["public_run_attempt"]
    require(isinstance(run_id, str) and re.fullmatch("[1-9][0-9]*", run_id) and attempt == "1", "fresh public run attempt required")
    run = api.public(f"repos/{SOURCE}/actions/runs/{run_id}/attempts/{attempt}")
    digest = sha(canonical(expected_permit).encode())
    require(type(run["id"]) is int and run["id"] == int(run_id)
            and type(run["run_attempt"]) is int and run["run_attempt"] == 1
            and run["head_sha"] == env["PUBLIC_WORKFLOW_COMMIT"] and run["head_branch"] == PUBLIC_BRANCH
            and run["workflow_id"] == PUBLIC_WORKFLOW_ID and run["path"] == PUBLIC_CARRIER
            and run["event"] == "workflow_dispatch" and run["head_repository"]["full_name"] == SOURCE
            and run["repository"]["full_name"] == SOURCE and run["display_title"] == "B6 approval " + digest,
            "public run/workflow/permit identity mismatch")
    reviewer = checked["reviewer"]
    for actor in (run["actor"], run["triggering_actor"]):
        require(type(actor["id"]) is int and actor["id"] > 0 and actor["id"] != reviewer["id"]
                and actor["login"].lower() != reviewer["login"].lower(), "public reviewer cannot initiate or rerun its gate")
    current = api.public(f"repos/{SOURCE}/actions/runs/{run_id}")
    require(type(current["run_attempt"]) is int and current["run_attempt"] == 1, "public run rerun invalidates approval")
    policies = api.public(f"repos/{SOURCE}/environments/{ENVIRONMENT}/deployment-branch-policies")["branch_policies"]
    require(len(policies) == 1 and policies[0]["name"] == PUBLIC_BRANCH and policies[0]["type"] == "branch",
            "public gate branch protection mismatch")
    history = api.public(f"repos/{SOURCE}/actions/runs/{run_id}/approvals")
    history = [r for r in history if any(e["id"] == ENVIRONMENT_ID for e in r["environments"])]
    if not history and run["status"] in ("waiting", "queued", "in_progress", "pending"):
        raise PendingApproval("PUBLIC_INDEPENDENT_APPROVAL_PENDING")
    require(len(history) == 1, "missing or ambiguous native public approval")
    approval = history[0]
    comment = "B6_HANDOFF_APPROVED " + canonical({"permit_sha256": digest, "public_run_id": run_id, "public_run_attempt": attempt})
    require(approval["state"] == "approved" and approval["user"]["type"] == "User"
            and approval["user"]["id"] == reviewer["id"] and approval["user"]["login"].lower() == reviewer["login"].lower()
            and approval["comment"] == comment, "independent native approval identity/comment mismatch")
    jobs = api.public(f"repos/{SOURCE}/actions/runs/{run_id}/attempts/{attempt}/jobs?per_page=100")
    require(type(jobs["total_count"]) is int and jobs["total_count"] == 1
            and len(jobs["jobs"]) == 1 and jobs["jobs"][0]["name"] == "B6 public approval",
            "public gate job identity mismatch")
    if run["status"] == "in_progress" and jobs["jobs"][0]["status"] in ("queued", "in_progress", "waiting"):
        raise PendingApproval("PUBLIC_GATE_COMPLETION_PENDING")
    require(run["status"] == "completed" and run["conclusion"] == "success"
            and jobs["jobs"][0]["status"] == "completed" and jobs["jobs"][0]["conclusion"] == "success",
            "public gate did not complete successfully")
    return {"owner_issue": issue, "public_approval": approval, "public_run": run, "public_jobs": jobs,
            "permit": expected_permit, "permit_sha256": digest}


def main():
    os.umask(0o077)
    env, api = dict(os.environ), GitHub()
    checked = context(env, api)
    root = Path(tempfile.mkdtemp(prefix="b6-qualification-", dir=env["RUNNER_TEMP"]))
    root.chmod(0o700)
    expected = permit(env, checked, secrets.token_hex(32))
    digest = sha(canonical(expected).encode())
    (root / "pending-permit.json").write_text(canonical(expected) + "\n")
    print("B6_PENDING_PERMIT_SHA256=" + digest, flush=True)
    print("B6_PENDING_PRIVATE_PERMIT=" + canonical(expected), flush=True)
    print("Scope is " + checked["mode"] + "; pending metadata is not authorization.", flush=True)
    with open(env["GITHUB_ENV"], "a") as stream:
        stream.write("B6_ROOT=" + str(root) + "\n")
    deadline = time.monotonic() + 1200
    while True:
        require(time.monotonic() <= deadline, "handoff approval timed out without authorization")
        try:
            evidence = verify_authorization(env, checked, expected, api)
            # Revalidate mutable PR review/protection/visibility immediately before release.
            fresh = context(env, api)
            require(canonical(permit(env, fresh, expected["nonce"])) == canonical(expected), "authority changed while awaiting approval")
            evidence = verify_authorization(env, fresh, expected, api)
            require(time.monotonic() <= deadline, "handoff approval timed out without authorization")
            evidence["source_review"], evidence["environment"] = fresh["source_review"], fresh["protection"]
            (root / "authorization.json").write_text(json.dumps(evidence, indent=2) + "\n")
            (root / "candidate-baseline.json").write_bytes(fresh["baseline_bytes"])
            print("B6_HANDOFF_VERIFIED scope=" + checked["mode"] + " permit_sha256=" + digest)
            return
        except PendingApproval as pending:
            print(str(pending), flush=True)
            remaining = deadline - time.monotonic()
            require(remaining > 0, "handoff approval timed out without authorization")
            # Private owner-record polling uses the scoped token. Public reads
            # are anonymous; bound their cadence and cache only immutable Git
            # contents, never mutable reviews, visibility or protection state.
            time.sleep(min(15 if str(pending) == "OWNER_AUTHORIZATION_PENDING" else 180, remaining))


if __name__ == "__main__":
    main()
