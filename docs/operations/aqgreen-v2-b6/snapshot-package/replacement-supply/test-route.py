#!/usr/bin/env python3
"""Non-privileged security regression checks; never dispatches a workflow."""

import base64
import copy
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import subprocess
import tarfile
import tempfile
import unittest
from unittest import mock

import yaml


HERE = Path(__file__).resolve().parent
REPO = HERE.parents[4]
WORKFLOW = REPO / ".github/workflows/b6-qualification.yml"
DOCUMENT = yaml.load(WORKFLOW.read_text(), Loader=yaml.BaseLoader)
STEPS = DOCUMENT["jobs"]["qualify"]["steps"]
GUARD = STEPS[0]["run"].split("python3 - <<'PY'\n", 1)[1].rsplit("\nPY", 1)[0]


class WorkflowGuardTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="b6-route-guards-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.environment = {
            "GITHUB_REPOSITORY": "aqua-lifestyle/b6-qualification-evidence",
            "GITHUB_EVENT_NAME": "workflow_dispatch", "GITHUB_REF": "refs/heads/main",
            "GITHUB_SHA": "b" * 40, "SOURCE_COMMIT": "a" * 40, "SOURCE_PR": "123",
            "AUTHORIZATION_ISSUE": "1", "INDEPENDENT_REVIEWER": "independent-reviewer",
            "GITHUB_ACTOR": "executor", "GITHUB_RUN_ID": "100", "GITHUB_RUN_ATTEMPT": "1",
            "GITHUB_ENV": str(self.root / "github-env"), "RUNNER_TEMP": str(self.root),
        }
        baseline = (HERE / "candidate-baseline.json").read_bytes()
        baseline_hash = hashlib.sha256(baseline).hexdigest()
        authority = {
            "schema": "B6PrivilegedQualificationAuthorizationV1", "source_commit": "a" * 40,
            "source_pr": 123, "baseline_sha256": baseline_hash,
            "workflow_sha256": hashlib.sha256(WORKFLOW.read_bytes()).hexdigest(),
            "run_id": "100", "run_attempt": "1", "reviewer": "independent-reviewer",
            "runner": "ubuntu-24.04", "evidence_repository": "aqua-lifestyle/b6-qualification-evidence",
            "scope": "QUALIFICATION_ONLY_NO_PRODUCTION",
        }
        private = "repos/aqua-lifestyle/b6-qualification-evidence"
        source = "repos/aqua-lifestyle/aqua-lifestyle"
        self.private, self.source = private, source
        self.responses = {
            private: {"visibility": "private"},
            private + "/environments/b6-qualification": {
                "can_admins_bypass": False,
                "protection_rules": [{"type": "required_reviewers", "prevent_self_review": True,
                                      "reviewers": [{"type": "User", "reviewer": {"login": "independent-reviewer"}}]}],
            },
            source + "/pulls/123": {"draft": False, "user": {"login": "implementation-author"},
                                    "head": {"sha": "a" * 40, "repo": {"full_name": "aqua-lifestyle/aqua-lifestyle"}}},
            source + "/pulls/123/reviews?per_page=100&page=1": [
                {"id": 1, "user": {"login": "independent-reviewer"}, "state": "APPROVED", "commit_id": "a" * 40,
                 "body": "B6_SUPPLY_BASELINE_APPROVED sha256:" + baseline_hash}],
            source + "/contents/docs/operations/aqgreen-v2-b6/snapshot-package/replacement-supply/candidate-baseline.json?ref=" + "a" * 40:
                {"content": base64.b64encode(baseline).decode()},
            source + "/contents/.github/workflows/b6-qualification.yml?ref=" + "a" * 40:
                {"content": base64.b64encode(WORKFLOW.read_bytes()).decode()},
            private + "/contents/.github/workflows/b6-qualification.yml?ref=" + "b" * 40:
                {"content": base64.b64encode(WORKFLOW.read_bytes()).decode()},
            private + "/issues/1": {"user": {"login": "Xolani-M"}, "state": "open", "body": json.dumps(authority)},
        }

    def execute(self):
        def response(command):
            self.assertEqual(command[:2], ["gh", "api"])
            return json.dumps(self.responses[command[2]]).encode()
        with mock.patch.dict(os.environ, self.environment, clear=True), mock.patch("subprocess.check_output", side_effect=response):
            exec(compile(GUARD, str(WORKFLOW), "exec"), {})

    def test_exact_independent_review_and_owner_authority_pass_preflight_only(self):
        self.execute()
        created = list(self.root.glob("b6-qualification-*"))
        self.assertEqual(len(created), 1)
        self.assertEqual(created[0].stat().st_mode & 0o777, 0o700)
        self.assertTrue((created[0] / "authorization.json").is_file())

    def test_each_missing_or_substituted_authority_fails_at_its_boundary(self):
        cases = [
            ("unappointed", "owner-designated independent reviewer required"),
            ("public", "private custody repository required"),
            ("trigger", "manual default-branch execution required"),
            ("actor", "reviewer cannot execute the qualification"),
            ("visibility", "custody must remain private"),
            ("admin-bypass", "environment bypass must be disabled"),
            ("no-environment-review", "independent environment protection required"),
            ("self-review", "independent environment protection required"),
            ("wrong-environment-reviewer", "designated reviewer must protect execution"),
            ("source-author", "reviewer cannot author the source PR"),
            ("source-fork", "reviewed source PR identity mismatch"),
            ("stale-source", "reviewed source PR identity mismatch"),
            ("no-review", "independent supply review missing"),
            ("stale-review", "current exact-commit supply approval missing"),
            ("wrong-baseline-review", "current exact-commit supply approval missing"),
            ("dismissed-review", "current exact-commit supply approval missing"),
            ("workflow-substitution", "executing workflow differs from independently reviewed source"),
            ("not-owner", "owner authorization issue required"),
            ("wrong-run", "owner authorization must bind this exact run, source and supply"),
            ("wrong-attempt", "owner authorization must bind this exact run, source and supply"),
            ("wrong-baseline", "owner authorization must bind this exact run, source and supply"),
            ("wrong-scope", "owner authorization must bind this exact run, source and supply"),
        ]
        original_responses, original_env = copy.deepcopy(self.responses), dict(self.environment)
        for case, reason in cases:
            with self.subTest(case=case):
                self.responses, self.environment = copy.deepcopy(original_responses), dict(original_env)
                protection = self.responses[self.private + "/environments/b6-qualification"]
                pr = self.responses[self.source + "/pulls/123"]
                review = self.responses[self.source + "/pulls/123/reviews?per_page=100&page=1"]
                issue = self.responses[self.private + "/issues/1"]
                authority = json.loads(issue["body"])
                if case == "unappointed": self.environment["INDEPENDENT_REVIEWER"] = ""
                elif case == "public": self.environment["GITHUB_REPOSITORY"] = "aqua-lifestyle/aqua-lifestyle"
                elif case == "trigger": self.environment["GITHUB_EVENT_NAME"] = "push"
                elif case == "actor": self.environment["GITHUB_ACTOR"] = "independent-reviewer"
                elif case == "visibility": self.responses[self.private]["visibility"] = "public"
                elif case == "admin-bypass": protection["can_admins_bypass"] = True
                elif case == "no-environment-review": protection["protection_rules"] = []
                elif case == "self-review": protection["protection_rules"][0]["prevent_self_review"] = False
                elif case == "wrong-environment-reviewer": protection["protection_rules"][0]["reviewers"] = []
                elif case == "source-author": pr["user"]["login"] = "independent-reviewer"
                elif case == "source-fork": pr["head"]["repo"]["full_name"] = "untrusted/fork"
                elif case == "stale-source": pr["head"]["sha"] = "c" * 40
                elif case == "no-review": review.clear()
                elif case == "stale-review": review[0]["commit_id"] = "c" * 40
                elif case == "wrong-baseline-review": review[0]["body"] = "APPROVED"
                elif case == "dismissed-review": review.append(dict(review[0], id=2, state="DISMISSED"))
                elif case == "workflow-substitution":
                    self.responses[self.private + "/contents/.github/workflows/b6-qualification.yml?ref=" + "b" * 40]["content"] = base64.b64encode(b"different").decode()
                elif case == "not-owner": issue["user"]["login"] = "implementation-author"
                elif case == "wrong-run": authority["run_id"] = "99"
                elif case == "wrong-attempt": authority["run_attempt"] = "2"
                elif case == "wrong-baseline": authority["baseline_sha256"] = "0" * 64
                elif case == "wrong-scope": authority["scope"] = "EXTRACT"
                issue["body"] = json.dumps(authority)
                with self.assertRaisesRegex(RuntimeError, reason):
                    self.execute()
                self.assertEqual(list(self.root.glob("b6-qualification-*")), [])

    def test_workflow_is_manual_private_and_shell_syntax_is_valid(self):
        self.assertEqual(list(DOCUMENT["on"]), ["workflow_dispatch"])
        self.assertEqual(DOCUMENT["concurrency"]["cancel-in-progress"], "false")
        self.assertIn("github.repository == 'aqua-lifestyle/b6-qualification-evidence'", DOCUMENT["jobs"]["qualify"]["if"])
        self.assertNotIn("upload-artifact", WORKFLOW.read_text().replace("# No upload-artifact,", ""))
        for step in STEPS:
            result = subprocess.run(["bash", "-n"], input=step["run"], text=True, capture_output=True)
            self.assertEqual(result.returncode, 0, step["name"] + ": " + result.stderr)


