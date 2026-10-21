#!/usr/bin/env python3
"""Export only allowlisted candidate material, never CLI homes or private keys."""

import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import stat
import sys
import tarfile

sys.dont_write_bytecode = True

SUPPLY_ENTRIES = (
    "dotnet-sdk-8.0.130-linux-x64.tar.gz", "frozen-dotnet", "feed",
    "toolchain-manifest.tsv", "feed-manifest.tsv", "candidate.json", "provenance", "native-logs",
)
HOST_ENTRIES = (
    "host-tools.json", "mawk.deb", "busybox-static.deb", "bubblewrap.deb",
    "flatpak-ppa.json", "flatpak-ppa-key.asc", "flatpak-ppa-key.gpg",
    *(f"{package}/{name}" for package in ("mawk", "busybox-static", "bubblewrap")
      for name in ("InRelease", "Packages.xz", "signature.log")),
)


def sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--supply", type=Path, required=True)
    parser.add_argument("--host-tools", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    os.umask(0o077)
    if os.geteuid() == 0 or args.output.exists() or not args.output.parent.is_dir() or args.output.parent.stat().st_uid != os.geteuid() or stat.S_IMODE(args.output.parent.stat().st_mode) != 0o700:
        raise RuntimeError("new non-privileged output beneath owned0700 parent required")
    args.output.mkdir(mode=0o700)
    spec = importlib.util.spec_from_file_location("candidate", Path(__file__).with_name("prepare-candidate.py"))
    candidate = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(candidate)
    tree = args.output / "native-packages-tree.tsv"
    candidate.inventory(args.supply / "native-packages", tree)
    archive = args.output / "replacement-inputs.tar.gz"
    with tarfile.open(archive, "x:gz") as output:
        for name in SUPPLY_ENTRIES:
            output.add(args.supply / name, arcname="supply/" + name)
        output.add(tree, arcname="supply/native-packages-tree.tsv")
        for name in HOST_ENTRIES:
            output.add(args.host_tools / name, arcname="host-tools/" + name)
    parts = []
    with archive.open("rb") as stream:
        while data := stream.read(64 * 1024 * 1024):
            part = args.output / f"replacement-inputs.tar.gz.part-{len(parts):03d}"
            with part.open("xb") as output:
                output.write(data)
            parts.append({"name": part.name, "sha256": hashlib.sha256(data).hexdigest()})
    result = {"archive_sha256": sha(archive), "archive_parts": len(parts),
              "size_bytes": archive.stat().st_size, "native_packages_tree_sha256": sha(tree), "parts": parts}
    (args.output / "transport.json").write_text(json.dumps(result, indent=2) + "\n")
    (args.output / "replacement-inputs.sha256").write_text(result["archive_sha256"] + "  " + archive.name + "\n")
    print(json.dumps({k: v for k, v in result.items() if k != "parts"}))


if __name__ == "__main__":
    main()
