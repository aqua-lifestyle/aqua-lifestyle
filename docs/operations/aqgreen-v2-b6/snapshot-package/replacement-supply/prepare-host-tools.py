#!/usr/bin/env python3
"""Non-privileged candidate acquisition of signed host-tool packages; no install."""

import argparse
import hashlib
import json
import lzma
import os
from pathlib import Path
import re
import subprocess
import sys
import urllib.request

import importlib.util

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("candidate", Path(__file__).with_name("prepare-candidate.py"))
helpers = importlib.util.module_from_spec(spec)
spec.loader.exec_module(helpers)
require, digest, download = helpers.require, helpers.digest, helpers.download


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    output = parser.parse_args().output.absolute()
    os.umask(0o077)
    require(os.geteuid() != 0 and not output.exists(), "new non-privileged output required")
    require(output.parent.is_dir() and output.parent.stat().st_uid == os.geteuid()
            and output.parent.stat().st_mode & 0o777 == 0o700, "private owned parent required")
    output.mkdir(mode=0o700)
    archive_keyring = Path("/usr/share/keyrings/ubuntu-archive-keyring.gpg")
    fingerprints = subprocess.check_output(["gpg", "--batch", "--show-keys", "--with-colons", str(archive_keyring)], stderr=subprocess.DEVNULL).decode()
    require("F6ECB3762474EDA9D21B7022871920D1991BC93C" in fingerprints, "Ubuntu archive publisher key absent")
    launchpad = helpers.fetch_json("https://api.launchpad.net/1.0/~flatpak/+archive/ubuntu/stable", output / "flatpak-ppa.json")
    fingerprint = launchpad["signing_key_fingerprint"]
    require(re.fullmatch("[A-F0-9]{40}", fingerprint), "invalid PPA signing fingerprint")
    key = output / "flatpak-ppa-key.asc"
    download("https://keyserver.ubuntu.com/pks/lookup?op=get&search=0x" + fingerprint, key)
    actual = subprocess.check_output(["gpg", "--batch", "--show-keys", "--with-colons", str(key)], stderr=subprocess.DEVNULL).decode()
    require(f"fpr:::::::::{fingerprint}:" in actual, "PPA key fingerprint mismatch")
    keyring = output / "flatpak-ppa-key.gpg"
    subprocess.run(["gpg", "--batch", "--yes", "--dearmor", "--output", str(keyring), str(key)], check=True, stderr=subprocess.DEVNULL)
    selections = [
        ("mawk", "1.3.4.20240123-1build1", "https://archive.ubuntu.com/ubuntu", "noble", archive_keyring, "/usr/bin/mawk"),
        ("busybox-static", "1:1.36.1-6ubuntu3.1", "https://archive.ubuntu.com/ubuntu", "noble-updates", archive_keyring, "/usr/bin/busybox"),
        ("bubblewrap", "0.12.0-1~flatpak1~24.04.1", "https://ppa.launchpadcontent.net/flatpak/stable/ubuntu", "noble", keyring, "/usr/bin/bwrap"),
    ]
    results = []
    for name, version, base, suite, trust, executable in selections:
        prefix = output / name
        prefix.mkdir()
        release = prefix / "InRelease"
        download(f"{base}/dists/{suite}/InRelease", release)
        with (prefix / "signature.log").open("wb") as log:
            verified = subprocess.run(["gpgv", "--status-fd", "1", "--keyring", str(trust), str(release)], stdout=log, stderr=subprocess.STDOUT)
        require(verified.returncode == 0, "host package repository signature failed")
        expected_publisher = fingerprint if name == "bubblewrap" else "F6ECB3762474EDA9D21B7022871920D1991BC93C"
        verified_keys = re.findall(r"^\[GNUPG:\] VALIDSIG ([A-F0-9]+) ", (prefix / "signature.log").read_text(), re.M)
        require(verified_keys == [expected_publisher], "host package publisher identity mismatch")
        index_name = "main/binary-amd64/Packages.xz"
        text = release.read_text()
        section = text.split("\nSHA256:\n", 1)[1].split("\nSHA512:", 1)[0]
        rows = [line.split() for line in section.splitlines()]
        expected = [row for row in rows if len(row) == 3 and row[2] == index_name]
        require(len(expected) == 1, "signed package-index identity missing")
        index = prefix / "Packages.xz"
        download(f"{base}/dists/{suite}/{index_name}", index)
        require(digest(index) == expected[0][0] and index.stat().st_size == int(expected[0][1]), "package-index identity mismatch")
        paragraphs = lzma.decompress(index.read_bytes()).decode().split("\n\n")
        packages = []
        for paragraph in paragraphs:
            fields = dict(line.split(": ", 1) for line in paragraph.splitlines() if ": " in line and not line.startswith(" "))
            if fields.get("Package") == name and fields.get("Version") == version and fields.get("Architecture") == "amd64":
                packages.append(fields)
        require(len(packages) == 1, "exact host package unavailable from signed index")
        package = packages[0]
        filename = package["Filename"]
        require(filename.startswith("pool/") and ".." not in filename.split("/"), "unsafe host archive location")
        deb = output / (name + ".deb")
        download(f"{base}/{filename}", deb)
        require(digest(deb) == package["SHA256"] and deb.stat().st_size == int(package["Size"]), "host archive identity mismatch")
        extracted = prefix / "extracted"
        subprocess.run(["dpkg-deb", "--extract", str(deb), str(extracted)], check=True, stdout=subprocess.DEVNULL)
        binary = extracted / executable.lstrip("/")
        if not binary.exists() and executable == "/usr/bin/busybox":
            binary = extracted / "bin/busybox"
        require(binary.is_file() and not binary.is_symlink(), "host executable absent")
        results.append({"package": name, "version": version, "url": f"{base}/{filename}",
                        "archive_sha256": digest(deb), "executable": executable,
                        "executable_sha256": digest(binary), "signed_index_sha256": digest(index),
                        "publisher": "flatpak/stable Launchpad PPA" if name == "bubblewrap" else "Ubuntu archive",
                        "publisher_key": fingerprint if name == "bubblewrap" else "F6ECB3762474EDA9D21B7022871920D1991BC93C"})
    (output / "host-tools.json").write_text(json.dumps({"status": "CANDIDATE_NOT_APPROVED", "packages": results}, indent=2) + "\n")
    print("Three host-tool candidates acquired and verified against signed repository metadata; none installed.")


if __name__ == "__main__":
    main()