class CustodyModeTests(unittest.TestCase):
    def test_cli_homes_keys_scratch_and_unselected_installed_caches_are_not_exports(self):
        spec = importlib.util.spec_from_file_location("verification", HERE / "verify-inputs.py")
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        for path in ("supply/native-home/.dotnet/corefx/cryptography/x509stores/my/dev.pfx",
                     "supply/native-home", "supply/native-packages/abp/9.4.1/abp.nuspec",
                     "supply/restore-artifacts/project.assets.json", "host-tools/bubblewrap/extracted/usr/bin/bwrap",
                     "supply/frozen-dotnet-evil/payload"):
            self.assertFalse(module.allowed_member(path), path)
        for path in ("supply/frozen-dotnet/dotnet", "supply/feed/abp.9.4.1.nupkg",
                     "supply/native-logs/abp.9.4.1.verify.log", "supply/native-packages-tree.tsv",
                     "host-tools/bubblewrap.deb", "host-tools/mawk/signature.log"):
            self.assertTrue(module.allowed_member(path), path)

    def test_authenticated_readonly_modes_survive_safe_extraction(self):
        spec = importlib.util.spec_from_file_location("verification", HERE / "verify-inputs.py")
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        with tempfile.TemporaryDirectory(prefix="b6-custody-test-") as directory:
            data = io.BytesIO()
            with tarfile.open(fileobj=data, mode="w") as archive:
                parent = tarfile.TarInfo("frozen")
                parent.type, parent.mode = tarfile.DIRTYPE, 0o555
                archive.addfile(parent)
                file = tarfile.TarInfo("frozen/payload")
                file.mode, file.size = 0o444, 3
                archive.addfile(file, io.BytesIO(b"SDK"))
            data.seek(0)
            with tarfile.open(fileobj=data) as archive:
                archive.extractall(directory, filter=module.custody_filter)
            root = Path(directory) / "frozen"
            self.assertEqual(root.stat().st_mode & 0o777, 0o555)
            self.assertEqual((root / "payload").stat().st_mode & 0o777, 0o444)
            self.assertEqual((root / "payload").read_bytes(), b"SDK")
            root.chmod(0o700)

    def test_path_link_and_special_mode_substitution_rejected(self):
        spec = importlib.util.spec_from_file_location("verification", HERE / "verify-inputs.py")
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        with tempfile.TemporaryDirectory(prefix="b6-custody-test-") as directory:
            for path in ("../escape", "/tmp/escape"):
                member = tarfile.TarInfo(path)
                if path.startswith("/"):
                    # data_filter safely strips a leading slash rather than allowing
                    # an absolute destination; it must stay in custody.
                    self.assertFalse(module.custody_filter(member, directory).name.startswith("/"))
                else:
                    with self.assertRaises(tarfile.FilterError):
                        module.custody_filter(member, directory)
            member = tarfile.TarInfo("outside-link")
            member.type, member.linkname = tarfile.SYMTYPE, "/etc/passwd"
            with self.assertRaises(tarfile.FilterError):
                module.custody_filter(member, directory)
            member = tarfile.TarInfo("setuid")
            member.mode = 0o4755
            with self.assertRaisesRegex(RuntimeError, "special archive modes forbidden"):
                module.custody_filter(member, directory)


if __name__ == "__main__":
    unittest.main(verbosity=2)
