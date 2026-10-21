#!/usr/bin/env python3
"""Route regression entry point; no workflow dispatch or privileged execution."""

import importlib.util
import io
from pathlib import Path
import tarfile
import tempfile
import unittest

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("handoff_tests", HERE / "test-handoff.py")
handoff = importlib.util.module_from_spec(spec)
spec.loader.exec_module(handoff)
# Existing source/visibility/review/owner denials moved to the actual verifier's
# component tests; they are retained, not replaced with weaker YAML string checks.
WorkflowGuardTests = handoff.WorkflowGuardTests
HandoffBindingTests = handoff.HandoffBindingTests


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
