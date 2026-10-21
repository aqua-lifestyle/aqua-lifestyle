#!/usr/bin/env python3
"""Verify pinned replacement candidates without executing them or approving them."""

import argparse
import csv
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import stat
import sys
import tarfile

sys.dont_write_bytecode = True


def sha(path, algorithm="sha256"):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, algorithm).hexdigest()


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def custody_filter(member, destination):
    # data_filter enforces link/path/type safety, but by default adds owner write
    # and discards directory modes. Preserve modes from the already hash-verified
    # custody archive so a read-only toolchain survives transfer exactly.
    filtered = tarfile.data_filter(member, destination)
    require(not member.mode & 0o7000, "special archive modes forbidden")
    return filtered.replace(mode=member.mode) if filtered is not None else None


def verify(baseline, root, repository):
    supply = root / "supply"
    require(sha(supply / "toolchain-manifest.tsv") == baseline["toolchain_manifest_sha256"], "toolchain manifest mismatch")
    require(sha(supply / "feed-manifest.tsv") == baseline["feed_manifest_sha256"], "feed manifest mismatch")
    require(sha(supply / "dotnet-sdk-8.0.130-linux-x64.tar.gz", "sha512") == baseline["sdk_publisher_sha512"], "publisher SDK archive mismatch")
    module_spec = importlib.util.spec_from_file_location("prepare", Path(__file__).with_name("prepare-candidate.py"))
    helper = importlib.util.module_from_spec(module_spec)
    module_spec.loader.exec_module(helper)
    helper.inventory(supply / "frozen-dotnet", root / "actual-toolchain.tsv")
    require((root / "actual-toolchain.tsv").read_bytes() == (supply / "toolchain-manifest.tsv").read_bytes(), "toolchain inventory mismatch")
    require(stat.S_IMODE((supply / "frozen-dotnet").stat().st_mode) == 0o555, "frozen root mode mismatch")
    require(sha(supply / "frozen-dotnet/dotnet") == baseline["dotnet_host_sha256"], "dotnet executable mismatch")
    with (supply / "feed-manifest.tsv").open() as stream:
        rows = list(csv.DictReader(stream, delimiter="\t"))
    package = repository / "docs/operations/aqgreen-v2-b6/snapshot-package"
    expected = (package / "authority/U215.tsv").read_text().splitlines()
    require([f'{r["id"]}\t{r["version"]}' for r in rows] == expected and len(rows) == 215, "exact U215 mismatch")
    require({p.name for p in (supply / "feed").iterdir()} == {r["archive"] for r in rows}, "feed inventory mismatch")
    for row in rows:
        require(row["archive"] == f'{row["id"]}.{row["version"]}.nupkg', "invalid feed archive")
        archive = supply / "feed" / row["archive"]
        require(archive.is_file() and not archive.is_symlink() and sha(archive) == row["raw_sha256"], "feed archive mismatch")
    for relative, expected_hash in baseline["lock_sha256"].items():
        require(sha(repository / "AqualLifeStyle/9.4.2/aspnet-core" / relative) == expected_hash, "lock identity mismatch")
    require(sha(package / "authoritative-runner.sh") == baseline["bootstrap_sha256"], "bootstrap identity mismatch")
    require(sha(root / "host-tools/host-tools.json") == baseline["host_tools_manifest_sha256"], "host manifest mismatch")
    for tool in baseline["host_tools"]:
        require(sha(root / "host-tools" / (tool["package"] + ".deb")) == tool["archive_sha256"], "host package mismatch")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--archive", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--repository", type=Path, required=True)
    args = parser.parse_args()
    os.umask(0o077)
    baseline_path = Path(__file__).with_name("candidate-baseline.json")
    baseline = json.loads(baseline_path.read_text())
    require(sha(args.archive) == baseline["archive_sha256"], "replacement archive mismatch")
    require(not args.output.exists() and not args.output.is_symlink(), "new output required")
    require(args.output.parent.is_dir() and args.output.parent.stat().st_uid == os.geteuid()
            and stat.S_IMODE(args.output.parent.stat().st_mode) == 0o700, "private owned parent required")
    args.output.mkdir(mode=0o700)
    with tarfile.open(args.archive) as archive:
        members = archive.getmembers()
        require(len({m.name for m in members}) == len(members), "duplicate archive entries")
        archive.extractall(args.output, members=members, filter=custody_filter)
    verify(baseline, args.output, args.repository)
    print("Replacement candidate archive, complete toolchain, 215 feed archives, locks and host packages verified; not approval.")


if __name__ == "__main__":
    main()
