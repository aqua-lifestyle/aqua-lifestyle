#!/usr/bin/env python3
"""Harmless component tests. Synthetic positives are not human/provider acceptance."""

import base64
import copy
import importlib.util
import io
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest import mock

import yaml

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[4]
SPEC = importlib.util.spec_from_file_location("b6_approval", REPO / ".github/b6_approval.py")
GATE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(GATE)
DOCUMENT = yaml.load((REPO / GATE.WORKFLOW).read_text(), Loader=yaml.BaseLoader)


def encoded(value):
    return {"encoding": "base64", "content": base64.b64encode(value).decode()}


class FixtureAPI:
    def __init__(self):
        self.responses = {}
        self.calls = []

    def public(self, path):
        self.calls.append(("public", path))
        return self.responses[path]

    def private(self, path):
        self.calls.append(("private", path))
        if not path.startswith("repos/" + GATE.CUSTODY):
            raise AssertionError("private token used for public request")
        return self.responses[path]


class HandoffFixture(unittest.TestCase):
    def setUp(self):
        self.api = FixtureAPI()
        self.responses = self.api.responses
        self.env = {"GITHUB_REPOSITORY": GATE.CUSTODY, "GITHUB_EVENT_NAME": "workflow_dispatch",
                    "GITHUB_REF": "refs/heads/main", "GITHUB_SHA": "b" * 40,
                    "SOURCE_COMMIT": "a" * 40, "SOURCE_PR": "113", "AUTHORIZATION_ISSUE": "1",
                    "PUBLIC_WORKFLOW_COMMIT": "d" * 40, "HANDOFF_MODE": "qualification",
                    "PRIVILEGED_EXECUTION_ENABLED": "OWNER_AUTHORIZED_AFTER_INDEPENDENT_ACCEPTANCE",
                    "INDEPENDENT_REVIEWER": "independent-reviewer", "GITHUB_ACTOR": "executor",
                    "GITHUB_TRIGGERING_ACTOR": "executor", "GITHUB_RUN_ID": "100", "GITHUB_RUN_ATTEMPT": "1"}
        self.source, self.private = "repos/" + GATE.SOURCE, "repos/" + GATE.CUSTODY
        source, private = self.source, self.private
        self.responses[private] = {"visibility": "private"}
        self.responses[source] = {"visibility": "public"}
        self.responses[source + "/pulls/113"] = {"draft": False, "user": {"login": "implementation-author", "id": 11},
            "head": {"sha": "a" * 40, "repo": {"full_name": GATE.SOURCE}}}
        files = {GATE.PACKAGE + "candidate-baseline.json": (HERE / "candidate-baseline.json").read_bytes(),
                 GATE.PACKAGE + "PUBLIC-HANDOFF-AMENDMENT.md": (HERE / "PUBLIC-HANDOFF-AMENDMENT.md").read_bytes(),
                 GATE.WORKFLOW: (REPO / GATE.WORKFLOW).read_bytes(),
                 GATE.VERIFIER: (REPO / GATE.VERIFIER).read_bytes(),
                 GATE.PUBLIC_SOURCE: (REPO / GATE.PUBLIC_SOURCE).read_bytes()}
        for path, value in files.items():
            self.responses[source + "/contents/" + path + "?ref=" + "a" * 40] = encoded(value)
        self.responses[private + "/contents/" + GATE.WORKFLOW + "?ref=" + "b" * 40] = encoded(files[GATE.WORKFLOW])
        self.responses[source + "/contents/" + GATE.PUBLIC_CARRIER + "?ref=" + "d" * 40] = encoded(files[GATE.PUBLIC_SOURCE])
        self.responses[source + "/pulls/113/reviews?per_page=100&page=1"] = [{"id": 1,
            "user": {"login": "independent-reviewer", "id": 42}, "state": "APPROVED", "commit_id": "a" * 40,
            "body": "B6_SUPPLY_BASELINE_APPROVED sha256:" + GATE.sha(files[GATE.PACKAGE + "candidate-baseline.json"])
                    + "\nB6_PUBLIC_HANDOFF_AMENDMENT_ACCEPTED sha256:" + GATE.sha(files[GATE.PACKAGE + "PUBLIC-HANDOFF-AMENDMENT.md"])}]
        self.responses[source + "/environments/" + GATE.ENVIRONMENT] = {"id": GATE.ENVIRONMENT_ID,
            "can_admins_bypass": False, "protection_rules": [{"type": "required_reviewers", "prevent_self_review": True,
            "reviewers": [{"type": "User", "reviewer": {"type": "User", "login": "independent-reviewer", "id": 42}}]}]}
        self.responses[private + "/actions/runs/100/attempts/1"] = {
            "id": 100, "run_attempt": 1, "head_sha": "b" * 40, "path": GATE.WORKFLOW,
            "event": "workflow_dispatch", "status": "in_progress", "repository": {"full_name": GATE.CUSTODY},
            "actor": {"id": 2, "login": "executor"}, "triggering_actor": {"id": 2, "login": "executor"}}
        self.checked = GATE.context(self.env, self.api)
        self.permit = GATE.permit(self.env, self.checked, "c" * 64)
        digest = GATE.sha(GATE.canonical(self.permit).encode())
        self.responses[private + "/issues/1"] = {"user": {"login": "Xolani-M"}, "state": "open", "body": GATE.canonical({
            "schema": "B6PublicPrivateRunAuthorizationV1", "permit": self.permit,
            "public_run_id": "200", "public_run_attempt": "1", "independence_attestation": GATE.ATTESTATION})}
        run = {"id": 200, "run_attempt": 1, "head_sha": "d" * 40, "head_branch": GATE.PUBLIC_BRANCH,
               "workflow_id": GATE.PUBLIC_WORKFLOW_ID, "path": GATE.PUBLIC_CARRIER, "event": "workflow_dispatch",
               "head_repository": {"full_name": GATE.SOURCE}, "repository": {"full_name": GATE.SOURCE},
               "display_title": "B6 approval " + digest, "actor": {"id": 2, "login": "executor"},
               "triggering_actor": {"id": 2, "login": "executor"}, "status": "completed", "conclusion": "success"}
        self.responses[source + "/actions/runs/200/attempts/1"] = run
        self.responses[source + "/actions/runs/200"] = copy.deepcopy(run)
        self.responses[source + "/environments/" + GATE.ENVIRONMENT + "/deployment-branch-policies"] = {
            "branch_policies": [{"name": GATE.PUBLIC_BRANCH, "type": "branch"}]}
        self.responses[source + "/actions/runs/200/approvals"] = [{"environments": [{"id": GATE.ENVIRONMENT_ID}],
            "state": "approved", "user": {"login": "independent-reviewer", "id": 42, "type": "User"},
            "comment": "B6_HANDOFF_APPROVED " + GATE.canonical({"permit_sha256": digest, "public_run_id": "200", "public_run_attempt": "1"})}]
        self.responses[source + "/actions/runs/200/attempts/1/jobs?per_page=100"] = {
            "total_count": 1, "jobs": [{"name": "B6 public approval", "status": "completed", "conclusion": "success"}]}

    def verify(self):
        return GATE.verify_authorization(self.env, self.checked, self.permit, self.api)


