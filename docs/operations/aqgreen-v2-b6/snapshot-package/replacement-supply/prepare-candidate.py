#!/usr/bin/env python3
"""Acquire replacement candidates, never approve or operationally qualify them.

No production inputs, ambient NuGet caches, dependency resolution refresh, or sudo.
All native output is retained in the private output directory, not printed.
"""

import argparse
import base64
import concurrent.futures
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import stat
import subprocess
import tarfile
import urllib.parse
import urllib.request
import zipfile


PACKAGE = Path(__file__).resolve().parent.parent
REPOSITORY = PACKAGE.parents[3]
BACKEND = REPOSITORY / "AqualLifeStyle/9.4.2/aspnet-core"
METADATA = "https://builds.dotnet.microsoft.com/dotnet/release-metadata/8.0/releases.json"
SERVICE = "https://api.nuget.org/v3/index.json"
SDK_SHA512 = "4f3ed7011a924fbe9d093cab087deff5ed9b19fc0de0222117fda7c1334605c31818b01ffbd445b69e59f294fb4d3383fd00fd19701e7142acb7bc88b9b4ecab"
SIGNERS = {
    "0E5F38F57DC1BCC806D8494F4F90FBCEDD988B46760709CBEEC6F4219AA6157D",
    "1F4B311D9ACC115C8DC8018B5A49E00FCE6DA8E2855F9F014CA6F34570BC482D",
    "5A2901D6ADA3D18260B9C6DFE2133C95D74B9EEF6AE0E5DC334C8454D1477DF4",
}
LOCKS = {
    "src/AqualLifeStyle.Core/packages.lock.json": "9f467700282dc48f8bdc172eb8f1830a3167cbc4efaa223a328b1ff00c4acddc",
    "src/AqualLifeStyle.EntityFrameworkCore/packages.lock.json": "2b1925d618a39d10fa57e3e8455f06c3565de75ccdc3ae4a9021e357c9d07356",
    "tools/AqualLifeStyle.B6SnapshotRunner/packages.lock.json": "401fafbcfa35347666a4de3fcfa92be9c66399663d8f8410eca420879c3d7b7b",
    "tools/AqualLifeStyle.B6SnapshotRunner.Tests/packages.lock.json": "e575bab05aa361751f35ce31340880c0286e6133c7f36b7e3e1fb2ff8bbf0a47",
}


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def digest(path, algorithm="sha256"):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, algorithm).hexdigest()


def download(url, destination):
    require(url.startswith("https://"), "HTTPS source required")
    with urllib.request.urlopen(url, timeout=120) as response:
        require(response.geturl().startswith("https://"), "HTTPS redirect required")
        with destination.open("xb") as output:
            while block := response.read(1024 * 1024):
                output.write(block)


def fetch_json(url, path):
    download(url, path)
    return json.loads(path.read_text())


