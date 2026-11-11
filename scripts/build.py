#!/usr/bin/env python3
"""Build and package against assemblies extracted from the official target image."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import uuid
import xml.etree.ElementTree as ET
import zipfile
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parents[1]
IMAGE = "jellyfin/jellyfin@sha256:fea863d0cd9ec445985d9c75fc6ed64e38a1d2766cf0c250a33d553f4fc1b771"
SDK = "mcr.microsoft.com/dotnet/sdk:10.0"
GUID = "e271ab40-03fd-4c83-a042-9a819a628da4"
ABI = "12.4.0.0"
ASSEMBLY = "Jellyfin.Plugin.DynamicHdrStripper.dll"
PROJECT = ROOT / "src/Jellyfin.Plugin.DynamicHdrStripper/Jellyfin.Plugin.DynamicHdrStripper.csproj"
RELEASE_URL = "https://github.com/feanorknd/jellyfin-dynamic-hdr-stripper/releases/download/v"


def run(*command):
    subprocess.run(command, cwd=ROOT, check=True)


def package(version, release):
    dist = ROOT / "dist"
    dist.mkdir(exist_ok=True)
    dll = PROJECT.parent / "bin/Release/net10.0" / ASSEMBLY
    if not dll.is_file():
        raise SystemExit("Release DLL not found; run this script without --package-only")
    filename = f"DynamicHDRStripper_{release}.zip"
    meta = {"guid": GUID, "name": "Dynamic HDR Stripper", "version": version, "targetAbi": ABI,
        "description": "Selective Dolby Vision/HDR10+ removal during HEVC HLS remux without video re-encoding.",
        "status": "Active", "autoUpdate": True, "imagePath": "logo.svg"}
    with zipfile.ZipFile(dist / filename, "w", zipfile.ZIP_DEFLATED) as archive:
        archive.write(dll, ASSEMBLY)
        archive.write(PROJECT.parent / "Configuration/logo.svg", "logo.svg")
        archive.writestr("meta.json", json.dumps(meta, indent=2, ensure_ascii=False) + "\n")
        for name in ("README.md", "LICENSE", "docs/GPL-2.0.txt", "docs/COMPATIBILITY.md", "docs/ARCHITECTURE.md"):
            archive.write(ROOT / name, name)
    with zipfile.ZipFile(dist / filename) as archive:
        assert [n for n in archive.namelist() if n.lower().endswith(".dll")] == [ASSEMBLY]
        assert not any(n.endswith((".cs", ".csproj", ".mkv")) for n in archive.namelist())
    checksum = hashlib.md5((dist / filename).read_bytes()).hexdigest()
    sha256 = hashlib.sha256((dist / filename).read_bytes()).hexdigest()
    (dist / "SHA256SUMS").write_text(f"{sha256}  {filename}\n")
    manifest = [{"guid": GUID, "name": "Dynamic HDR Stripper", "owner": "feanorknd", "category": "General",
        "overview": "Work around HDR detection problems by removing Dolby Vision during HLS remux.",
        "description": "For Jellyfin Server 12.4.0.0. Selective DV/HDR10+ removal without video re-encoding, per-client regex overrides, Dry Run, and a localized configuration menu.",
        "imageUrl": "https://raw.githubusercontent.com/feanorknd/jellyfin-dynamic-hdr-stripper/main/src/Jellyfin.Plugin.DynamicHdrStripper/Configuration/logo.svg",
        "versions": [{"version": version, "targetAbi": ABI,
            "changelog": "Add crossed-out DV logo, ordered per-client regex configurations with DV-only or DV+HDR10+ overrides, English/Spanish language detection, and clearer advanced settings. Remove included/excluded client lists; recreate selections using specific configurations.",
            "timestamp": datetime.now(timezone.utc).isoformat(),
            "sourceUrl": RELEASE_URL + release + "/" + filename, "checksum": checksum}]}]
    if (ROOT / "manifest.json").exists():
        previous = json.loads((ROOT / "manifest.json").read_text())[0]["versions"]
        manifest[0]["versions"].extend(v for v in previous if v["version"] != version)
    # Generated separately: publishing manifest.json is an explicit maintainer operation.
    (dist / "manifest.json").write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n")
    print(f"Generated: dist/{filename}, dist/SHA256SUMS and dist/manifest.json")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package-only", action="store_true", help="Package an existing Release DLL")
    args = parser.parse_args()
    version = ET.parse(PROJECT).findtext(".//Version")
    if version is None or not all(part.isdecimal() for part in version.split(".")):
        raise SystemExit("Invalid project version")
    release = ".".join(version.split(".")[:3])
    if not args.package_only:
        host = ROOT / ".build/host"
        host.mkdir(parents=True, exist_ok=True)
        run("docker", "pull", IMAGE)
        name = "dynamic-hdr-build-" + uuid.uuid4().hex[:12]
        run("docker", "create", "--name", name, IMAGE)
        try:
            run("docker", "cp", name + ":/jellyfin/.", str(host))
        finally:
            run("docker", "rm", name)
        user = ["--user", f"{os.getuid()}:{os.getgid()}"] if hasattr(os, "getuid") else []
        run("docker", "run", "--rm", *user, "-e", "DOTNET_CLI_HOME=/tmp", "-e", "DOTNET_NOLOGO=1",
            "--mount", f"type=bind,source={ROOT},target=/work", "-w", "/work", SDK,
            "dotnet", "build", "src/Jellyfin.Plugin.DynamicHdrStripper", "-c", "Release")
    package(version, release)


if __name__ == "__main__":
    main()