class WorkflowGuardTests(HandoffFixture):
    def test_mock_exact_independent_record_passes_without_executing_any_workflow(self):
        result = self.verify()
        self.assertEqual(result["permit"], self.permit)
        self.assertEqual(result["public_approval"]["user"]["id"], 42)

    def test_existing_source_visibility_identity_and_review_denials_are_preserved(self):
        cases = [("unappointed", "owner-designated independent reviewer required"),
                 ("public-private-repo", "private custody repository required"),
                 ("trigger", "manual default-branch execution required"),
                 ("executor", "reviewer cannot author or execute qualification"),
                 ("private-visibility", "custody must remain private"),
                 ("admin-bypass", "public environment identity/no-bypass mismatch"),
                 ("no-environment-review", "independent environment protection required"),
                 ("self-review", "independent environment protection required"),
                 ("wrong-environment-reviewer", "designated reviewer must exclusively protect execution"),
                 ("source-author", "reviewer cannot author the source PR"),
                 ("source-fork", "reviewed source PR identity mismatch"),
                 ("stale-source", "reviewed source PR identity mismatch"),
                 ("no-review", "independent supply review missing"),
                 ("stale-review", "current exact-commit supply approval missing"),
                 ("wrong-baseline-review", "exact baseline approval missing"),
                 ("dismissed-review", "current exact-commit supply approval missing"),
                 ("workflow-substitution", "executing workflow differs from reviewed source")]
        original = copy.deepcopy(self.responses)
        original_env = dict(self.env)
        for case, reason in cases:
            with self.subTest(case=case):
                self.api.responses = self.responses = copy.deepcopy(original)
                self.env = dict(original_env)
                protection = self.responses[self.source + "/environments/" + GATE.ENVIRONMENT]
                pr = self.responses[self.source + "/pulls/113"]
                reviews = self.responses[self.source + "/pulls/113/reviews?per_page=100&page=1"]
                if case == "unappointed": self.env["INDEPENDENT_REVIEWER"] = ""
                elif case == "public-private-repo": self.env["GITHUB_REPOSITORY"] = GATE.SOURCE
                elif case == "trigger": self.env["GITHUB_EVENT_NAME"] = "push"
                elif case == "executor": self.env["GITHUB_ACTOR"] = "independent-reviewer"
                elif case == "private-visibility": self.responses[self.private]["visibility"] = "public"
                elif case == "admin-bypass": protection["can_admins_bypass"] = True
                elif case == "no-environment-review": protection["protection_rules"] = []
                elif case == "self-review": protection["protection_rules"][0]["prevent_self_review"] = False
                elif case == "wrong-environment-reviewer": protection["protection_rules"][0]["reviewers"] = []
                elif case == "source-author": pr["user"]["login"] = "independent-reviewer"
                elif case == "source-fork": pr["head"]["repo"]["full_name"] = "untrusted/fork"
                elif case == "stale-source": pr["head"]["sha"] = "e" * 40
                elif case == "no-review": reviews.clear()
                elif case == "stale-review": reviews[0]["commit_id"] = "e" * 40
                elif case == "wrong-baseline-review": reviews[0]["body"] = reviews[0]["body"].splitlines()[1]
                elif case == "dismissed-review": reviews.append(dict(reviews[0], id=2, state="DISMISSED"))
                elif case == "workflow-substitution": self.responses[self.private + "/contents/" + GATE.WORKFLOW + "?ref=" + "b" * 40] = encoded(b"different")
                with self.assertRaisesRegex(RuntimeError, reason):
                    GATE.context(self.env, self.api)

    def test_workflow_is_manual_private_and_shell_syntax_is_valid(self):
        self.assertEqual(list(DOCUMENT["on"]), ["workflow_dispatch"])
        self.assertEqual(DOCUMENT["on"]["workflow_dispatch"]["inputs"]["mode"]["default"], "handoff-test")
        self.assertEqual(DOCUMENT["concurrency"]["cancel-in-progress"], "false")
        test = DOCUMENT["jobs"]["handoff-test"]
        self.assertEqual(test["permissions"]["contents"], "read")
        self.assertEqual(len(test["steps"]), 2)
        self.assertNotIn("environment", test)
        self.assertEqual(test["steps"][0], DOCUMENT["jobs"]["qualify"]["steps"][0])
        for job in DOCUMENT["jobs"].values():
            for step in job["steps"]:
                result = subprocess.run(["bash", "-n"], input=step["run"], text=True, capture_output=True)
                self.assertEqual(result.returncode, 0, result.stderr)
        self.assertNotIn("sudo", "\n".join(s["run"] for s in test["steps"]))
        public = yaml.load((REPO / GATE.PUBLIC_SOURCE).read_text(), Loader=yaml.BaseLoader)
        self.assertEqual(public["permissions"], {})
        self.assertEqual(list(public["on"]["workflow_dispatch"]["inputs"]), ["permit_sha256"])
        self.assertNotIn("secrets.", (REPO / GATE.PUBLIC_SOURCE).read_text())


