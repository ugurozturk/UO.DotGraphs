#!/usr/bin/env python3
"""Verify the single publishable tool; never build, install or publish anything."""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[1]
PACKAGE_ID = "UO.Graphs.Cli"
REPOSITORY = "https://github.com/ugurozturk/UO.DotGraphs"
TOOL = "tools/net10.0/any/"
REQUIRED = {
    "LICENSE", "README.md", "THIRD-PARTY-NOTICES.txt", "licenses/NUglify-1.21.17.txt",
    "templates/copilot/uographs/SKILL.md", "templates/copilot/uographs/references/cli.md",
    "templates/codex/uographs/SKILL.md", "templates/codex/uographs/references/cli.md",
    "scripts/install_skill.py",
    "docs/schemas/cli-envelope.schema.json", "scripts/safe_export.py",
    TOOL + "DotnetToolSettings.xml", TOOL + "uographs.dll", TOOL + "uographs.deps.json",
    TOOL + "uographs.runtimeconfig.json", TOOL + "UO.Graphs.Core.dll",
    TOOL + "UO.Graphs.Roslyn.dll", TOOL + "UO.Graphs.Storage.Sqlite.dll",
    TOOL + "BuildHost-netcore/Microsoft.CodeAnalysis.Workspaces.MSBuild.BuildHost.dll",
}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def read_version(props):
    version = ET.parse(props).findtext(".//Version", "").strip()
    require(re.fullmatch(r"(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)", version),
            "Directory.Build.props Version must be stable major.minor.patch")
    return version


def verify(directory, version, commit):
    require(re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+", version), "Invalid expected version")
    require(re.fullmatch(r"[0-9a-f]{40}", commit), "Expected full Git commit SHA")
    packages = list(directory.glob("*.nupkg"))
    require(len(packages) == 1, "Expected exactly one .nupkg")
    package = packages[0]
    require(not package.is_symlink(), "Package must not be a symlink")
    require(package.name == f"{PACKAGE_ID}.{version}.nupkg", "Wrong package filename")
    with zipfile.ZipFile(package) as archive:
        names = archive.namelist()
        require(len(names) == len(set(names)), "Duplicate ZIP entries")
        require(REQUIRED <= set(names), f"Missing payload: {sorted(REQUIRED - set(names))}")
        for name in names:
            parts = PurePosixPath(name).parts
            require(not name.startswith("/") and ".." not in parts and "\\" not in name,
                    f"Unsafe ZIP entry: {name}")
            lower = name.lower()
            require(not any(p.lower() in {".git", ".uographs", "samples", "tests"} for p in parts),
                    f"Private/test directory in package: {name}")
            require(not lower.endswith((".db", ".sqlite", ".trx", ".binlog", ".cs", ".pfx", ".pem")),
                    f"Unexpected source/evidence/secret file: {name}")
            require(not (lower.endswith(".dll") and
                         (PurePosixPath(lower).name.startswith(("volo.", "mediatr", "masstransit")) or
                          "test" in PurePosixPath(lower).name)), f"Fixture DLL in product: {name}")
        nuspecs = [n for n in names if n.endswith(".nuspec")]
        require(len(nuspecs) == 1, "Expected one nuspec")
        metadata = ET.fromstring(archive.read(nuspecs[0])).find("{*}metadata")
        require(metadata is not None, "Missing nuspec metadata")
        require(metadata.findtext("{*}id") == PACKAGE_ID, "Wrong package ID")
        require(metadata.findtext("{*}version") == version, "Wrong package version")
        license_node = metadata.find("{*}license")
        require(license_node is not None and license_node.get("type") == "expression" and license_node.text == "MIT",
                "Missing MIT expression")
        types = metadata.findall("{*}packageTypes/{*}packageType")
        require(any(t.get("name") == "DotnetTool" for t in types), "Not a DotnetTool")
        repository = metadata.find("{*}repository")
        require(repository is not None and repository.get("url") == REPOSITORY and
                repository.get("commit") == commit, "Wrong repository URL/commit")
        require(not metadata.findall("{*}dependencies/{*}group/{*}dependency") and
                not metadata.findall("{*}dependencies/{*}dependency"), "Tool must bundle its dependencies")
        commands = ET.fromstring(archive.read(TOOL + "DotnetToolSettings.xml")).findall(".//Command")
        require(len(commands) == 1 and commands[0].get("Name") == "uographs" and
                commands[0].get("EntryPoint") == "uographs.dll", "Wrong tool entry point")
        for rid in ("linux-x64", "win-x64", "osx-x64", "osx-arm64"):
            require(any(f"runtimes/{rid}/native/" in n for n in names), f"Missing SQLite runtime: {rid}")
    return {"package": package.name, "version": version, "commit": commit,
            "sha256": hashlib.sha256(package.read_bytes()).hexdigest()}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path, nargs="?")
    parser.add_argument("--version")
    parser.add_argument("--commit")
    parser.add_argument("--print-version", action="store_true")
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--write-manifest", action="store_true")
    mode.add_argument("--verify-manifest", action="store_true")
    args = parser.parse_args()
    if args.print_version:
        print(read_version(ROOT / "Directory.Build.props"))
        return
    require(args.directory is not None and args.version and args.commit, "directory, --version and --commit are required")
    result = verify(args.directory, args.version, args.commit)
    manifest = args.directory / "manifest.json"
    if args.write_manifest:
        with manifest.open("x", encoding="utf-8") as output:
            json.dump(result, output, indent=2)
            output.write("\n")
    if args.verify_manifest:
        require(not manifest.is_symlink(), "Manifest must not be a symlink")
        require(json.loads(manifest.read_text()) == result, "Artifact manifest/hash/source mismatch")
    print(f"PASS: {result['package']} ({result['sha256']})")


if __name__ == "__main__":
    main()