def inventory(root, output):
    rows = ["type\tmode\tsize\tsha256\tlink_target\tpath\n"]
    for path in sorted(root.rglob("*"), key=lambda p: p.relative_to(root).as_posix()):
        relative = path.relative_to(root).as_posix()
        require(not any(c in relative for c in "\t\r\n"), "unrepresentable path")
        mode = format(stat.S_IMODE(path.lstat().st_mode), "o")
        if path.is_symlink():
            target = os.readlink(path)
            require(path.resolve().is_relative_to(root.resolve()), "escaping toolchain link")
            require(not any(c in target for c in "\t\r\n"), "unrepresentable link")
            row = ["L", mode, "-", hashlib.sha256(target.encode()).hexdigest(), target, relative]
        elif path.is_dir():
            row = ["D", mode, "-", "-", "-", relative]
        else:
            require(path.is_file(), "unsafe toolchain entry")
            row = ["F", mode, str(path.stat().st_size), digest(path), "-", relative]
        rows.append("\t".join(row) + "\n")
    output.write_text("".join(rows))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--download-cache", type=Path,
                        help="Previously downloaded candidate archives only; all verification is repeated")
    args = parser.parse_args()
    os.umask(0o077)
    require(os.geteuid() != 0, "candidate preparation must be non-privileged")
    output = args.output.absolute()
    require(not output.exists() and not output.is_symlink(), "output must be new")
    require(output.parent.is_dir() and not output.parent.is_symlink(), "trusted parent required")
    require(output.parent.stat().st_uid == os.geteuid() and stat.S_IMODE(output.parent.stat().st_mode) == 0o700,
            "parent must be operator-owned 0700")
    output.mkdir(mode=0o700)
    def acquire_archive(url, destination):
        cached = args.download_cache / destination.relative_to(output) if args.download_cache else None
        if cached is not None and cached.is_file() and not cached.is_symlink():
            shutil.copyfile(cached, destination)
        else:
            download(url, destination)
    provenance = output / "provenance"
    provenance.mkdir()
    feed = output / "feed"
    feed.mkdir()
    frozen = output / "frozen-dotnet"
    frozen.mkdir()
    coordinates = [tuple(row.split("\t")) for row in (PACKAGE / "authority/U215.tsv").read_text().splitlines()]
    require(len(coordinates) == 215 and coordinates == sorted(set(coordinates)), "exact U215 required")
    require(digest(PACKAGE / "authority/U215.tsv") == "372c918aa794e976601ee292caa9c56a884784ebcfe04cd53981f76d3e982dd1", "U215 authority drift")
    lock_content = {}
    for relative, expected in LOCKS.items():
        path = BACKEND / relative
        require(digest(path) == expected, "dependency lock drift")
        for graph in json.loads(path.read_text())["dependencies"].values():
            for name, item in graph.items():
                if item["type"] == "Project":
                    continue
                key = (name.lower(), item["resolved"])
                require(key not in lock_content or lock_content[key] == item["contentHash"], "conflicting lock hash")
                lock_content[key] = item["contentHash"]
    require(set(lock_content) == set(coordinates), "lock union must be exact U215")

    metadata = fetch_json(METADATA, provenance / "dotnet-release-metadata.json")
    sdks = [sdk for release in metadata["releases"] for sdk in release.get("sdks", []) if sdk["version"] == "8.0.130"]
    require(len(sdks) == 1 and sdks[0]["runtime-version"] == "8.0.30", "SDK/runtime publisher metadata mismatch")
    files = [f for f in sdks[0]["files"] if f["rid"] == "linux-x64" and f["name"].endswith(".tar.gz")]
    require(len(files) == 1 and files[0]["hash"] == SDK_SHA512, "publisher SDK checksum changed")
    sdk_url = files[0]["url"]
    require(sdk_url == "https://builds.dotnet.microsoft.com/dotnet/Sdk/8.0.130/dotnet-sdk-8.0.130-linux-x64.tar.gz", "unexpected SDK publisher")
    archive = output / "dotnet-sdk-8.0.130-linux-x64.tar.gz"
    acquire_archive(sdk_url, archive)
    require(digest(archive, "sha512") == SDK_SHA512, "SDK publisher SHA512 mismatch")
    with tarfile.open(archive) as bundle:
        bundle.extractall(frozen, filter="data")

    index = fetch_json(SERVICE, provenance / "nuget-service-index.json")
    signature_url = next(r["@id"] for r in index["resources"] if r["@type"] == "RepositorySignatures/5.0.0")
    signatures = fetch_json(signature_url, provenance / "nuget-repository-signatures.json")
    require(signatures["allRepositorySigned"] is True, "repository signature requirement absent")
    require({c["fingerprints"]["2.16.840.1.101.3.4.2.1"].upper() for c in signatures["signingCertificates"]} == SIGNERS,
            "repository signer rotation requires separate approval")
    base = next(r["@id"] for r in index["resources"] if r["@type"] == "PackageBaseAddress/3.0.0")
    require(base == "https://api.nuget.org/v3-flatcontainer/", "unexpected package source")

    def acquire(coordinate):
        name, version = coordinate
        require(re.fullmatch(r"[a-z0-9_.-]+", name) and re.fullmatch(r"[0-9A-Za-z.+-]+", version), "invalid coordinate")
        url = f"{base}{name}/{version}/{name}.{version}.nupkg"
        destination = feed / f"{name}.{version}.nupkg"
        acquire_archive(url, destination)
        return {"id": name, "version": version, "url": url, "raw_sha256": digest(destination)}

    with concurrent.futures.ThreadPoolExecutor(max_workers=6) as workers:
        sources = list(workers.map(acquire, coordinates))
    (provenance / "package-downloads.json").write_text(json.dumps(sources, indent=2) + "\n")
    dotnet = frozen / "dotnet"
    home = output / "native-home"
    home.mkdir()
    packages = output / "native-packages"
    logs = output / "native-logs"
    logs.mkdir()
    environment = {
        "PATH": f"{frozen}:/usr/bin:/bin", "HOME": str(home), "TMPDIR": str(home),
        "DOTNET_ROOT": str(frozen), "DOTNET_ROOT_X64": str(frozen), "DOTNET_CLI_HOME": str(home),
        "DOTNET_CLI_TELEMETRY_OPTOUT": "1", "DOTNET_SKIP_FIRST_TIME_EXPERIENCE": "1",
        "DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE": "true", "DOTNET_NOLOGO": "1",
        "DOTNET_GENERATE_ASPNET_CERTIFICATE": "false",
        "DOTNET_NUGET_SIGNATURE_VERIFICATION": "true", "NUGET_CERT_REVOCATION_MODE": "online",
        "NUGET_PACKAGES": str(packages), "NUGET_HTTP_CACHE_PATH": str(home / "http"),
        "LC_ALL": "C", "MSBUILDDISABLENODEREUSE": "1",
    }

    def native(arguments, log):
        with log.open("xb") as stream:
            result = subprocess.run([str(dotnet), *arguments], env=environment, stdout=stream, stderr=subprocess.STDOUT)
        require(result.returncode == 0, "native candidate verification failed; inspect private evidence")
        return log.read_text()

    require(native(["--version"], logs / "sdk-version.log").strip() == "8.0.130", "SDK version mismatch")
    require(native(["nuget", "--version"], logs / "nuget-version.log").strip() == "NuGet Command Line\n6.8.2.3", "NuGet version mismatch")
    runtimes = native(["--list-runtimes"], logs / "runtime-versions.log")
    require(runtimes == f"Microsoft.AspNetCore.App 8.0.30 [{frozen}/shared/Microsoft.AspNetCore.App]\nMicrosoft.NETCore.App 8.0.30 [{frozen}/shared/Microsoft.NETCore.App]\n", "runtime version mismatch")
    policy = PACKAGE / "qualification-nuget.config"
    native(["restore", str(BACKEND / "tools/AqualLifeStyle.B6SnapshotRunner.Tests/AqualLifeStyle.B6SnapshotRunner.Tests.csproj"),
            "--configfile", str(policy), "--packages", str(packages), "--locked-mode", "--source", str(feed),
            "--disable-build-servers", f"-p:ArtifactsPath={output / 'restore-artifacts'}",
            "-p:ImportDirectoryBuildProps=false", "-p:ImportDirectoryBuildTargets=false"], logs / "locked-restore.log")

    def verify(coordinate):
        name, version = coordinate
        archive = feed / f"{name}.{version}.nupkg"
        log = logs / f"{name}.{version}.verify.log"
        text = native(["nuget", "verify", "--all", "--configfile", str(policy), "--verbosity", "detailed", str(archive)], log)
        require(text.lower().splitlines().count(f"successfully verified package '{name}.{version}'.") == 1, "missing native success")
        require(f"Service index: {SERVICE}" in text.splitlines(), "missing signed source provenance")
        match = re.search(r"^Signature type: Repository\n(?:(?!^Signature type:).)*?^  SHA256 hash: ([A-F0-9]+)$", text, re.M | re.S)
        require(match is not None and match[1] in SIGNERS, "unapproved repository signer")
        installed = packages / name / version
        require(digest(installed / archive.name) == digest(archive), "native restored archive differs")
        content = json.loads((installed / ".nupkg.metadata").read_text())["contentHash"]
        require(content == lock_content[coordinate], "lock content identity differs")
        raw512 = base64.b64encode(bytes.fromhex(digest(archive, "sha512"))).decode()
        require((installed / (archive.name + ".sha512")).read_text().strip() == raw512,
                "native raw archive hash differs")
        with zipfile.ZipFile(archive) as z:
            entries = [e for e in z.namelist() if e.lower() == name + ".nuspec"]
            require(len(entries) == 1, "ambiguous nuspec")
            archive_nuspec = hashlib.sha256(z.read(entries[0])).hexdigest()
            expected_files = {archive.name, archive.name + ".sha512", ".nupkg.metadata"}
            for entry in z.infolist():
                if entry.is_dir() or entry.filename.startswith(("_rels/", "package/services/metadata/core-properties/")) or entry.filename == "[Content_Types].xml":
                    continue
                relative = urllib.parse.unquote(entry.filename)
                require(not relative.startswith("/") and "\\" not in relative and ".." not in Path(relative).parts, "unsafe package path")
                if relative.lower() == name + ".nuspec":
                    relative = name + ".nuspec"
                require(relative not in expected_files, "normalized package path collision")
                expected_files.add(relative)
                path = installed / relative
                require(path.is_file() and not path.is_symlink(), "missing installed package file")
                if relative != name + ".nuspec":
                    require(hashlib.sha256(z.read(entry)).hexdigest() == digest(path), "installed content differs")
            require({p.relative_to(installed).as_posix() for p in installed.rglob("*") if p.is_file()} == expected_files, "installed package inventory differs")
        return [name, version, archive.name, digest(archive), raw512, content, archive_nuspec, digest(installed / (name + ".nuspec")), SERVICE]

    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as workers:
        rows = list(workers.map(verify, coordinates))
    header = "id\tversion\tarchive\traw_sha256\traw_sha512_base64\tcontent_hash\tarchive_nuspec_sha256\tinstalled_nuspec_sha256\tsource\n"
    (output / "feed-manifest.tsv").write_text(header + "".join("\t".join(row) + "\n" for row in rows))
    actual_coordinates = {(p.parent.name, p.name) for p in packages.glob("*/*") if p.is_dir()}
    require(actual_coordinates == set(coordinates), "native package cache must be exact U215")
    for relative, expected in LOCKS.items():
        require(digest(BACKEND / relative) == expected, "native restore mutated lock")
    for path in frozen.rglob("*"):
        if not path.is_symlink():
            path.chmod(stat.S_IMODE(path.stat().st_mode) & ~0o222)
    frozen.chmod(0o555)
    inventory(frozen, output / "toolchain-manifest.tsv")
    result = {
        "schema": "B6ReplacementSupplyCandidateV1", "status": "CANDIDATE_NOT_APPROVED",
        "sdk_version": "8.0.130", "runtime_version": "8.0.30", "nuget_version": "6.8.2.3",
        "sdk_url": sdk_url, "sdk_publisher_sha512": SDK_SHA512,
        "dotnet_host_sha256": digest(dotnet), "toolchain_manifest_sha256": digest(output / "toolchain-manifest.tsv"),
        "feed_manifest_sha256": digest(output / "feed-manifest.tsv"), "package_count": len(rows),
        "lock_sha256": LOCKS, "native_trust_verified_count": len(rows),
        "classification": "NON_PRIVILEGED_CANDIDATE_SUPPLY_VERIFICATION_NOT_GATE_Q_A",
    }
    (output / "candidate.json").write_text(json.dumps(result, indent=2) + "\n")
    print("Candidate acquisition and native verification completed: 215 packages; independent approval required.")


if __name__ == "__main__":
    main()