class HandoffBindingTests(HandoffFixture):
    def test_mock_full_preflight_records_exact_authority_without_supply_or_privilege(self):
        with tempfile.TemporaryDirectory(prefix="b6-handoff-component-") as directory:
            env = {**self.env, "RUNNER_TEMP": directory, "GITHUB_ENV": str(Path(directory) / "env")}
            with mock.patch.dict(os.environ, env, clear=True), mock.patch.object(GATE, "GitHub", return_value=self.api), \
                    mock.patch.object(GATE.secrets, "token_hex", return_value="c" * 64), mock.patch("builtins.print"):
                GATE.main()
            roots = list(Path(directory).glob("b6-qualification-*"))
            self.assertEqual(len(roots), 1)
            record = json.loads((roots[0] / "authorization.json").read_text())
            self.assertEqual(record["permit"], self.permit)
            self.assertEqual(roots[0].stat().st_mode & 0o777, 0o700)
            self.assertEqual({p.name for p in roots[0].iterdir()},
                             {"pending-permit.json", "authorization.json", "candidate-baseline.json"})

    def test_pending_timeout_never_records_verified_authority(self):
        self.responses[self.private + "/issues/1"]["body"] = '{"schema":"B6PublicPrivateAuthorizationPendingV1"}'
        with tempfile.TemporaryDirectory(prefix="b6-handoff-timeout-") as directory:
            env = {**self.env, "RUNNER_TEMP": directory, "GITHUB_ENV": str(Path(directory) / "env")}
            with mock.patch.dict(os.environ, env, clear=True), mock.patch.object(GATE, "GitHub", return_value=self.api), \
                    mock.patch.object(GATE.secrets, "token_hex", return_value="c" * 64), \
                    mock.patch.object(GATE.time, "monotonic", side_effect=[0, 0, 1201]), mock.patch("builtins.print"):
                with self.assertRaisesRegex(RuntimeError, "handoff approval timed out without authorization"):
                    GATE.main()
            root = next(Path(directory).glob("b6-qualification-*"))
            self.assertFalse((root / "authorization.json").exists())
            self.assertFalse((root / "candidate-baseline.json").exists())

    def test_late_successful_api_responses_cannot_release_after_deadline(self):
        with tempfile.TemporaryDirectory(prefix="b6-handoff-late-") as directory:
            env = {**self.env, "RUNNER_TEMP": directory, "GITHUB_ENV": str(Path(directory) / "env")}
            with mock.patch.dict(os.environ, env, clear=True), mock.patch.object(GATE, "GitHub", return_value=self.api), \
                    mock.patch.object(GATE.secrets, "token_hex", return_value="c" * 64), \
                    mock.patch.object(GATE.time, "monotonic", side_effect=[0, 0, 1201]), mock.patch("builtins.print"):
                with self.assertRaisesRegex(RuntimeError, "handoff approval timed out without authorization"):
                    GATE.main()
            root = next(Path(directory).glob("b6-qualification-*"))
            self.assertFalse((root / "authorization.json").exists())

    def test_every_private_permit_binding_rejects_substitution(self):
        original = self.responses[self.private + "/issues/1"]["body"]
        fields = ["source_commit", "baseline_sha256", "amendment_sha256", "private_workflow_commit",
                  "private_workflow_sha256", "verifier_sha256", "private_run_id", "private_run_attempt",
                  "public_workflow_commit", "public_workflow_sha256", "nonce", "mode"]
        for field in fields:
            with self.subTest(field=field):
                body = GATE.strict_json(original)
                body["permit"][field] = "SUBSTITUTED"
                self.responses[self.private + "/issues/1"]["body"] = GATE.canonical(body)
                with self.assertRaisesRegex(RuntimeError, "owner permit must bind exact"):
                    self.verify()
        self.responses[self.private + "/issues/1"]["body"] = original

    def test_missing_owner_and_human_attestation_reject(self):
        issue = self.responses[self.private + "/issues/1"]
        issue["user"]["login"] = "implementation-author"
        with self.assertRaisesRegex(RuntimeError, "owner authorization issue required"): self.verify()
        issue["user"]["login"] = "Xolani-M"
        body = GATE.strict_json(issue["body"])
        body["independence_attestation"] = "another account"
        issue["body"] = GATE.canonical(body)
        with self.assertRaisesRegex(RuntimeError, "genuine independent human appointment required"): self.verify()

    def test_native_approval_identity_and_comment_tampering_reject(self):
        key = self.source + "/actions/runs/200/approvals"
        original = copy.deepcopy(self.responses[key])
        for case in ("missing", "duplicate", "rejected", "wrong-person", "bot", "comment", "attempt"):
            with self.subTest(case=case):
                history = copy.deepcopy(original)
                if case == "missing": history.clear()
                elif case == "duplicate": history.append(copy.deepcopy(history[0]))
                elif case == "rejected": history[0]["state"] = "rejected"
                elif case == "wrong-person": history[0]["user"] = {"login": "Xolani-M", "id": 1, "type": "User"}
                elif case == "bot": history[0]["user"]["type"] = "Bot"
                elif case == "comment": history[0]["comment"] = "approved"
                elif case == "attempt": history[0]["comment"] = history[0]["comment"].replace('"public_run_attempt":"1"', '"public_run_attempt":"2"')
                self.responses[key] = history
                with self.assertRaisesRegex(RuntimeError, "missing or ambiguous|independent native approval"):
                    self.verify()
        self.responses[key] = original

    def test_public_run_workflow_commit_branch_title_and_actor_bindings_reject(self):
        key = self.source + "/actions/runs/200/attempts/1"
        original = copy.deepcopy(self.responses[key])
        changes = {"id": 201, "run_attempt": 2, "head_sha": "e" * 40, "head_branch": "main",
                   "workflow_id": 99, "path": "other.yml", "event": "push", "display_title": "green"}
        for field, value in changes.items():
            with self.subTest(field=field):
                self.responses[key] = dict(copy.deepcopy(original), **{field: value})
                with self.assertRaisesRegex(RuntimeError, "public run/workflow/permit identity mismatch"): self.verify()
        self.responses[key] = copy.deepcopy(original)
        self.responses[key]["triggering_actor"] = {"id": 42, "login": "independent-reviewer"}
        with self.assertRaisesRegex(RuntimeError, "public reviewer cannot initiate or rerun its gate"): self.verify()

    def test_public_rerun_failed_job_and_missing_protection_reject(self):
        self.responses[self.source + "/actions/runs/200"]["run_attempt"] = 2
        with self.assertRaisesRegex(RuntimeError, "public run rerun invalidates approval"): self.verify()
        self.responses[self.source + "/actions/runs/200"]["run_attempt"] = 1
        jobs = self.responses[self.source + "/actions/runs/200/attempts/1/jobs?per_page=100"]
        jobs["jobs"][0]["conclusion"] = "failure"
        with self.assertRaisesRegex(RuntimeError, "public gate did not complete successfully"): self.verify()
        jobs["jobs"][0]["conclusion"] = "success"
        self.responses[self.source + "/environments/" + GATE.ENVIRONMENT + "/deployment-branch-policies"]["branch_policies"] = []
        with self.assertRaisesRegex(RuntimeError, "public gate branch protection mismatch"): self.verify()

    def test_explicit_pending_states_never_return_verified_authority(self):
        issue = self.responses[self.private + "/issues/1"]
        original = issue["body"]
        issue["body"] = '{"schema":"B6PublicPrivateAuthorizationPendingV1"}'
        with self.assertRaisesRegex(GATE.PendingApproval, "OWNER_AUTHORIZATION_PENDING"): self.verify()
        issue["body"] = original
        self.responses[self.source + "/actions/runs/200/attempts/1"]["status"] = "waiting"
        self.responses[self.source + "/actions/runs/200/approvals"] = []
        with self.assertRaisesRegex(GATE.PendingApproval, "PUBLIC_INDEPENDENT_APPROVAL_PENDING"): self.verify()

    def test_operational_mode_cannot_use_unaccepted_amendment_or_unset_enablement(self):
        self.env["PRIVILEGED_EXECUTION_ENABLED"] = ""
        with self.assertRaisesRegex(RuntimeError, "privileged execution remains owner-disabled"): GATE.context(self.env, self.api)
        self.env["PRIVILEGED_EXECUTION_ENABLED"] = "OWNER_AUTHORIZED_AFTER_INDEPENDENT_ACCEPTANCE"
        review = self.responses[self.source + "/pulls/113/reviews?per_page=100&page=1"][0]
        review["body"] = review["body"].splitlines()[0]
        with self.assertRaisesRegex(RuntimeError, "handoff amendment remains pending independent acceptance"): GATE.context(self.env, self.api)
        self.env["HANDOFF_MODE"] = "handoff-test"
        self.assertEqual(GATE.context(self.env, self.api)["mode"], "handoff-test")
        # A harmless acceptance cannot authorize the operational permit.
        self.assertNotEqual(GATE.permit(self.env, GATE.context(self.env, self.api), "c" * 64), self.permit)

    def test_strict_json_and_nonce_do_not_normalize_invalid_authority(self):
        with self.assertRaisesRegex(RuntimeError, "duplicate authority JSON key"):
            GATE.strict_json('{"approved":false,"approved":true}')
        with self.assertRaisesRegex(RuntimeError, "fresh nonce identity required"):
            GATE.permit(self.env, self.checked, "")

    def test_renamed_author_or_executor_and_wrong_private_run_reject(self):
        pr = self.responses[self.source + "/pulls/113"]
        pr["user"]["id"] = 42
        with self.assertRaisesRegex(RuntimeError, "source PR by immutable identity"):
            GATE.context(self.env, self.api)
        pr["user"]["id"] = 11
        private_run = self.responses[self.private + "/actions/runs/100/attempts/1"]
        private_run["actor"]["id"] = 42
        with self.assertRaisesRegex(RuntimeError, "private reviewer cannot execute or rerun by immutable identity"):
            GATE.context(self.env, self.api)
        private_run["actor"]["id"] = 2
        private_run["head_sha"] = "e" * 40
        with self.assertRaisesRegex(RuntimeError, "private workflow/run identity mismatch"):
            GATE.context(self.env, self.api)

    def test_public_api_transport_never_receives_private_token(self):
        class Response(io.BytesIO):
            status = 200
            def geturl(self): return "https://api.github.com/repos/" + GATE.SOURCE
        def response(request, timeout):
            self.assertNotIn("Authorization", dict(request.header_items()))
            return Response(b'{"visibility":"public"}')
        with mock.patch.dict(os.environ, {"GH_TOKEN": "SYNTHETIC_PRIVATE_TOKEN"}), mock.patch("urllib.request.urlopen", side_effect=response):
            self.assertEqual(GATE.GitHub().public("repos/" + GATE.SOURCE), {"visibility": "public"})
        with self.assertRaisesRegex(RuntimeError, "private credential scope mismatch"):
            GATE.GitHub().private("repos/" + GATE.SOURCE)

    def test_only_exact_commit_contents_are_cached_not_mutable_authority(self):
        urls = []
        class Response(io.BytesIO):
            status = 200
            def geturl(self): return self.url
        def response(request, timeout):
            urls.append(request.full_url)
            result = Response(b'{"value":"fixture"}')
            result.url = request.full_url
            return result
        api = GATE.GitHub()
        with mock.patch("urllib.request.urlopen", side_effect=response):
            immutable = "repos/" + GATE.SOURCE + "/contents/file?ref=" + "a" * 40
            api.public(immutable)
            api.public(immutable)
            mutable = "repos/" + GATE.SOURCE + "/actions/runs/200/approvals"
            api.public(mutable)
            api.public(mutable)
        self.assertEqual(len(urls), 3)


if __name__ == "__main__":
    unittest.main(verbosity=2)
